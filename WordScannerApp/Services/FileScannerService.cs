using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WordScannerApp.Models;

namespace WordScannerApp.Services
{
    public class FileScannerService
    {
        private readonly object _lockObject = new object();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly SemaphoreSlim _semaphore;
        private bool _isPaused = false;
        private readonly ManualResetEvent _pauseEvent = new ManualResetEvent(true);

        public FileScannerService(int maxConcurrency = 4)
        {
            _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }

        public event EventHandler<FileScanResult>? FileScanned;
        public event EventHandler<string>? ProgressUpdated;
        public event EventHandler<int>? FilesProcessedUpdated;
        public event EventHandler<int>? TotalFilesUpdated;

        public bool IsPaused
        {
            get { lock (_lockObject) { return _isPaused; } }
            set
            {
                lock (_lockObject)
                {
                    _isPaused = value;
                    if (_isPaused)
                        _pauseEvent.Reset();
                    else
                        _pauseEvent.Set();
                }
            }
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
            IsPaused = false; // Разблокировать потоки
        }

        public async Task<List<FileScanResult>> ScanFilesAsync(
            List<string> forbiddenWords,
            string outputDirectory,
            List<string> fileExtensions)
        {
            var results = new List<FileScanResult>();
            var allFiles = new List<string>();
            int processedFiles = 0;

            // Получаем все диски
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable)
                .ToList();

            ProgressUpdated?.Invoke(this, "Поиск файлов...");

            // Собираем все файлы
            foreach (var drive in drives)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                try
                {
                    var files = GetFilesRecursive(drive.RootDirectory.FullName, fileExtensions, _cancellationTokenSource.Token);
                    allFiles.AddRange(files);
                }
                catch (Exception ex)
                {
                    ProgressUpdated?.Invoke(this, $"Ошибка при сканировании {drive.Name}: {ex.Message}");
                }
            }

            TotalFilesUpdated?.Invoke(this, allFiles.Count);
            ProgressUpdated?.Invoke(this, $"Найдено файлов: {allFiles.Count}. Начинаем обработку...");

            // Обрабатываем файлы параллельно
            var tasks = allFiles.Select(async filePath =>
            {
                await _semaphore.WaitAsync(_cancellationTokenSource.Token);
                try
                {
                    _pauseEvent.WaitOne(); // Ожидание если приостановлено
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    var result = await ScanFileAsync(filePath, forbiddenWords, outputDirectory, _cancellationTokenSource.Token);
                    
                    if (result != null)
                    {
                        lock (_lockObject)
                        {
                            results.Add(result);
                            processedFiles++;
                            FilesProcessedUpdated?.Invoke(this, processedFiles);
                        }
                        FileScanned?.Invoke(this, result);
                    }
                    else
                    {
                        lock (_lockObject)
                        {
                            processedFiles++;
                            FilesProcessedUpdated?.Invoke(this, processedFiles);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Игнорируем отмену
                }
                catch (Exception ex)
                {
                    ProgressUpdated?.Invoke(this, $"Ошибка при обработке {filePath}: {ex.Message}");
                }
                finally
                {
                    _semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return results;
        }

        private List<string> GetFilesRecursive(string directory, List<string> extensions, CancellationToken cancellationToken)
        {
            var files = new List<string>();
            
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return files;

                // Пропускаем системные директории
                var dirInfo = new DirectoryInfo(directory);
                if (dirInfo.Attributes.HasFlag(FileAttributes.System) ||
                    dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
                    return files;

                foreach (var file in Directory.GetFiles(directory))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var ext = Path.GetExtension(file).ToLower();
                    if (extensions.Contains(ext))
                    {
                        files.Add(file);
                    }
                }

                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        files.AddRange(GetFilesRecursive(subDir, extensions, cancellationToken));
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Пропускаем директории без доступа
                    }
                    catch (Exception)
                    {
                        // Игнорируем другие ошибки
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Пропускаем директории без доступа
            }
            catch (Exception)
            {
                // Игнорируем ошибки
            }

            return files;
        }

        private async Task<FileScanResult?> ScanFileAsync(
            string filePath,
            List<string> forbiddenWords,
            string outputDirectory,
            CancellationToken cancellationToken)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                    return null;

                // Читаем содержимое файла
                string content;
                Encoding encoding = Encoding.UTF8;

                try
                {
                    content = await File.ReadAllTextAsync(filePath, encoding, cancellationToken);
                }
                catch
                {
                    // Пробуем другие кодировки
                    try
                    {
                        encoding = Encoding.GetEncoding(1251);
                        content = await File.ReadAllTextAsync(filePath, encoding, cancellationToken);
                    }
                    catch
                    {
                        return null; // Не удалось прочитать файл
                    }
                }

                var result = new FileScanResult
                {
                    FilePath = filePath,
                    FileSize = fileInfo.Length
                };

                bool hasForbiddenWords = false;
                string modifiedContent = content;

                // Ищем запрещенные слова
                foreach (var word in forbiddenWords)
                {
                    if (string.IsNullOrWhiteSpace(word))
                        continue;

                    var pattern = $@"\b{Regex.Escape(word)}\b";
                    var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                    
                    if (matches.Count > 0)
                    {
                        hasForbiddenWords = true;
                        result.WordCounts[word] = matches.Count;
                        result.TotalReplacements += matches.Count;

                        // Заменяем на звездочки
                        modifiedContent = Regex.Replace(modifiedContent, pattern, "*******", RegexOptions.IgnoreCase);
                    }
                }

                if (!hasForbiddenWords)
                    return null;

                // Создаем выходную директорию если не существует
                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                // Копируем оригинальный файл
                var fileName = Path.GetFileName(filePath);
                var safeFileName = SanitizeFileName(fileName);
                var originalPath = Path.Combine(outputDirectory, $"original_{safeFileName}");
                File.Copy(filePath, originalPath, true);

                // Создаем файл с заменой
                var modifiedPath = Path.Combine(outputDirectory, $"modified_{safeFileName}");
                await File.WriteAllTextAsync(modifiedPath, modifiedContent, encoding, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                ProgressUpdated?.Invoke(this, $"Ошибка при сканировании {filePath}: {ex.Message}");
                return null;
            }
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _semaphore?.Dispose();
            _pauseEvent?.Dispose();
        }
    }
}

