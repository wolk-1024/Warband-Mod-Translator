using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;

namespace WpfApp1
{
    public partial class App : Application
    {
       /*
        private static Mutex? g_Mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string MutexName = "Warband_Mod_Translator_Mutex_1024";

            try
            {
                g_Mutex = Mutex.OpenExisting(MutexName);

                MessageBox.Show("Приложение уже запущено!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                Current.Shutdown();

                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                g_Mutex = new Mutex(true, MutexName);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            g_Mutex?.ReleaseMutex();

            g_Mutex?.Dispose();

            base.OnExit(e);
        }
       */
    }

}
