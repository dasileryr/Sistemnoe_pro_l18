using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Forms;
using WordScannerApp.Models;
using WordScannerApp.Services;

namespace WordScannerApp
{
    public partial class MainWindow : Window
    {
        private FileScannerService? _scannerService;
        private ReportService _reportService;
        private List<FileScanResult> _scanResults;
        private int _totalFiles = 0;
        private long _totalSize = 0;
        private int _totalReplacements = 0;

        public MainWindow()
        {
            InitializeComponent();
            _reportService = new ReportService();
            _scanResults = new List<FileScanResult>();
            AddLog("Приложение готово к работе");
        }

        private void LoadWordsFromFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                Title = "Выберите файл с запрещенными словами"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var words = File.ReadAllLines(dialog.FileName)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim());
                    
                    ForbiddenWordsTextBox.Text = string.Join(Environment.NewLine, words);
                    AddLog($"Загружено {words.Count()} слов из файла: {dialog.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearWords_Click(object sender, RoutedEventArgs e)
        {
            ForbiddenWordsTextBox.Clear();
            AddLog("Список запрещенных слов очищен");
        }

        private void BrowseOutputDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OutputDirectoryTextBox.Text = dialog.SelectedPath;
                AddLog($"Выбрана выходная директория: {dialog.SelectedPath}");
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация
            var forbiddenWords = ForbiddenWordsTextBox.Text
                .Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .ToList();

            if (forbiddenWords.Count == 0)
            {
                MessageBox.Show("Пожалуйста, введите или загрузите запрещенные слова.", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputDirectoryTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, укажите выходную директорию.", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Получаем расширения файлов
            var extensions = GetSelectedExtensions();
            if (extensions.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите хотя бы одно расширение файлов.", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Сброс состояния
            _scanResults.Clear();
            ResultsDataGrid.ItemsSource = null;
            _totalFiles = 0;
            _totalSize = 0;
            _totalReplacements = 0;
            UpdateStatistics();

            // Настройка UI
            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;
            ResumeButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            OverallProgressBar.Value = 0;
            FileProgressBar.Visibility = Visibility.Visible;
            StatusTextBlock.Text = "Сканирование начато...";

            // Создаем сервис сканирования
            _scannerService = new FileScannerService(maxConcurrency: 4);
            _scannerService.FileScanned += ScannerService_FileScanned;
            _scannerService.ProgressUpdated += ScannerService_ProgressUpdated;
            _scannerService.FilesProcessedUpdated += ScannerService_FilesProcessedUpdated;
            _scannerService.TotalFilesUpdated += ScannerService_TotalFilesUpdated;

            AddLog($"Начато сканирование. Запрещенных слов: {forbiddenWords.Count}");
            AddLog($"Расширения файлов: {string.Join(", ", extensions)}");

            try
            {
                var results = await _scannerService.ScanFilesAsync(
                    forbiddenWords,
                    OutputDirectoryTextBox.Text,
                    extensions);

                // Генерация отчета
                var reportPath = Path.Combine(OutputDirectoryTextBox.Text, 
                    $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                await _reportService.GenerateReportAsync(results, reportPath);
                AddLog($"Отчет сохранен: {reportPath}");

                StatusTextBlock.Text = "Сканирование завершено";
                MessageBox.Show($"Сканирование завершено!\nНайдено файлов: {results.Count}\nОтчет сохранен: {reportPath}", 
                    "Завершено", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Ошибка при сканировании";
                MessageBox.Show($"Ошибка при сканировании: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"Ошибка: {ex.Message}");
            }
            finally
            {
                StartButton.IsEnabled = true;
                PauseButton.IsEnabled = false;
                ResumeButton.IsEnabled = false;
                StopButton.IsEnabled = false;
                FileProgressBar.Visibility = Visibility.Collapsed;
                _scannerService?.Dispose();
            }
        }

        private void ScannerService_TotalFilesUpdated(object? sender, int totalFiles)
        {
            Dispatcher.Invoke(() =>
            {
                _totalFiles = totalFiles;
                ProgressTextBlock.Text = $"0 / {totalFiles} файлов обработано";
            });
        }

        private void ScannerService_FilesProcessedUpdated(object? sender, int processedFiles)
        {
            Dispatcher.Invoke(() =>
            {
                if (_totalFiles > 0)
                {
                    OverallProgressBar.Value = (double)processedFiles / _totalFiles * 100;
                    ProgressTextBlock.Text = $"{processedFiles} / {_totalFiles} файлов обработано";
                }
            });
        }

        private void ScannerService_ProgressUpdated(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = message;
                AddLog(message);
            });
        }

        private void ScannerService_FileScanned(object? sender, FileScanResult result)
        {
            Dispatcher.Invoke(() =>
            {
                _scanResults.Add(result);
                _totalSize += result.FileSize;
                _totalReplacements += result.TotalReplacements;
                
                ResultsDataGrid.ItemsSource = null;
                ResultsDataGrid.ItemsSource = _scanResults;
                
                UpdateStatistics();
                AddLog($"Найден файл: {result.FilePath} (замен: {result.TotalReplacements})");
            });
        }

        private void UpdateStatistics()
        {
            FoundFilesCountRun.Text = _scanResults.Count.ToString();
            ProcessedFilesCountRun.Text = _totalFiles.ToString();
            TotalReplacementsRun.Text = _totalReplacements.ToString();
            TotalSizeRun.Text = FormatFileSize(_totalSize);
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _scannerService?.Pause();
            PauseButton.IsEnabled = false;
            ResumeButton.IsEnabled = true;
            StatusTextBlock.Text = "Приостановлено";
            AddLog("Сканирование приостановлено");
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            _scannerService?.Resume();
            PauseButton.IsEnabled = true;
            ResumeButton.IsEnabled = false;
            StatusTextBlock.Text = "Возобновлено";
            AddLog("Сканирование возобновлено");
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _scannerService?.Cancel();
            StatusTextBlock.Text = "Остановлено";
            AddLog("Сканирование остановлено пользователем");
        }

        private List<string> GetSelectedExtensions()
        {
            var extensions = new List<string>();

            if (TxtCheckBox.IsChecked == true)
                extensions.AddRange(new[] { ".txt" });

            if (DocCheckBox.IsChecked == true)
                extensions.AddRange(new[] { ".doc", ".docx" });

            if (PdfCheckBox.IsChecked == true)
                extensions.Add(".pdf");

            if (HtmlCheckBox.IsChecked == true)
                extensions.AddRange(new[] { ".html", ".htm" });

            if (AllCheckBox.IsChecked == true)
            {
                // Все текстовые файлы
                extensions.AddRange(new[] { 
                    ".txt", ".doc", ".docx", ".pdf", ".html", ".htm", 
                    ".rtf", ".odt", ".xml", ".json", ".csv", ".log",
                    ".md", ".ini", ".cfg", ".conf", ".properties" 
                });
            }

            return extensions.Distinct().ToList();
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            LogTextBox.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        }
    }
}

