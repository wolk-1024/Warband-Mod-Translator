using System;
using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace WpfApp1
{
    public partial class App : Application
    {
        private const string MutexName = "Warband_Mod_Translator_Mutex_1024";

        private static Mutex? g_Mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            RegisterExceptionsHandlers();

            base.OnStartup(e);

            //RegisterAppMutex(MutexName);
        }

        private void RegisterExceptionsHandlers()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException; // Обработка исключений в UI потоке

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException; // Обработка исключений во всех потоках

            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException; // Обработка исключений в асинхронных операциях (Task)
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            //e.Handled = true;

            //LogException(e.Exception, "UI Thread");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var Except = e.ExceptionObject as Exception;

            LogException(Except, "Non-UI Thread");

            if (e.IsTerminating)
            {
                MessageBox.Show("Что-то пошло не так... Приложение будет закрыто.", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            //LogException(e.Exception, "Background Task");

            //e.SetObserved();
        }

        private void LogException(Exception? Except, string Source)
        {
            if (Except != null)
            {
            }
        }

        private void RegisterAppMutex(string MutexName)
        {
            bool IsRunning;

            g_Mutex = new Mutex(true, MutexName, out IsRunning);

            if (!IsRunning)
            {
                MessageBox.Show("Приложение уже запущено!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                Current.Shutdown();

                return;
            }
        }

        private void FreeMutex()
        {
            //g_Mutex?.ReleaseMutex(); // Вызывает исключение..

            //g_Mutex?.Dispose();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            //FreeMutex();

            base.OnExit(e);
        }
    }

}
