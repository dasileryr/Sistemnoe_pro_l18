using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UserMonitorApp.Models;

namespace UserMonitorApp.Services
{
    public class ProcessMonitorService : IDisposable
    {
        private readonly object _lockObject = new object();
        private bool _isMonitoring = false;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task? _monitoringTask;
        private string _reportPath = string.Empty;
        private List<string> _forbiddenPrograms = new List<string>();
        private bool _enableStatistics = true;
        private bool _enableModeration = false;
        private HashSet<string> _knownProcesses = new HashSet<string>();

        public event EventHandler<ProcessLogEntry>? ProcessStarted;
        public event EventHandler<string>? ForbiddenProcessDetected;

        public bool IsMonitoring => _isMonitoring;

        public void Start(string reportPath, List<string> forbiddenPrograms, bool enableStatistics, bool enableModeration)
        {
            if (_isMonitoring)
                return;

            _reportPath = reportPath;
            _forbiddenPrograms = forbiddenPrograms;
            _enableStatistics = enableStatistics;
            _enableModeration = enableModeration;

            // Создаем директорию для отчетов
            if (!Directory.Exists(_reportPath))
                Directory.CreateDirectory(_reportPath);

            _isMonitoring = true;
            _knownProcesses.Clear();

            // Запускаем мониторинг
            _monitoringTask = Task.Run(() => MonitoringWorker(_cancellationTokenSource.Token));
        }

        public void Stop()
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;
            _cancellationTokenSource.Cancel();
            _monitoringTask?.Wait(TimeSpan.FromSeconds(2));
        }

        private async Task MonitoringWorker(CancellationToken cancellationToken)
        {
            var logFilePath = Path.Combine(_reportPath, 
                $"ProcessLog_{DateTime.Now:yyyyMMdd}.txt");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var currentProcesses = Process.GetProcesses()
                        .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                        .ToList();

                    lock (_lockObject)
                    {
                        foreach (var process in currentProcesses)
                        {
                            try
                            {
                                var processName = process.ProcessName.ToLower();
                                var processKey = $"{processName}_{process.Id}";

                                if (!_knownProcesses.Contains(processKey))
                                {
                                    // Новый процесс
                                    _knownProcesses.Add(processKey);

                                    var entry = new ProcessLogEntry
                                    {
                                        Timestamp = DateTime.Now,
                                        ProcessName = process.ProcessName,
                                        ProcessPath = GetProcessPath(process),
                                        WasBlocked = false
                                    };

                                    // Проверка на запрещенную программу
                                    bool isForbidden = _forbiddenPrograms.Any(fp => 
                                        processName.Contains(fp.ToLower()) || 
                                        entry.ProcessPath.ToLower().Contains(fp.ToLower()));

                                    if (isForbidden && _enableModeration)
                                    {
                                        entry.WasBlocked = true;
                                        ForbiddenProcessDetected?.Invoke(this, process.ProcessName);

                                        // Закрываем процесс
                                        try
                                        {
                                            process.Kill();
                                        }
                                        catch { }

                                        // Записываем в отчет о модерации
                                        CreateModerationReport(entry);
                                    }

                                    if (_enableStatistics)
                                    {
                                        ProcessStarted?.Invoke(this, entry);
                                        LogProcessEntry(logFilePath, entry);
                                    }
                                }
                            }
                            catch
                            {
                                // Игнорируем ошибки доступа к процессу
                            }
                        }

                        // Очищаем завершенные процессы из списка известных
                        var activeKeys = currentProcesses
                            .Select(p => $"{p.ProcessName.ToLower()}_{p.Id}")
                            .ToHashSet();
                        _knownProcesses.RemoveWhere(k => !activeKeys.Contains(k));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при мониторинге процессов: {ex.Message}");
                }

                await Task.Delay(1000, cancellationToken); // Проверка каждую секунду
            }
        }

        private string GetProcessPath(Process process)
        {
            try
            {
                return process.MainModule?.FileName ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private void LogProcessEntry(string logFilePath, ProcessLogEntry entry)
        {
            try
            {
                var logLine = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] " +
                             $"Process: {entry.ProcessName}, " +
                             $"Path: {entry.ProcessPath}, " +
                             $"Blocked: {entry.WasBlocked}\n";
                File.AppendAllText(logFilePath, logLine, Encoding.UTF8);
            }
            catch { }
        }

        private void CreateModerationReport(ProcessLogEntry entry)
        {
            try
            {
                var reportPath = Path.Combine(_reportPath, 
                    $"ModerationReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                var report = new StringBuilder();
                report.AppendLine("=".PadRight(80, '='));
                report.AppendLine("ОТЧЕТ О МОДЕРИРОВАНИИ ПРОЦЕССА");
                report.AppendLine("=".PadRight(80, '='));
                report.AppendLine($"Время обнаружения: {entry.Timestamp:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"Запрещенная программа: {entry.ProcessName}");
                report.AppendLine($"Путь: {entry.ProcessPath}");
                report.AppendLine($"Действие: Процесс закрыт");
                report.AppendLine("=".PadRight(80, '='));

                File.AppendAllText(reportPath, report.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
        }
    }
}

