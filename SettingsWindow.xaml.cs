using System.Windows;

using ModTranslator;
using WarbandParser;

namespace ModTranslatorSettings
{
    public partial class SettingsWindow : Window
    {
        private MainTranslatorWindow MainWindow;

        private bool g_HideWindow = true;

        private void InitSettingsWindow()
        {
            this.Width = 300;

            this.Height = 300;

            this.ResizeMode = ResizeMode.NoResize;

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            this.Closing += SettingsWindow_Closing;

            ShowID.Unchecked += ShowID_Unchecked;

            ShowID.Checked += ShowID_Checked;

            FreeExport.ToolTip = "При экспорте в .csv заменять непереведённые строки на оригинальный текст.";

            ImportLog.ToolTip = "Запись в файл несоответствий строк импорта и загруженного оригинала.";
        }

        public SettingsWindow(MainTranslatorWindow Window)
        {
            InitializeComponent();

            InitSettingsWindow();

            MainWindow = Window;
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
            if (ShowID.IsChecked == true && MainWindow != null)
            {
                MainWindow.SetColumnVisibility("ID", Visibility.Visible);
            }
        }

        private void ShowID_Unchecked(object sender, RoutedEventArgs e)
        {
            if (ShowID.IsChecked == false && MainWindow != null)
            {
                MainWindow.SetColumnVisibility("ID", Visibility.Collapsed);
            }
        }
    }
}
