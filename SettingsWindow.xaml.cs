using Microsoft.Win32;
using ModTranslator;
using System.IO;
using System.Windows;
using WarbandParser;
using static ModTranslator.MainTranslatorWindow;

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
        private MainTranslatorWindow MainWindow;

        /// <summary>
        /// 
        /// </summary>
        private List<WorkLoad.CatInfo>? g_OldCatsData = null;

        public bool g_CompareMode = false;

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

            this.MainWindow = Window;

            Parser.g_DeleteDublicatesIDs = !this.ShowDubsID.IsChecked ?? true;

            Parser.g_IgnoreBlockingSymbol = this.ShowBlocksymbols.IsChecked ?? false;
        }

        public void CloseWindow()
        {
            this.g_HideWindow = false;

            this.Close();
        }

        private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.g_HideWindow == true) // По умолчанию не закрываем окно, а делаем невидимым.
            {
                this.Hide();

                e.Cancel = true;
            }
            else
                e.Cancel = false;
        }

        private void ShowID_Checked(object sender, RoutedEventArgs e)
        {
            if (this.MainWindow != null)
            {
                this.MainWindow.SetColumnVisibility("ID", Visibility.Visible);
            }
        }

        private void ShowID_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.MainWindow != null)
            {
                this.MainWindow.SetColumnVisibility("ID", Visibility.Collapsed);
            }
        }

        private void ShowDubsID_Checked(object sender, RoutedEventArgs e)
        {
            if (this.MainWindow != null)
            {
                Parser.g_DeleteDublicatesIDs = false;

                if (this.MainWindow.IsLoadDataGrid())
                {
                    this.MainWindow.RefreshMainGridAndSetCount();
                }
            }
        }

        private void ShowDubsID_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.MainWindow != null)
            {
                Parser.g_DeleteDublicatesIDs = true;

                if (this.MainWindow.IsLoadDataGrid())
                {
                    this.MainWindow.RefreshMainGridAndSetCount();
                }
            }
        }

        private void ShowBlocksymbols_Checked(object sender, RoutedEventArgs e)
        {
            if (this.MainWindow != null)
            {
                Parser.g_IgnoreBlockingSymbol = true;

                if (this.MainWindow.IsLoadDataGrid())
                {
                    this.MainWindow.RefreshMainGridAndSetCount();
                }
            }
        }

        private void ShowBlocksymbols_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.MainWindow != null)
            {
                Parser.g_IgnoreBlockingSymbol = false;

                if (this.MainWindow.IsLoadDataGrid())
                {
                    this.MainWindow.RefreshMainGridAndSetCount();
                }
            }
        }

        private void ShowFemales_Checked(object sender, RoutedEventArgs e)
        {
            if (this.MainWindow != null)
            {
                if (this.MainWindow.IsLoadDataGrid())
                {
                    this.MainWindow.MainDataGrid.Items.Refresh();
                }
            }
        }

        private void ShowFemales_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.MainWindow != null)
            {
                if (this.MainWindow.IsLoadDataGrid())
                {
                    this.MainWindow.MainDataGrid.Items.Refresh();
                }
            }
        }

        private void FixMenus_Click(object sender, RoutedEventArgs e)
        {
            if (this.MainWindow != null)
            {
                if (this.MainWindow.AskIfCatIsChanged("Всё равно продолжить?", "Продолжить?", MessageBoxImage.Question) == 0)
                    return;

                this.MainWindow.CatIsChanged(false);

                FixMenusDialog(this.MainWindow.g_CurrentOriginalFile);
            }
        }

        private async void CompMode_Click(object sender, RoutedEventArgs e)
        {
            if (this.MainWindow != null)
            {
                if (!this.g_CompareMode)
                {
                    if (MainWindow.AskIfCatIsChanged("Всё равно продолжить?", "Продолжить?", MessageBoxImage.Question) == 0)
                    {
                        e.Handled = true;

                        this.CompMode.IsChecked = false;

                        this.g_CompareMode = false;

                        return;
                    }

                    this.g_OldCatsData = SaveAllCats();

                    if (await MainWindow.ChooseOldModAndSeeDifference())
                    {
                        this.g_CompareMode = true;

                        this.MainWindow.Title = "(Режим сравнения)";

                        this.FixMenus.IsEnabled = false;

                        this.MainWindow.OpenModButton.IsEnabled = false;

                        this.MainWindow.RefreshMainGridAndSetCount();
                    }
                    else
                    {
                        RestoreAllCats();

                        this.g_OldCatsData = null;

                        this.g_CompareMode = false;

                        this.CompMode.IsChecked = false;

                        this.MainWindow.Title = Settings.AppTitle;

                        this.MainWindow.OpenModButton.IsEnabled = true;
                    }
                }
                else if (this.g_CompareMode)
                {
                    var Ask = MessageBox.Show($"Выйти из режима сравнения?", "Вопрос", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (Ask == MessageBoxResult.No)
                    {
                        e.Handled = true;

                        this.CompMode.IsChecked = true;

                        return;
                    }
                    else if (Ask == MessageBoxResult.Yes)
                    {
                        RestoreAllCats();

                        this.g_CompareMode = false;

                        this.FixMenus.IsEnabled = true;

                        this.MainWindow.Title = Settings.AppTitle;

                        this.MainWindow.OpenModButton.IsEnabled = true;

                        this.MainWindow.LoadCategoryFromFileBox(0);
                    }                        
                }
            }
        }

        private List<WorkLoad.CatInfo> SaveAllCats()
        {
            var Result = new List<WorkLoad.CatInfo>();

            foreach (var Cat in WorkLoad.GetBindings())
            {
                if (Cat.Rows != null) // Сохраняем только валидные категории
                    Result.Add(Cat.Clone());
            }
            return Result;
        }

        private void RestoreAllCats()
        {
            if (this.g_OldCatsData != null)
            {
                var CatsName = new List<string>();

                foreach (var Cat in this.g_OldCatsData)
                {
                    var Category = WorkLoad.FindByCategory(Cat.Category);

                    if (Category == null)
                        throw new Exception("Всё плохо. Не получилось восстановить данные категорий");

                    Category.CopyFrom(Cat);

                    CatsName.Add(Cat.Category);
                }
                this.MainWindow.SelectFilesBox.ItemsSource = CatsName;
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
                    File.Copy(MenuFilePath, BackupFullPath, RewriteBackup);

                    File.Delete(MenuFilePath);

                    File.WriteAllText(MenuFilePath, NewMenuText);

                    if (this.MainWindow.ProcessAndLoadSingleFile(MenuFilePath) == false)
                    {
                        MessageBox.Show("Что-то пошло не так...", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            return false;
        }

    }
}
