using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UserMonitorApp.Models;

namespace UserMonitorApp.Services
{
    public class KeyboardMonitorService : IDisposable
    {
        private readonly object _lockObject = new object();
        private LowLevelKeyboardHook? _keyboardHook;
        private bool _isMonitoring = false;
        private readonly Queue<KeyLogEntry> _keyQueue = new Queue<KeyLogEntry>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task? _loggingTask;
        private string _reportPath = string.Empty;
        private List<string> _forbiddenWords = new List<string>();
        private StringBuilder _currentText = new StringBuilder();
        private bool _enableStatistics = true;
        private bool _enableModeration = false;

        public event EventHandler<string>? ForbiddenWordDetected;
        public event EventHandler<KeyLogEntry>? KeyPressed;

        public bool IsMonitoring => _isMonitoring;

        public void Start(string reportPath, List<string> forbiddenWords, bool enableStatistics, bool enableModeration)
        {
            if (_isMonitoring)
                return;

            _reportPath = reportPath;
            _forbiddenWords = forbiddenWords;
            _enableStatistics = enableStatistics;
            _enableModeration = enableModeration;

            // Создаем директорию для отчетов
            if (!Directory.Exists(_reportPath))
                Directory.CreateDirectory(_reportPath);

            _keyboardHook = new LowLevelKeyboardHook();
            _keyboardHook.KeyPressed += OnKeyPressed;
            _keyboardHook.Install();

            _isMonitoring = true;

            // Запускаем задачу для записи логов
            if (_enableStatistics)
            {
                _loggingTask = Task.Run(() => LoggingWorker(_cancellationTokenSource.Token));
            }
        }

        public void Stop()
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;
            _keyboardHook?.Uninstall();
            _keyboardHook?.Dispose();
            _keyboardHook = null;

            _cancellationTokenSource.Cancel();
            _loggingTask?.Wait(TimeSpan.FromSeconds(2));

            // Записываем оставшиеся записи
            FlushQueue();
        }

        private void OnKeyPressed(object? sender, Keys key)
        {
            if (!_isMonitoring)
                return;

            var entry = new KeyLogEntry
            {
                Timestamp = DateTime.Now,
                Key = key.ToString(),
                IsSpecialKey = IsSpecialKey(key)
            };

            lock (_lockObject)
            {
                _keyQueue.Enqueue(entry);
            }

            KeyPressed?.Invoke(this, entry);

            // Обработка для модерации
            if (_enableModeration)
            {
                ProcessKeyForModeration(key);
            }
        }

        private void ProcessKeyForModeration(Keys key)
        {
            if (key == Keys.Space || key == Keys.Enter || key == Keys.Tab)
            {
                // Проверяем текущий текст на запрещенные слова
                var text = _currentText.ToString().ToLower();
                foreach (var word in _forbiddenWords)
                {
                    if (text.Contains(word.ToLower()))
                    {
                        CreateModerationReport(text, word);
                        ForbiddenWordDetected?.Invoke(this, word);
                        _currentText.Clear();
                        return;
                    }
                }
                _currentText.Clear();
            }
            else if (key == Keys.Back)
            {
                if (_currentText.Length > 0)
                    _currentText.Remove(_currentText.Length - 1, 1);
            }
            else if (!IsSpecialKey(key))
            {
                var charKey = GetCharFromKey(key);
                if (charKey != '\0')
                    _currentText.Append(charKey);
            }
        }

        private char GetCharFromKey(Keys key)
        {
            // Простое преобразование
            if (key >= Keys.A && key <= Keys.Z)
                return (char)('a' + (key - Keys.A));
            if (key >= Keys.D0 && key <= Keys.D9)
                return (char)('0' + (key - Keys.D0));
            return '\0';
        }

        private bool IsSpecialKey(Keys key)
        {
            return key == Keys.Control || key == Keys.ControlKey || 
                   key == Keys.Alt || key == Keys.Menu ||
                   key == Keys.Shift || key == Keys.ShiftKey ||
                   key == Keys.LWin || key == Keys.RWin ||
                   key == Keys.CapsLock || key == Keys.NumLock ||
                   key == Keys.Scroll;
        }

        private void CreateModerationReport(string text, string forbiddenWord)
        {
            try
            {
                var reportPath = Path.Combine(_reportPath, 
                    $"ModerationReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                var report = new StringBuilder();
                report.AppendLine("=".PadRight(80, '='));
                report.AppendLine("ОТЧЕТ О МОДЕРИРОВАНИИ");
                report.AppendLine("=".PadRight(80, '='));
                report.AppendLine($"Время обнаружения: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"Обнаружено запрещенное слово: {forbiddenWord}");
                report.AppendLine($"Текст: {text}");
                report.AppendLine("=".PadRight(80, '='));

                File.AppendAllText(reportPath, report.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при создании отчета модерации: {ex.Message}");
            }
        }

        private async Task LoggingWorker(CancellationToken cancellationToken)
        {
            var logFilePath = Path.Combine(_reportPath, 
                $"KeyLog_{DateTime.Now:yyyyMMdd}.txt");

            while (!cancellationToken.IsCancellationRequested || _keyQueue.Count > 0)
            {
                var entries = new List<KeyLogEntry>();

                lock (_lockObject)
                {
                    while (_keyQueue.Count > 0)
                    {
                        entries.Add(_keyQueue.Dequeue());
                    }
                }

                if (entries.Count > 0)
                {
                    var logText = new StringBuilder();
                    foreach (var entry in entries)
                    {
                        logText.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {entry.Key}");
                    }

                    try
                    {
                        await File.AppendAllTextAsync(logFilePath, logText.ToString(), Encoding.UTF8, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка при записи лога: {ex.Message}");
                    }
                }

                await Task.Delay(1000, cancellationToken);
            }
        }

        private void FlushQueue()
        {
            var logFilePath = Path.Combine(_reportPath, 
                $"KeyLog_{DateTime.Now:yyyyMMdd}.txt");

            var entries = new List<KeyLogEntry>();
            lock (_lockObject)
            {
                while (_keyQueue.Count > 0)
                {
                    entries.Add(_keyQueue.Dequeue());
                }
            }

            if (entries.Count > 0)
            {
                var logText = new StringBuilder();
                foreach (var entry in entries)
                {
                    logText.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {entry.Key}");
                }

                try
                {
                    File.AppendAllText(logFilePath, logText.ToString(), Encoding.UTF8);
                }
                catch { }
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
        }
    }

    // Класс для перехвата клавиатуры через Windows API
    public class LowLevelKeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public event EventHandler<Keys>? KeyPressed;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        public void Install()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule?.ModuleName ?? "UserMonitorApp"), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                var key = (Keys)vkCode;
                KeyPressed?.Invoke(this, key);
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Uninstall()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Uninstall();
        }
    }
}

