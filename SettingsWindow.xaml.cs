using System.Windows;

using ModTranslator;
using WarbandParser;

namespace ModTranslatorSettings
{
    public partial class SettingsWindow : Window
    {
        /// <summary>
        /// Главное окно.
        /// </summary>
        private MainTranslatorWindow g_MainWindow;

        private bool g_HideWindow = true;

        private void InitSettingsWindow()
        {
            this.Width = 300;

            this.Height = 300;

            this.ResizeMode = ResizeMode.NoResize;

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            this.Closing += SettingsWindow_Closing;
        }

        public SettingsWindow(MainTranslatorWindow Window)
        {
            InitializeComponent();

            InitSettingsWindow();

            g_MainWindow = Window;
        }

        public void CloseWindow()
        {
            g_HideWindow = false;

            this.Close();
        }

        private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (g_HideWindow == true) // По умолчанию не закрываем окно, а делаем невидимым.
            {
                this.Hide();

                e.Cancel = true;
            }
            else
                e.Cancel = false;
        }

        private void ShowID_Checked(object sender, RoutedEventArgs e)
        {
            if (g_MainWindow != null)
            {
                g_MainWindow.SetColumnVisibility("ID", Visibility.Visible);
            }
        }

        private void ShowID_Unchecked(object sender, RoutedEventArgs e)
        {
            if (g_MainWindow != null)
            {
                g_MainWindow.SetColumnVisibility("ID", Visibility.Collapsed);
            }
        }

        private void ShowDubsID_Checked(object sender, RoutedEventArgs e)
        {
            if (g_MainWindow != null)
            {
                Parser.g_DeleteDublicatesIDs = false;

                if (g_MainWindow.IsLoadedTextData())
                {
                    g_MainWindow.RefreshMainGridAndSetCount();
                }
            }
        }

        private void ShowDubsID_Unchecked(object sender, RoutedEventArgs e)
        {
            if (g_MainWindow != null)
            {
                Parser.g_DeleteDublicatesIDs = true;

                if (g_MainWindow.IsLoadedTextData())
                {
                    g_MainWindow.RefreshMainGridAndSetCount();
                }
            }
        }

        private void ShowBlocksymbols_Checked(object sender, RoutedEventArgs e)
        {
            if (g_MainWindow != null)
            {
                Parser.g_IgnoreBlockingSymbol = true;

                if (g_MainWindow.IsLoadedTextData())
                {
                    g_MainWindow.RefreshMainGridAndSetCount();
                }
            }
        }

        private void ShowBlocksymbols_Unchecked(object sender, RoutedEventArgs e)
        {
            if (g_MainWindow != null)
            {
                Parser.g_IgnoreBlockingSymbol = false;

                if (g_MainWindow.IsLoadedTextData())
                {
                    g_MainWindow.RefreshMainGridAndSetCount();
                }
            }
        }
    }
}
