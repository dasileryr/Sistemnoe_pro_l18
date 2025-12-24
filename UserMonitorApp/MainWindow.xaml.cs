using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Forms;
using UserMonitorApp.Models;
using UserMonitorApp.Services;

namespace UserMonitorApp
{
    public partial class MainWindow : Window
    {
        private MonitorSettings _settings;
        private KeyboardMonitorService? _keyboardMonitor;
        private ProcessMonitorService? _processMonitor;
        private int _keysPressedCount = 0;
        private int _processesStartedCount = 0;
        private int _forbiddenWordsDetected = 0;
        private int _blockedProcessesCount = 0;
        private const string SettingsFileName = "monitor_settings.json";

        public MainWindow()
        {
            InitializeComponent();
            _settings = LoadSettings();
            ApplySettingsToUI();
        }

        private void BrowseReportPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ReportPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void LoadForbiddenWords_Click(object sender, RoutedEventArgs e)
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
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearForbiddenWords_Click(object sender, RoutedEventArgs e)
        {
            ForbiddenWordsTextBox.Clear();
        }

        private void LoadForbiddenPrograms_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                Title = "Выберите файл с запрещенными программами"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var programs = File.ReadAllLines(dialog.FileName)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim());
                    
                    ForbiddenProgramsTextBox.Text = string.Join(Environment.NewLine, programs);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearForbiddenPrograms_Click(object sender, RoutedEventArgs e)
        {
            ForbiddenProgramsTextBox.Clear();
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _settings.EnableStatistics = EnableStatisticsCheckBox.IsChecked == true;
                _settings.EnableModeration = EnableModerationCheckBox.IsChecked == true;
                _settings.ReportPath = ReportPathTextBox.Text;
                _settings.ForbiddenWords = ForbiddenWordsTextBox.Text
                    .Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Trim())
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .ToList();
                _settings.ForbiddenPrograms = ForbiddenProgramsTextBox.Text
                    .Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                SaveSettings(_settings);
                MessageBox.Show("Настройки сохранены успешно!", "Успех", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении настроек: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartMonitoring_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings.ReportPath))
            {
                MessageBox.Show("Пожалуйста, укажите путь для сохранения отчетов в настройках.", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Запускаем мониторинг клавиатуры
                if (_settings.EnableStatistics || _settings.EnableModeration)
                {
                    _keyboardMonitor = new KeyboardMonitorService();
                    _keyboardMonitor.KeyPressed += KeyboardMonitor_KeyPressed;
                    _keyboardMonitor.ForbiddenWordDetected += KeyboardMonitor_ForbiddenWordDetected;
                    _keyboardMonitor.Start(_settings.ReportPath, _settings.ForbiddenWords, 
                        _settings.EnableStatistics, _settings.EnableModeration);
                }

                // Запускаем мониторинг процессов
                if (_settings.EnableStatistics || _settings.EnableModeration)
                {
                    _processMonitor = new ProcessMonitorService();
                    _processMonitor.ProcessStarted += ProcessMonitor_ProcessStarted;
                    _processMonitor.ForbiddenProcessDetected += ProcessMonitor_ForbiddenProcessDetected;
                    _processMonitor.Start(_settings.ReportPath, _settings.ForbiddenPrograms, 
                        _settings.EnableStatistics, _settings.EnableModeration);
                }

                StartMonitoringButton.IsEnabled = false;
                StopMonitoringButton.IsEnabled = true;
                MonitoringStatusTextBlock.Text = "Мониторинг запущен";
                MonitoringStatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                AddMonitoringLog("Мониторинг запущен");

                // Сброс счетчиков
                _keysPressedCount = 0;
                _processesStartedCount = 0;
                _forbiddenWordsDetected = 0;
                _blockedProcessesCount = 0;
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске мониторинга: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopMonitoring_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _keyboardMonitor?.Stop();
                _keyboardMonitor?.Dispose();
                _keyboardMonitor = null;

                _processMonitor?.Stop();
                _processMonitor?.Dispose();
                _processMonitor = null;

                StartMonitoringButton.IsEnabled = true;
                StopMonitoringButton.IsEnabled = false;
                MonitoringStatusTextBlock.Text = "Мониторинг остановлен";
                MonitoringStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                AddMonitoringLog("Мониторинг остановлен");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при остановке мониторинга: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void KeyboardMonitor_KeyPressed(object? sender, KeyLogEntry entry)
        {
            Dispatcher.Invoke(() =>
            {
                _keysPressedCount++;
                UpdateStatistics();
            });
        }

        private void KeyboardMonitor_ForbiddenWordDetected(object? sender, string word)
        {
            Dispatcher.Invoke(() =>
            {
                _forbiddenWordsDetected++;
                UpdateStatistics();
                AddMonitoringLog($"Обнаружено запрещенное слово: {word}");
            });
        }

        private void ProcessMonitor_ProcessStarted(object? sender, ProcessLogEntry entry)
        {
            Dispatcher.Invoke(() =>
            {
                _processesStartedCount++;
                UpdateStatistics();
                AddMonitoringLog($"Запущен процесс: {entry.ProcessName}");
            });
        }

        private void ProcessMonitor_ForbiddenProcessDetected(object? sender, string processName)
        {
            Dispatcher.Invoke(() =>
            {
                _blockedProcessesCount++;
                UpdateStatistics();
                AddMonitoringLog($"Заблокирован запрещенный процесс: {processName}");
            });
        }

        private void UpdateStatistics()
        {
            KeysPressedCountRun.Text = _keysPressedCount.ToString();
            ProcessesStartedCountRun.Text = _processesStartedCount.ToString();
            ForbiddenWordsDetectedRun.Text = _forbiddenWordsDetected.ToString();
            BlockedProcessesCountRun.Text = _blockedProcessesCount.ToString();
        }

        private void AddMonitoringLog(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            MonitoringLogTextBox.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
            MonitoringLogTextBox.ScrollToEnd();
        }

        private void RefreshReports_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reports = new List<ReportInfo>();

                if (Directory.Exists(_settings.ReportPath))
                {
                    var files = Directory.GetFiles(_settings.ReportPath, "*.txt");
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        var reportType = GetReportType(fileInfo.Name);
                        reports.Add(new ReportInfo
                        {
                            ReportType = reportType,
                            CreatedDate = fileInfo.CreationTime,
                            Description = fileInfo.Name,
                            FilePath = file
                        });
                    }
                }

                ReportsDataGrid.ItemsSource = reports.OrderByDescending(r => r.CreatedDate);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении отчетов: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetReportType(string fileName)
        {
            if (fileName.Contains("KeyLog"))
                return "Лог клавиатуры";
            if (fileName.Contains("ProcessLog"))
                return "Лог процессов";
            if (fileName.Contains("ModerationReport"))
                return "Отчет модерации";
            return "Другой";
        }

        private void ReportsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReportsDataGrid.SelectedItem is ReportInfo report && !string.IsNullOrEmpty(report.FilePath))
            {
                try
                {
                    var content = File.ReadAllText(report.FilePath);
                    var window = new ReportViewWindow(report.Description, content);
                    window.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при открытии отчета: {ex.Message}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenReportsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(_settings.ReportPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", _settings.ReportPath);
                }
                else
                {
                    MessageBox.Show("Папка с отчетами не существует.", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии папки: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private MonitorSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    var json = File.ReadAllText(SettingsFileName);
                    return System.Text.Json.JsonSerializer.Deserialize<MonitorSettings>(json) ?? new MonitorSettings();
                }
            }
            catch { }
            return new MonitorSettings();
        }

        private void SaveSettings(MonitorSettings settings)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFileName, json);
        }

        private void ApplySettingsToUI()
        {
            EnableStatisticsCheckBox.IsChecked = _settings.EnableStatistics;
            EnableModerationCheckBox.IsChecked = _settings.EnableModeration;
            ReportPathTextBox.Text = _settings.ReportPath;
            ForbiddenWordsTextBox.Text = string.Join(Environment.NewLine, _settings.ForbiddenWords);
            ForbiddenProgramsTextBox.Text = string.Join(Environment.NewLine, _settings.ForbiddenPrograms);
        }

        protected override void OnClosed(EventArgs e)
        {
            StopMonitoring_Click(this, new RoutedEventArgs());
            base.OnClosed(e);
        }
    }

    public class ReportInfo
    {
        public string ReportType { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }
}

