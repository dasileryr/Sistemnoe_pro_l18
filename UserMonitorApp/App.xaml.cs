using System;
using System.Threading;
using System.Windows;

namespace UserMonitorApp
{
    public partial class App : Application
    {
        private static Mutex? _mutex = null;
        private const string AppName = "UserMonitorApp";

        protected override void OnStartup(StartupEventArgs e)
        {
            // Проверка запуска только одной копии
            bool createdNew;
            _mutex = new Mutex(true, AppName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("Приложение уже запущено!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}

