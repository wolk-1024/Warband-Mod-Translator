using System.Windows;

using ModTranslator;

namespace WarbandSearch
{
    public partial class SearchWindow : Window
    {
        private readonly MainTranslatorWindow MainWindow;

        private (int RowIndex, int ColumnIndex) g_FoundedCell = (-1, -1);

        private bool g_HideWindow = true;

        private void InitSearchWindow()
        {
            this.Width = 350;

            this.Height = 150;

            this.ResizeMode = ResizeMode.NoResize;

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            this.Closing += SearchWindow_Closing;
        }

        public void CloseWindow()
        {
            g_HideWindow = false;

            this.Close();
        }

        private void SearchWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (g_HideWindow == true) // По умолчанию не закрываем окно, а делаем невидимым.
            {
                this.Hide();

                e.Cancel = true;
            }
            else
                e.Cancel = false;
        }

        public SearchWindow(MainTranslatorWindow Window)
        {
            InitializeComponent();

            InitSearchWindow();

            MainWindow = Window;
        }

        private void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            int StartRow = MainWindow.g_CurrentSelectedCell.RowIndex;

            if (StartRow >= 0 && g_FoundedCell.RowIndex >= 0)
                StartRow++;
            else
                StartRow = 0;

            bool FullSearch = false;

            if (CheckFullSearch.IsChecked != null && CheckFullSearch.IsChecked == true)
                FullSearch = true;

            var Result = MainWindow.FindCellByString(MainWindow.MainDataGrid, SearchTextBox.Text, StartRow, FullSearch, true, StringComparison.OrdinalIgnoreCase);

            if (Result.ColumnIndex >= 0 || Result.RowIndex >= 0)
            {
                g_FoundedCell = Result;

                MainWindow.FocusCell(MainWindow.MainDataGrid, Result.RowIndex, Result.ColumnIndex);
            }
            else
            {
                g_FoundedCell = (-1, -1);

                MessageBox.Show("Ничего не найдено", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

    }
}
