using System.IO;
using System.Windows;
using Microsoft.Win32;

using ModTranslator;
using WarbandParser;

namespace ModTranslatorSettings
{
    public partial class SettingsWindow : Window
    {
        /// <summary>
        /// Суффикс для дубликатов id
        /// </summary>
        private const string c_DublicateSuffix = ".";

        /// <summary>
        /// Главное окно.
        /// </summary>
        private MainTranslatorWindow g_MainWindow;

        private bool g_HideWindow = true;

        private void InitSettingsWindow()
        {
            this.Width = 300;

            this.Height = 350;

            this.ResizeMode = ResizeMode.NoResize;

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            this.Closing += SettingsWindow_Closing;
        }

        public SettingsWindow(MainTranslatorWindow Window)
        {
            InitializeComponent();

            InitSettingsWindow();

            g_MainWindow = Window;

            Parser.g_DeleteDublicatesIDs = !this.ShowDubsID.IsChecked ?? true;

            Parser.g_IgnoreBlockingSymbol = this.ShowBlocksymbols.IsChecked ?? false;
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

        private void ShowFemales_Checked(object sender, RoutedEventArgs e)
        {
            if (g_MainWindow != null)
            {
                if (g_MainWindow.IsLoadedTextData())
                {
                    g_MainWindow.MainDataGrid.Items.Refresh();
                }
            }
        }

        private void ShowFemales_Unchecked(object sender, RoutedEventArgs e)
        {
            if (g_MainWindow != null)
            {
                if (g_MainWindow.IsLoadedTextData())
                {
                    g_MainWindow.MainDataGrid.Items.Refresh();
                }
            }
        }

        private void FixMenus_Click(object sender, RoutedEventArgs e)
        {
            if (g_MainWindow != null)
            {
                g_MainWindow.DataTextChangedMessage();

                FixMenusDialog(g_MainWindow.g_CurrentOriginalFile);
            }
        }

        private string GetNewBackupFileName(string FileName)
        {
            string TimeStamp = DateTime.Now.ToString("yyMMddHHmmss");

            var Ext = Path.GetExtension(FileName);

            var Name = Path.GetFileNameWithoutExtension(FileName);

            return Name + "." + TimeStamp + Ext;
        }

        private string GetNormalFileNameFromBackup(string FileName)
        {
            var Ext = Path.GetExtension(FileName);

            var Name = Path.GetFileNameWithoutExtension(FileName);

            Name = Path.GetFileNameWithoutExtension(Name);

            return Name + Ext;
        }

        private bool FixMenusDialog(string MenuFilePath, bool RewriteBackup = true)
        {
            if (!File.Exists(MenuFilePath))
                return false;

            int RenamedIds = 0;

            var NewMenuText = Parser.ReCreateMenuFile(MenuFilePath, c_DublicateSuffix, out RenamedIds, RowFlags.Dublicate);

            if (RenamedIds == 0 && !string.IsNullOrEmpty(NewMenuText))
            {
                MessageBox.Show("Исправления не требуются", "Меню", MessageBoxButton.OK, MessageBoxImage.Information);

                return true;
            }

            if (RenamedIds > 0 && !string.IsNullOrEmpty(NewMenuText))
            {
                var BackupFullPath = Path.GetDirectoryName(MenuFilePath) + "\\" + GetNewBackupFileName(MenuFilePath);

                var Answer = MessageBox.Show($"Перезаписать {RenamedIds} дублированных ID в \"{MenuFilePath}\" ?\nБудет создана резервная копия \"{BackupFullPath}\"", "Внимание!", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (Answer == MessageBoxResult.Yes)
                {
                    g_MainWindow.DataTextChangedMessage();

                    File.Copy(MenuFilePath, BackupFullPath, RewriteBackup);

                    File.Delete(MenuFilePath);

                    File.WriteAllText(MenuFilePath, NewMenuText);

                    g_MainWindow.ProcessAndLoadOriginalFiles(MenuFilePath);
                }
            }
            return false;
        }
    }
}
