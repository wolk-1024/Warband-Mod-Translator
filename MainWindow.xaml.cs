/*
 *  (c) wolk-1024
 *  
 *  Планы на релизную версию: 
 * 
 *  Добавить работу с несколькими категориями одновременно, без перезагрузки таблицы. (важно)
 *  Исправить недостатки парсера.
 *  Добавить локализацию на английский.
 *  Доработать режим сравнения.
 *  Доработать поиск.
 *  Добавить больше горячих клавиш для поиска.
 */
using Microsoft.Win32;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
//
using WarbandAbout;
using WarbandParser;
using WarbandSearch;
using ModTranslatorSettings;
using EncodingTextFile;

//#nullable disable

namespace ModTranslator
{
    public partial class MainTranslatorWindow : Window
    {
        enum ColumnIndex
        {
            Number = 0,
            ID = 1,
            Original = 2,
            Translated = 3
        }

        public static class Settings
        {
            public static string AppTitle = "Warband Mod Translator v1.0b";

            /// <summary>
            /// Допустимые разделители в csv файлах.
            /// Для Warband ['|']
            /// Для Google Sheets [',']
            /// </summary>
            public static readonly char[] CsvSeparators = { '|', ',' };

            /// <summary>
            /// Список замен знаков, которые искаженно отображаются в игре. (Из-за шрифтов или ещё чего - я хз)
            /// </summary>
            public static readonly Dictionary<char, char> ForbiddenChars = new Dictionary<char, char>
            {
                { '—', '-' }
              //{ 'ё', 'е' },
              //{ 'Ё', 'Е' }
            };

            /// <summary>
            /// Цвет строк\ячеек по умолчанию.
            /// </summary>
            public static SolidColorBrush DefaultRowColor = Brushes.White;

            /// <summary>
            /// Цвет подсветки строк с женскими персонажами.
            /// </summary>
            public static SolidColorBrush FemalesColor = Brushes.Pink;

            /// <summary>
            /// Цвет для неизвестных строк.
            /// </summary>
            public static SolidColorBrush UnknownsColor = Brushes.LightGray;
        }

        public class WorkLoad
        {
            public class ModInfo
            {
                public string FileName   { get; set; } = string.Empty;
                public string ExportName { get; set; } = string.Empty;
                public string Category   { get; set; } = string.Empty;

                public bool IsChanged { get; set; } = false;
                public List<ModTextRow>? Rows { get; set; } = null;
            }
            /// <summary>
            /// Все данные для загрузки и экпорта файлов мода.
            /// </summary>
            public static readonly List<ModInfo> BindingsList = new List<ModInfo>
            {
                new ModInfo { FileName = "conversation.txt",    ExportName = "dialogs.csv",         Category = "Диалоги" },
                new ModInfo { FileName = "factions.txt",        ExportName = "factions.csv",        Category = "Фракции" },
                new ModInfo { FileName = "info_pages.txt",      ExportName = "info_pages.csv",      Category = "Страницы информации" },
                new ModInfo { FileName = "item_kinds1.txt",     ExportName = "item_kinds.csv",      Category = "Виды предметов" },
                new ModInfo { FileName = "item_modifiers.txt",  ExportName = "item_modifiers.csv",  Category = "Состояние предметов" },
                new ModInfo { FileName = "menus.txt",           ExportName = "game_menus.csv",      Category = "Игровое меню" },
                new ModInfo { FileName = "parties.txt",         ExportName = "parties.csv" ,        Category = "Места" },
                new ModInfo { FileName = "party_templates.txt", ExportName = "party_templates.csv", Category = "Шаблоны мест" },
                new ModInfo { FileName = "quests.txt",          ExportName = "quests.csv",          Category = "Задания" },
                new ModInfo { FileName = "quick_strings.txt",   ExportName = "quick_strings.csv",   Category = "Быстрые строки" },
                new ModInfo { FileName = "skills.txt",          ExportName = "skills.csv",          Category = "Навыки" },
                new ModInfo { FileName = "skins.txt",           ExportName = "skins.csv" ,          Category = "Скины" },
                new ModInfo { FileName = "strings.txt",         ExportName = "game_strings.csv",    Category = "Игровые строки" },
                new ModInfo { FileName = "troops.txt",          ExportName = "troops.csv",          Category = "Войска" }
            };

            public static ModInfo? FindByCategory(string CategoryName, StringComparison Compare = StringComparison.OrdinalIgnoreCase)
            {
                return BindingsList.FirstOrDefault(x => (x != null) && string.Equals(x.Category, CategoryName, Compare), null);
            }

            public static ModInfo? FindByFile(string FileName, StringComparison Compare = StringComparison.OrdinalIgnoreCase)
            {
                return BindingsList.FirstOrDefault(x => (x != null) && string.Equals(x.Category, FileName, Compare), null);
            }

            public static List<string> GetChangedCategories()
            {
                return BindingsList.Where(x => x.IsChanged).Select(x => x.Category).ToList();
            }

            public static List<ModInfo> GetChangedModInfo()
            {
                return BindingsList.Where(x => x.IsChanged).ToList();
            }

            public static List<ModInfo> GetBindings() 
            {
                return BindingsList; 
            }
        }

        /// <summary>
        /// Путь к моду.
        /// </summary>
        public string g_ModFolderPath = string.Empty;

        /// <summary>
        /// Теущий обрабатываемый файл.
        /// </summary>
        public string g_CurrentOriginalFile = string.Empty;

        /// <summary>
        /// Режим сравнения версий.
        /// </summary>
        public bool g_CompareMode = false;

        /// <summary>
        /// // Путь к .csv переводу.
        /// </summary>
        public string g_FileForExport = string.Empty;

        /// <summary>
        /// Значение текущей выделенной ячейки. (Только ячейка из столбца перевода)
        /// </summary>
        private string g_CurrentCellValue = string.Empty;

        /// <summary>
        /// Индекс текущей выделенной ячейки.
        /// </summary>
        public (int RowIndex, int ColumnIndex) g_CurrentSelectedCell = (-1, -1);

        /// <summary>
        /// Окно настроек.
        /// </summary>
        private readonly SettingsWindow g_OptionsWindow;

        /// <summary>
        /// Окошко поиска.
        /// </summary>
        private readonly SearchWindow g_SearchWindow;

        private class TextImportInfo
        {
            public int SuccessLoaded { get; set; }

            public List<ModTextRow> FailedLoad = new List<ModTextRow> { };
        }

        public void InitMainWindow()
        {
            this.Title = Settings.AppTitle;

            this.WindowState = WindowState.Maximized;

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            this.Closing += MainWindowClosing; // Обработка закрытия окна.

            this.KeyDown += KeyDownHandler;  // Глобальная обработка нажатий клавиш.
        }

        public MainTranslatorWindow()
        {
            InitializeComponent();

            InitMainWindow();

            g_OptionsWindow = new SettingsWindow(this);

            g_SearchWindow = new SearchWindow(this);
        }

        private void MainWindowClosing(object? sender, CancelEventArgs e)
        {
            if (IsLoadDataGrid())
            {
                var Result = AskIfAllCatsChanged("Выйти без их сохранения?", "Подтверждение", MessageBoxImage.Question);

                if (Result == 1)
                    e.Cancel = false;
                else if (Result == 0)
                {
                    e.Cancel = true;

                    return;
                }
            }
            g_OptionsWindow.CloseWindow();

            g_SearchWindow.CloseWindow();
        }

        /// <summary>
        /// -1 данные не изменены. 1 - да. 0 - нет.
        /// </summary>
        public int AskIfAllCatsChanged(string MessageIfChanged, string Caption, MessageBoxImage Image)
        {
            var ListName = WorkLoad.GetChangedCategories();

            if (ListName.Count > 0)
            {
                string Categories = string.Join(", ", ListName);

                MessageBoxResult Result = MessageBox.Show($"Данные были изменены в: {Categories}\n\n{MessageIfChanged}", Caption, MessageBoxButton.YesNo, Image);

                if (Result == MessageBoxResult.Yes)
                    return 1;
                else if (Result == MessageBoxResult.No)
                    return 0;
            }
            return -1;
        }

        /// <summary>
        /// -1 данные не изменены. 1 - да. 0 - нет.
        /// </summary>
        public int AskIfCatIsChanged(string MessageIfChanged, string Caption, MessageBoxImage Image)
        {
            var ModInfo = GetCurrentMod();

            if (ModInfo != null && ModInfo.IsChanged)
            {
                MessageBoxResult Result = MessageBox.Show($"Данные были изменены в: {ModInfo.Category}\n\n{MessageIfChanged}", Caption, MessageBoxButton.YesNo, Image);

                if (Result == MessageBoxResult.Yes)
                    return 1;
                else if (Result == MessageBoxResult.No)
                    return 0;
            }
            return -1;
        }

        ///<summary>Установить для всех категорий состояния</summary>
        public void AllCatsIsChanged(bool Bool)
        {
            var List = WorkLoad.GetChangedModInfo();

            if (List.Count > 0)
                foreach (var Item in List) 
                    Item.IsChanged = Bool;
        }

        ///<summary>Установить состояние только для текущей категории</summary>
        public void CatIsChanged(bool Bool)
        {
            var CurrentMod = GetCurrentMod();

            if (CurrentMod != null)
                CurrentMod.IsChanged = Bool;
        }

        private void EnableControlButtons()
        {
            SelectFilesBox.IsEnabled = true;

            ImportButton.IsEnabled   = true;

            PrevCellButton.IsEnabled = true;

            NextCellButton.IsEnabled = true;

            SaveButton.IsEnabled     = true;
        }

        public bool IsLoadDataGrid()
        {
            return (MainDataGrid.Items.Count > 0 && File.Exists(g_CurrentOriginalFile));
        }

        /// <summary>
        /// Возвращает биндинг с таблицей. Id, текст, перевод и т.д
        /// </summary>
        private List<ModTextRow> GetMainRows()
        {
            var Bindings = MainDataGrid.ItemsSource as List<ModTextRow>;

            if (Bindings != null)
                return Bindings;

            return new List<ModTextRow>();
        }

        /// <summary>
        /// 
        /// </summary>
        private WorkLoad.ModInfo? GetCurrentMod()
        {
            var Bindings = MainDataGrid.ItemsSource as List<ModTextRow>;

            if (Bindings != null)
                return WorkLoad.BindingsList.Find(x => (x.Rows == Bindings));

            return null;
        }

        /// <summary>
        /// Обновляет таблицу, подсчитывает видимые строки.
        /// </summary>
        /// <param name="TextData"></param>
        private void RefreshMainGrid(List<ModTextRow> Rows)
        {
            UpdateVisibleRowsNumbers(Rows);

            g_CurrentSelectedCell = (-1, -1);

            MainDataGrid.ItemsSource = null;

            MainDataGrid.ItemsSource = Rows;

            MainDataGrid.Items.Refresh();

            //FocusFirstVisibleRow();
        }

        public void RefreshMainGrid()
        {
            RefreshMainGrid(GetMainRows());
        }

        public void RefreshMainGridAndSetCount(List<ModTextRow> Rows)
        {
            RefreshMainGrid(Rows);

            SetTranslateCountLabel();
        }

        public void RefreshMainGridAndSetCount()
        {
            RefreshMainGridAndSetCount(GetMainRows());
        }

        /// <summary>
        /// Функция для проверки видимости строки.
        /// </summary>
        /// <param name="TextData"></param>
        /// <returns>Вернёт true, если строка будет видна в таблице.</returns>
        public static bool IsVisibleRow(ModTextRow TextData)
        {
            if (TextData != null)
            {
                var Flags = TextData.Flags;

                if (Flags.HasFlag(RowFlags.Dublicate) || Flags.HasFlag(RowFlags.DublicateDifferentValue))
                {
                    if (Parser.g_DeleteDublicatesIDs) // и мы их удаляем, то
                        return false; // строка не видна.
                }
                if (Flags.HasFlag(RowFlags.BlockSymbol)) // Если есть блокирующий символ {!}
                {
                    if (!Parser.g_IgnoreBlockingSymbol) // и мы НЕ игнорим их, то
                        return false; // строка не видна.
                }
                if (Flags.HasFlag(RowFlags.ParseError)) // Ошибки парсера не показываем.
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Нумерует только видимые строки.
        /// </summary>
        /// <param name="TextMod"></param>
        public static void UpdateVisibleRowsNumbers(List<ModTextRow> TextMod)
        {
            var VisibleCount = 0;

            foreach (var Row in TextMod)
            {
                Row.RowNum = 0;

                if (IsVisibleRow(Row))
                {
                    VisibleCount++;

                    Row.RowNum = VisibleCount;
                }
            }
        }

        private static int CountFieldsOutsideQuotes(string TextLine, char Separator)
        {
            bool InQuotes = false;

            int Result = 1;

            for (int i = 0; i < TextLine.Length; i++)
            {
                char Char = TextLine[i];

                if (Char == '"')
                {
                    if (i + 1 < TextLine.Length && TextLine[i + 1] == '"')
                        i++; // Пропустить экранированную кавычку
                    else
                        InQuotes = !InQuotes;
                }
                else if (Char == Separator && !InQuotes)
                {
                    Result++;
                }
            }
            return Result;
        }

        public static List<string> ParseCsvLine(string TextLine, char Separator)
        {
            var Result = new List<string>();

            if (string.IsNullOrEmpty(TextLine))
                return Result;

            bool InQuotes = false;

            var СurrentField = new StringBuilder();

            bool SeparatorFound = false;

            for (int i = 0; i < TextLine.Length; i++)
            {
                char currentChar = TextLine[i];

                if (currentChar == '"')
                {
                    if (InQuotes && i + 1 < TextLine.Length && TextLine[i + 1] == '"')
                    {
                        СurrentField.Append('"');

                        i++; 
                    }
                    else
                    {
                        InQuotes = !InQuotes;
                    }
                }
                else if (currentChar == Separator && !InQuotes && !SeparatorFound)
                {
                    Result.Add(СurrentField.ToString());

                    СurrentField.Clear();

                    SeparatorFound = true;
                }
                else
                {
                    СurrentField.Append(currentChar);
                }
            }
            Result.Add(СurrentField.ToString());

            return Result;
        }

        public static char? DetectCsvLineSeparator(string TextLine, char[] CandidateSeparators)
        {
            if (string.IsNullOrEmpty(TextLine) || CandidateSeparators == null || CandidateSeparators.Length == 0)
                return null;

            char? Result = null;

            int MaxIndex = TextLine.Length;

            foreach (char Sep in CandidateSeparators)
            {
                int Index = TextLine.IndexOf(Sep);

                if (Index >= 0 && Index < MaxIndex)
                {
                    var CountFilds = CountFieldsOutsideQuotes(TextLine, Sep);

                    if (CountFilds >= 2)
                    {
                        MaxIndex = Index;

                        Result = Sep;
                    }
                }
            }
            return Result;
        }

        public static char? DetectCsvFileSeparator(string FilePath, char[] CandidateSeparators, int SampleLines)
        {
            if (!File.Exists(FilePath) || CandidateSeparators.Length == 0)
                return null;

            int FoundCount = 0;

            var Result = new List<char> { };

            foreach (string Line in File.ReadLines(FilePath))
            {
                if (FoundCount >= SampleLines)
                    break;

                string Cleaned = new string(Line.Where(c => !char.IsControl(c)).ToArray()).Trim(); // Убираем переносы и пробелы из строки.

                if (!string.IsNullOrEmpty(Cleaned))
                {
                    var CsvSeparator = DetectCsvLineSeparator(Line, CandidateSeparators);

                    if (CsvSeparator == null)
                        return null;

                    Result.Add((char)CsvSeparator);

                    FoundCount++;
                }
            }
            return Result.Distinct().Count() == 1 ? Result[0] : null;
        }

        public static List<ModTextRow> ReadModTextCsvFile(string FilePath, char[] Separators, int MaxArgs = 3)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var CsvSeparator = DetectCsvFileSeparator(FilePath, Separators, 5); // Допустимые разделители в csv

            if (CsvSeparator == null)
                return Result;

            var AllLines = File.ReadAllLines(FilePath, Encoding.UTF8);

            if (AllLines.Length == 0)
                return Result;

            foreach (var CurrentLine in AllLines)
            {
                if (string.IsNullOrWhiteSpace(CurrentLine))
                    continue; // Пропускаем пустые строки, если они есть.

                var LineArgs = ParseCsvLine(CurrentLine, (char)CsvSeparator);

                if (LineArgs.Count <= 1 || LineArgs.Count > MaxArgs) // Параметров не должно быть больше MaxArgs, если больше, то явная ошибка в ParseCsvLine
                {
                    //Result.Clear();

                    //return Result;
                    continue;
                }

                Result.Add(new ModTextRow
                {
                    RowId = LineArgs[0],
                    TranslatedText = LineArgs[1]
                });
            }
            return Result;
        }

        public static List<ModTextRow>? ProcessOriginalFiles(string FilePath)
        {
            if (!File.Exists(FilePath))
                return null;

            List<ModTextRow>? Result = null;

            try
            {
                var FileName = Path.GetFileName(FilePath);

                switch (FileName)
                {
                    case "conversation.txt":
                        {
                            Result = Parser.LoadAndParseConversationFile(FilePath);
                            break;
                        }
                    case "factions.txt":
                        {
                            Result = Parser.LoadAndParseFactionsFile(FilePath);
                            break;
                        }
                    case "info_pages.txt":
                        {
                            Result = Parser.LoadAndParseInfoPagesFile(FilePath);
                            break;
                        }
                    case "item_kinds1.txt":
                        {
                            Result = Parser.LoadAndParseItemKindsFile(FilePath);
                            break;
                        }
                    case "item_modifiers.txt": // Этого файла часто не бывает в модах.
                        {
                            Result = Parser.LoadAndParseItemModifiersFile(FilePath);
                            break;
                        }
                    case "menus.txt": // Есть проблемы: в файле ингода содержатся одинаковые id, но имеют разное значение.
                        {
                            Result = Parser.LoadAndParseMenuFile(FilePath);
                            break;
                        }
                    case "parties.txt":
                        {
                            Result = Parser.LoadAndParsePartiesFile(FilePath);
                            break;
                        }
                    case "party_templates.txt":
                        {
                            Result = Parser.LoadAndParsePartyTemplatesFile(FilePath);
                            break;
                        }
                    case "quests.txt":
                        {
                            Result = Parser.LoadAndParseQuestsFile(FilePath);
                            break;
                        }
                    case "quick_strings.txt":
                        {
                            Result = Parser.LoadAndParseQuickStringsFile(FilePath);
                            break;
                        }
                    case "skills.txt":
                        {
                            Result = Parser.LoadAndParseSkillsFile(FilePath);
                            break;
                        }
                    case "skins.txt":
                        {
                            Result = Parser.LoadAndParseSkinsFile(FilePath);
                            break;
                        }
                    case "strings.txt":
                        {
                            Result = Parser.LoadAndParseStringsFile(FilePath);
                            break;
                        }
                    case "troops.txt":
                        {
                            Result = Parser.LoadAndParseTroopsFile(FilePath);
                            break;
                        }
                }
            }
            catch (Exception)
            {
                MessageBox.Show($"При обработке: {FilePath} произошла критическая ошибка.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Stop);

                return null;
            }
            return Result;
        }

        private static object? GetDataGridCellValue(DataGridCellInfo CellInfo)
        {
            var Binding = new Binding();

            if (CellInfo.Column is DataGridTextColumn)
            {
                Binding = ((DataGridTextColumn)CellInfo.Column).Binding as Binding;
            }

            if (Binding != null)
            {
                string PropertyName = Binding.Path.Path;

                var BoundItem = CellInfo.Item;

                var PropertyInfo = BoundItem.GetType().GetProperty(PropertyName);

                if (PropertyInfo != null)
                    return PropertyInfo.GetValue(CellInfo.Item);
            }
            return null;
        }

        private static string GetCellStringValue(object? Sender)
        {
            if (Sender != null)
            {
                var Result = GetDataGridCellValue(((DataGrid)Sender).CurrentCell);

                if (Result != null)
                    return (string)Result;
            }
            return string.Empty;
        }

        /// <returns>Вернёт null, если ничего не найдёт или ошибка.</returns>
        public static string? GetCellStringValue(DataGrid Table, int RowIndex, int ColumnIndex, bool OnlyVisible = true)
        {
            var Row = Table.Items[RowIndex] as ModTextRow;

            if (Row != null)
            {
                if (OnlyVisible)
                {
                    if (!IsVisibleRow(Row)) // Не ищем, если указано искать только видимые
                        return null;
                }

                var Column = Table.Columns[ColumnIndex];

                if (Column is DataGridBoundColumn BoundColumn)
                {
                    var Binding = BoundColumn.Binding as Binding;

                    if (Binding != null)
                    {
                        var PropertyInfo = Row.GetType().GetProperty(Binding.Path.Path);

                        if (PropertyInfo == null)
                            return null;

                        var Value = PropertyInfo.GetValue(Row);

                        if (Value != null)
                            return Value.ToString();
                    }
                }
            }
            return null;
        }

        public static bool SetCellValue(DataGrid TableGrid, int RowIndex, int ColumnIndex, string NewValue)
        {
            if (RowIndex < 0 || RowIndex >= TableGrid.Items.Count || ColumnIndex < 0 || ColumnIndex >= TableGrid.Columns.Count)
            {
                return false;
            }

            var Row = TableGrid.Items[RowIndex];

            var PropertyInfo = Row.GetType().GetProperties()[ColumnIndex];

            try
            {
                //var ConvertedValue = Convert.ChangeType(newValue, PropertyInfo.PropertyType);

                PropertyInfo.SetValue(Row, NewValue);

                var Result = GetCellStringValue(TableGrid, RowIndex, ColumnIndex);

                if (Result == NewValue)
                    return true;
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        // Вернет любую
        public ModTextRow? GetRowDataByIndex(int Index)
        {
            if (MainDataGrid.Items.Count > Index && Index >= 0)
            {
                object Data = MainDataGrid.Items[Index];

                if (Data != null)
                    return Data as ModTextRow;
            }
            return null;
        }

        public bool SetColumnVisibility(string Name, Visibility Value)
        {
            var Column = GetColumnByName(Name);

            if (Column != null)
            {
                Column.Visibility = Value;

                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FilePath"></param>
        /// <param name="LoadInEmpty"></param>
        /// <returns></returns>
        private TextImportInfo ImportTranslate(string FilePath, bool LoadInEmpty = false)
        {
            var Result = new TextImportInfo { SuccessLoaded = 0 };

            var TranslateData = ReadModTextCsvFile(FilePath, Settings.CsvSeparators, 3);

            if (TranslateData.Count >= 1)
            {
                var MainRows = GetMainRows();

                foreach (ModTextRow Data in TranslateData)
                {
                    if (Parser.IsDummyRow(Data)) // Не импортируем "противобаговую" строку.
                        continue;

                    // Чувствителен к регистру.
                    var DataIndex = MainRows.FindIndex(0, MainRows.Count, Item => string.Equals(Item.RowId, Data.RowId, StringComparison.Ordinal)); // Ищем совпадения по Id

                    // Не чуствителен
                    //var DataIndex = g_CurrentMainGridData.FindIndex(0, g_CurrentMainGridData.Count, Item => string.Equals(Item.RowId, Data.RowId, StringComparison.OrdinalIgnoreCase));

                    if (DataIndex >= 0)
                    {
                        if (LoadInEmpty) // Загрузка только в пустые
                        {
                            if (string.IsNullOrEmpty(MainRows[DataIndex].TranslatedText))
                            {
                                MainRows[DataIndex].TranslatedText = Data.TranslatedText;

                                Result.SuccessLoaded++;
                            }
                        }
                        else
                        {
                            MainRows[DataIndex].TranslatedText = Data.TranslatedText;

                            Result.SuccessLoaded++;
                        }
                    }
                    else
                        Result.FailedLoad.Add(Data); // В файле-переводе и оригинале нет совпадения ID. (Вероятно, импортируемый файл может быть старой версией перевода)
                }
                RefreshMainGrid(MainRows);
            }
            return Result;
        }

        private long HowManyTranslatedLines()
        {
            long TranslatedCount = 0;

            foreach (var Item in MainDataGrid.Items)
            {
                if (Item is ModTextRow TextMod)
                {
                    // Считаем все, включая скрытые, но не ошибки.
                    if (!TextMod.Flags.HasFlag(RowFlags.ParseError))
                    {
                        if (!string.IsNullOrEmpty(TextMod.TranslatedText))
                            TranslatedCount++;
                    }
                }
            }
            return TranslatedCount;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="TableGrid"></param>
        /// <returns></returns>
        private static long GetVisibleCount(DataGrid TableGrid)
        {
            long VisibleCount = 0;

            foreach (var Item in TableGrid.Items)
            {
                if (Item is ModTextRow TextMod)
                {
                    if (IsVisibleRow(TextMod))
                        VisibleCount++;
                }
            }
            return VisibleCount;
        }

        public long GetVisibleCount()
        {
            return GetVisibleCount(MainDataGrid);
        }

        public void SetTranslateCountLabel(long TranslateLines)
        {
            var Hiden = MainDataGrid.Items.Count - GetVisibleCount();

            //var NeedToTranslate = MainDataGrid.Items.Count - TranslateLines;

            TranslateCount.Content = string.Format($"Загружено: {MainDataGrid.Items.Count} | Скрыто: {Hiden} | Переведено: {TranslateLines}");
        }

        public void SetTranslateCountLabel()
        {
            SetTranslateCountLabel(HowManyTranslatedLines());
        }

        private string ReplaceForbiddenChars(string Input)
        {
            if (string.IsNullOrEmpty(Input))
                return Input;

            return new string(Input.Select(c => Settings.ForbiddenChars.GetValueOrDefault(c, c)).ToArray());
        }

        private void ExportModTextToFile(string FilePath, bool EmptyExport)
        {
            bool WriteDummy = false;

            using (StreamWriter WriteText = new StreamWriter(FilePath))
            {
                var MainRows = GetMainRows();

                foreach (var TextData in MainRows)
                {
                    if (WriteDummy == false)
                    {
                        var Prefix = Parser.ExtractPrefixFromId(TextData.RowId);

                        WriteText.WriteLine(Prefix + "1164|Do not delete this line");

                        WriteDummy = true;
                    }

                    if (IsVisibleRow(TextData)) // Пишем только видимые строки.
                    {
                        string TranslatedData = TextData.TranslatedText;

                        if (EmptyExport) // Если экспорт только пустых
                        {
                            if (string.IsNullOrEmpty(TranslatedData))
                                TranslatedData = TextData.OriginalText; // Меняем пустой перевод на оригинал
                            else
                                continue; // Если не пустая, то пропускаем запись.
                        }

                        if (!string.IsNullOrEmpty(TranslatedData)) // Не записываем в файл поля без перевода. (Возможно стоит?)
                        {
                            var ResultTranlatedText = ReplaceForbiddenChars(TranslatedData);

                            string NewLine = TextData.RowId + "|" + ResultTranlatedText;

                            WriteText.WriteLine(NewLine);
                        }
                    }
                }
            }
        }

        private string GetTranslateFileNameFromFileBox()
        {
            var SelectedCategory = SelectFilesBox.SelectedValue.ToString();

            if (!string.IsNullOrEmpty(SelectedCategory))
            {
                var LoadedFile = WorkLoad.FindByCategory(SelectedCategory);

                if (LoadedFile != null)
                {
                    var ExportName = LoadedFile.ExportName;

                    if (!string.IsNullOrEmpty(ExportName))
                        return ExportName;
                }
            }
            return "translate.csv";
        }

        private void ExportFileDialog(string SaveTo = "")
        {
            var FileDialog = new SaveFileDialog();

            FileDialog.Title = "Экспорт перевода";

            FileDialog.Filter = "CSV файлы|*.csv|Текстовые файлы|*.txt";

            var CsvName = SaveTo;

            if (string.IsNullOrEmpty(CsvName))
                CsvName = Path.GetFileName(g_FileForExport);

            if (g_CompareMode)
                FileDialog.FileName = "new_" + CsvName;
            else
                FileDialog.FileName = CsvName;

            FileDialog.OverwritePrompt = true; // Спросить о перезаписи файла.

            var SourceDirectory = g_ModFolderPath + "\\languages";

            if (Directory.Exists(SourceDirectory))
                FileDialog.InitialDirectory = SourceDirectory;
            else
                FileDialog.InitialDirectory = g_ModFolderPath;

            if (FileDialog.ShowDialog() == true)
            {
                var EmptyExport = g_OptionsWindow.EmptyExport.IsChecked;

                if (EmptyExport == null)
                    EmptyExport = false;

                ExportModTextToFile(FileDialog.FileName, (bool)EmptyExport);

                CatIsChanged(false);

                g_FileForExport = FileDialog.FileName;
            }
        }

        private void DataGridSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            foreach (DataGridCellInfo CellInfo in e.AddedCells)
            {
                var SelectedItem = CellInfo.Item;

                int RowIndex = MainDataGrid.Items.IndexOf(SelectedItem);

                if (RowIndex != -1)
                {
                    g_CurrentSelectedCell.RowIndex = RowIndex;

                    g_CurrentSelectedCell.ColumnIndex = CellInfo.Column.DisplayIndex;

                    break;
                }
            }
        }

        private void DataGridCellBegining(object? sender, DataGridBeginningEditEventArgs e)
        {
            if ((ColumnIndex)e.Column.DisplayIndex == ColumnIndex.Number) // №
            {
                //
            }

            if ((ColumnIndex)e.Column.DisplayIndex == ColumnIndex.ID) // ID
            {
                //
            }

            if ((ColumnIndex)e.Column.DisplayIndex == ColumnIndex.Original) // Оригинал
            {
                //
            }

            if ((ColumnIndex)e.Column.DisplayIndex == ColumnIndex.Translated) // Перевод
            {
                g_CurrentCellValue = GetCellStringValue(sender);
            }
        }

        private void DataGridCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if ((ColumnIndex)e.Column.DisplayIndex == ColumnIndex.Number)
            {
                //
            }

            if ((ColumnIndex)e.Column.DisplayIndex == ColumnIndex.ID)
            {
                //
            }

            if ((ColumnIndex)e.Column.DisplayIndex == ColumnIndex.Original)
            {
                //
            }

            if ((ColumnIndex)e.Column.DisplayIndex == ColumnIndex.Translated)
            {
                if (e.EditAction == DataGridEditAction.Commit)
                {
                    var TranslatedLines = HowManyTranslatedLines();

                    var NewText = ((TextBox)e.EditingElement).Text;

                    if (NewText != g_CurrentCellValue) // Если ячейка была изменена
                    {
                        CatIsChanged(true);

                        if (NewText == string.Empty) // и стала пустой, то
                        {
                            if (TranslatedLines > 0)
                                TranslatedLines--; // уменьшаем счётчик.
                        }

                        if (g_CurrentCellValue == string.Empty) // Если ячейка была изменена и при этом она была пустой, то
                            TranslatedLines++; // увеличиваем счётчик.
                    }
                    SetTranslateCountLabel(TranslatedLines);

                    g_CurrentCellValue = string.Empty;
                }
            }
        }

        public DataGridColumn? GetColumnByName(DataGrid Data, string Name)
        {
            foreach (var Column in Data.Columns)
            {
                if (Column.Header.ToString() == Name)
                    return Column;
            }
            return null;
        }

        public DataGridColumn? GetColumnByName(string Name)
        {
            return GetColumnByName(MainDataGrid, Name);
        }

        public DataGridColumn? GetColumnByIndex(DataGrid Data, int Index)
        {
            if (Data != null)
            {
                if (Index <= Data.Columns.Count)
                    return Data.Columns[Index];
            }
            return null;
        }

        public int GetColumnIndexByName(DataGrid Data, string Name)
        {
            int Result = -1;

            foreach (var Column in Data.Columns)
            {
                Result++;

                if (Column.Header.ToString() == Name)
                    return Result;
            }
            return Result;
        }

        public ModTextRow? SearchModTextByValue(string Value, int StartRow = 0, bool FullSearch = true, bool OnlyVisible = true, StringComparison CaseType = StringComparison.Ordinal)
        {
            var Indexes = FindCellByString(MainDataGrid, Value, StartRow, FullSearch, OnlyVisible, CaseType);

            if (Indexes.ColumnIndex >= 0 && Indexes.RowIndex >= 0)
            {
                var Item = MainDataGrid.Items.GetItemAt(Indexes.RowIndex);

                var Result = Item as ModTextRow;

                return Result;
            }
            return null;
        }

        public static int FindCellByStringInColumn(DataGrid DataTable, string Value, int StartRow, int ColumnIndex, bool FullSearch = false, bool OnlyVisible = true, StringComparison Case = StringComparison.Ordinal)
        {
            if (ColumnIndex > DataTable.Columns.Count || StartRow < 0 || ColumnIndex < 0)
                return -1;

            if (OnlyVisible)
            {
                if (DataTable.Columns[ColumnIndex].Visibility != Visibility.Visible)
                    return -1;
            }

            for (int Row = StartRow; Row < DataTable.Items.Count; Row++)
            {
                var CellValue = GetCellStringValue(DataTable, Row, ColumnIndex, OnlyVisible);

                if (CellValue != null)
                {
                    string Result = CellValue.ToString();

                    if (FullSearch)
                    {
                        if (Result.Equals(Value, Case))
                            return Row;
                    }
                    else
                    {
                        if (Result.IndexOf(Value, Case) >= 0)
                            return Row;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// 
        /// <param name="DataTable"></param>
        /// <param name="Value"></param>
        /// <param name="StartRow">Стартовый индекс строки от которой будет идти поиск</param>
        /// <param name="FullSearch">Поиск строки целиком, а не по первому вхождению.</param>
        /// <param name="OnlyVisible">Поиск только в отображаемых данных</param>
        /// <param name="Case"></param>
        /// <returns></returns>
        public (int RowIndex, int ColumnIndex) FindCellByString(DataGrid DataTable, string Value, int StartRow, bool FullSearch = false, bool OnlyVisible = true, StringComparison Case = StringComparison.Ordinal)
        {
            for (int ColumnIndex = 0; ColumnIndex < DataTable.Columns.Count; ColumnIndex++)
            {
                var RowIndex = FindCellByStringInColumn(DataTable, Value, StartRow, ColumnIndex, FullSearch, OnlyVisible, Case);

                if (RowIndex >= 0)
                    return (RowIndex, ColumnIndex);
            }
            return (-1, -1);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ModFolderPath"></param>
        /// <returns></returns>
        private int LoadModFilesAndCategories(string ModFolderPath)
        {
            var LoadCount = -1;

            var CategoryList = new List<string> { };

            foreach (var ModFile in WorkLoad.GetBindings())
            {
                string FullPath = Path.Combine(ModFolderPath, ModFile.FileName);

                if (File.Exists(FullPath))
                {
                    var LoadedFile = ProcessOriginalFiles(FullPath);

                    if (LoadedFile == null)
                    {
                        var Ask = MessageBox.Show($"При загрузке {FullPath} произшла ошибка. Продолжить загрузку?", "Ошибка загрузки", MessageBoxButton.YesNo, MessageBoxImage.Error);

                        if (Ask == MessageBoxResult.Yes)
                            continue;

                        return -1;
                    }
                    LoadCount++;

                    ModFile.Rows = LoadedFile;

                    ModFile.IsChanged = false;

                    CategoryList.Add(ModFile.Category);
                }
            }
            if (LoadCount >= 0)
            {
                SelectFilesBox.SelectedIndex = 0;

                SelectFilesBox.ItemsSource = CategoryList;

                SelectFilesBox.Items.Refresh();
            }
            return LoadCount;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FilePath"></param>
        /// <returns></returns>
        public bool ProcessAndLoadSingleFile(string FilePath)
        {
            if (!File.Exists(FilePath))
                return false;

            var BindMod = WorkLoad.FindByFile(Path.GetFileName(FilePath));

            if (BindMod != null)
            {
                var LoadedData = ProcessOriginalFiles(FilePath);

                if (LoadedData != null)
                {
                    BindMod.IsChanged = false;

                    BindMod.Rows = LoadedData;

                    RefreshMainGridAndSetCount(BindMod.Rows);

                    return true;
                }
            }
            return false;
        }

        private bool LoadCategoryFromFileBox(int CategoryIndex)
        {
            var SelectedCategory = SelectFilesBox.SelectedValue?.ToString();

            if (!string.IsNullOrEmpty(SelectedCategory))
            {
                var LoadedFile = WorkLoad.FindByCategory(SelectedCategory);

                if (LoadedFile == null || LoadedFile.Rows == null)
                {
                    MessageBox.Show($"Не удалось загрузить {SelectedCategory}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                    return false;
                }
                RefreshMainGridAndSetCount(LoadedFile.Rows);

                return true;
            }
            return false;
        }

        private void SelectFilesBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            g_CompareMode = false;

            var SelectedCategory = SelectFilesBox.SelectedValue?.ToString();

            if (string.IsNullOrEmpty(SelectedCategory))
                return;

            var LoadedFile = WorkLoad.FindByCategory(SelectedCategory);

            if (LoadedFile != null)
            {
                g_FileForExport = Path.Combine(g_ModFolderPath + "\\languages\\" + LoadedFile.ExportName);

                if (LoadedFile.Rows != null)
                {
                    g_CurrentOriginalFile = Path.Combine(g_ModFolderPath, LoadedFile.FileName);

                    RefreshMainGridAndSetCount(LoadedFile.Rows);
                }
            }
            ActionAfterSelectionChanged();
        }

        private void ActionAfterSelectionChanged()
        {
            var SelectedCategory = SelectFilesBox.SelectedValue.ToString();

            if (!string.IsNullOrEmpty(SelectedCategory))
            {
                var LoadedFile = WorkLoad.FindByCategory(SelectedCategory);

                if (LoadedFile != null)
                {
                    if (string.Equals(LoadedFile.FileName, "menus.txt"))
                        g_OptionsWindow.FixMenus.IsEnabled = true;
                    else
                        g_OptionsWindow.FixMenus.IsEnabled = false;
                }
            }
        }

        private void OpenModButton_Click(object sender, RoutedEventArgs e)
        {
            var FolderDialog = new OpenFolderDialog();

            //FolderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            FolderDialog.Title = "Выбрать папку с установленным модом";

            FolderDialog.Multiselect = false;

            if (FolderDialog.ShowDialog() == true)
            {
                g_FileForExport  = string.Empty;

                g_ModFolderPath  = FolderDialog.FolderName;

                if (LoadModFilesAndCategories(FolderDialog.FolderName) == -1)
                {
                    MessageBox.Show("Неверный мод.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                if (!LoadCategoryFromFileBox(0))
                {
                    MessageBox.Show("Ошибка загрузки категории.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                ModPathText.Text = FolderDialog.FolderName;

                EnableControlButtons();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoadDataGrid())
                return;

            ExportFileDialog();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoadDataGrid())
                return;

            if (g_OptionsWindow == null)
                return;

            var DialogFile = new OpenFileDialog();

            DialogFile.Title = "Импорт перевода";

            DialogFile.Filter = "CSV файлы|*.csv|Текстовые файлы|*.txt|Все файлы|*.*";

            DialogFile.FileName = GetTranslateFileNameFromFileBox();

            var SourceDirectory = g_ModFolderPath + "\\languages";

            if (Directory.Exists(SourceDirectory))
                DialogFile.InitialDirectory = SourceDirectory;
            else
                DialogFile.InitialDirectory = g_ModFolderPath;

            if (DialogFile.ShowDialog() == true)
            {
                var Result = ImportTranslate(DialogFile.FileName, (g_OptionsWindow.ImportOnlyInEmpty.IsChecked == true));

                if (Result.FailedLoad.Count > 0 && g_OptionsWindow.ImportLog.IsChecked == true)
                {
                    using (StreamWriter WriteText = new StreamWriter("failed_" + Path.GetFileName(DialogFile.FileName)))
                    {
                        foreach (var FailLine in Result.FailedLoad)
                            WriteText.WriteLine(FailLine.RowId + "|" + FailLine.TranslatedText);
                    }
                }

                var FormatedResult = string.Format("Загружено: {0}\rНет совпадений: {1}", Result.SuccessLoaded, Result.FailedLoad.Count);

                MessageBox.Show(FormatedResult, "Импорт", MessageBoxButton.OK, MessageBoxImage.Information);

                SetTranslateCountLabel();

                CatIsChanged(true);
            }
        }

        private void ModPathText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) // Ввод
            {
                e.Handled = true;

                if (!Directory.Exists(ModPathText.Text))
                {
                    MessageBox.Show("Неверный путь.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }
                else
                {
                    if (AskIfAllCatsChanged("Всё равно продолжить?", "Продолжить?", MessageBoxImage.Question) == 0)
                        return;

                    AllCatsIsChanged(false);

                    if (LoadModFilesAndCategories(ModPathText.Text) >= 0)
                    {
                        g_ModFolderPath = ModPathText.Text;

                        SelectFilesBox.IsEnabled = true;

                        EnableControlButtons();
                    }
                }
            }
        }

        private void KeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) // Ctrl + S
            {
                CatIsChanged(false);

                if (IsLoadDataGrid())
                {
                    ExportFileDialog();
                }
                e.Handled = true;
            }

            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) // Ctrl + Z
            {
                //e.Handled = true;
            }

            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) // Ctrl + F
            {
                e.Handled = true;

                g_SearchWindow.Show(); // Окно поиска.

                g_SearchWindow.Owner = this;
            }

            if (e.Key == Key.F3)
            {
                //e.Handled = true;
            }

            if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.Shift) // Shift + F3
            {
                //e.Handled = true;
            }

            if (e.Key == Key.F && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) // Ctrl + Shift + F
            {
                if (AskIfCatIsChanged("Всё равно продолжить?", "Продолжить?", MessageBoxImage.Question) == 0)
                    return;

                CatIsChanged(false);

                if (g_CompareMode)
                {
                    g_CompareMode = false;

                    if (!LoadCategoryFromFileBox(SelectFilesBox.SelectedIndex))
                    {
                        //
                    }
                }
                else
                {
                    if (ChooseModAndSeeDifference())
                    {
                        g_CompareMode = true;
                    }
                }
                e.Handled = true;
            }
        }

        private bool ChooseModAndSeeDifference()
        {
            if (!IsLoadDataGrid())
                return false;

            var FolderDialog = new OpenFolderDialog();

            FolderDialog.Title = "Выбрать папку со старой версией мода";

            FolderDialog.Multiselect = false;

            if (Directory.Exists(g_ModFolderPath))
                FolderDialog.InitialDirectory = g_ModFolderPath;

            if (FolderDialog.ShowDialog() == true)
            {
                var TargetFile = FolderDialog.FolderName + "\\" + Path.GetFileName(g_CurrentOriginalFile);

                if (!File.Exists(TargetFile))
                {
                    MessageBox.Show($"Не найден файл {TargetFile}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                    return false;
                }

                var LoadedData = ProcessOriginalFiles(TargetFile);

                if (LoadedData == null)
                {
                    MessageBox.Show($"Неверный мод {FolderDialog.FolderName}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                    return false;
                }

                var ModChanges = Parser.GetModTextChanges(GetMainRows(), LoadedData);

                if (ModChanges.Count == 0)
                {
                    MessageBox.Show($"Разницы нет", "Сравнение", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                RefreshMainGridAndSetCount(ModChanges);

                if (ModChanges.Count > 0)
                {
                    MessageBox.Show($"Изменено {ModChanges.Count} строк", "Сравнение", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return true;
            }
            return false;
        }

        private void FocusOnRow(ModTextRow RowData)
        {
            if (RowData != null)
            {
                MainDataGrid.ScrollIntoView(RowData);

                MainDataGrid.Focus();
            }
        }

        /// <summary>
        /// Прокрутит табличку к первой видимой строке.
        /// </summary>
        private void FocusFirstVisibleRow()
        {
            for (int i = 0; i < MainDataGrid.Items.Count; i++)
            {
                var Row = MainDataGrid.Items[i] as ModTextRow;

                if (Row != null && IsVisibleRow(Row))
                {
                    FocusOnRow(Row);

                    return;
                }
            }
        }

        /// <summary> 
        /// Исправить: Если будет SelectionUnit=FullRow то программа крашнет 
        /// </summary>
        public void FocusCell(DataGrid Data, int RowIndex, int ColumnIndex)
        {
            if (RowIndex < 0 || ColumnIndex < 0 || Data == null)
                return;

            if (Data.SelectionUnit == DataGridSelectionUnit.Cell)
            {
                var Item = Data.Items[RowIndex] as ModTextRow;

                if (Item != null)
                {
                    var Column = Data.Columns[ColumnIndex];

                    Data.ScrollIntoView(Item, Column); // Прокручиваем к строке.

                    Data.CurrentCell = new DataGridCellInfo(Item, Column);

                    Data.SelectedCells.Clear();

                    Data.SelectedCells.Add(new DataGridCellInfo(Item, Column));

                    Data.Focus();

                    if (!Data.IsReadOnly)
                    {
                        //Data.BeginEdit();
                    }
                }
            }
        }

        /// <summary>
        /// Фокус на ближайшую+1 пустую ячейку
        /// </summary>
        /// <param name="TableGrid"></param>
        /// <param name="ColumnName"></param>
        /// <param name="Cycle"></param>
        /// <returns></returns>
        private int NextFocusCell(DataGrid TableGrid, string ColumnName, bool Cycle = false)
        {
            if (!IsLoadDataGrid())
                return -1;

            int ColumnIndex = GetColumnIndexByName(TableGrid, ColumnName);

            if (ColumnIndex == -1)
                return -1;

            g_CurrentSelectedCell.ColumnIndex = ColumnIndex;

            if (g_CurrentSelectedCell.RowIndex == TableGrid.Items.Count) // Если мы на полследней строке, то
                g_CurrentSelectedCell.RowIndex = -1; // начинаем сначала

            int NextIndex = g_CurrentSelectedCell.RowIndex + 1; // Индекс следующей строки от текущей выделенной.

            int CountCells = TableGrid.Items.Count; // Всего строк.

            while (NextIndex < CountCells) // Пока индекс текущей строки меньше чем все.
            {
                var ModText = TableGrid.Items[NextIndex] as ModTextRow;

                if (ModText == null)
                    return -1;

                if (IsVisibleRow(ModText)) // Только видимые строки
                {
                    if (ModText.TranslatedText == string.Empty)
                    {
                        FocusCell(TableGrid, NextIndex, ColumnIndex);

                        return 1;
                    }
                }
                NextIndex++;
            }

            if (Cycle)
                g_CurrentSelectedCell.RowIndex = -1;

            return 0;
        }

        private int PrevFocusCell(DataGrid TableGrid, string ColumnName, bool Cycle = false)
        {
            if (!IsLoadDataGrid())
                return -1;

            var ColumnIndex = GetColumnIndexByName(TableGrid, ColumnName);

            if (ColumnIndex == -1)
                return -1;

            g_CurrentSelectedCell.ColumnIndex = ColumnIndex;

            int PrevIndex = g_CurrentSelectedCell.RowIndex;

            if (g_CurrentSelectedCell.RowIndex >= 0)
                PrevIndex--;
            else
                PrevIndex = 0;

            while (PrevIndex >= 0)
            {
                var ModText = TableGrid.Items[PrevIndex] as ModTextRow;

                if (ModText == null)
                    return -1;

                if (IsVisibleRow(ModText))
                {
                    if (ModText.TranslatedText == string.Empty)
                    {
                        FocusCell(TableGrid, PrevIndex, ColumnIndex);

                        return 1;
                    }
                }
                PrevIndex--;
            }

            if (Cycle)
                g_CurrentSelectedCell.RowIndex = TableGrid.Items.Count;

            return 0;
        }

        private void NextCellButton_Click(object sender, RoutedEventArgs e)
        {
            if (NextFocusCell(MainDataGrid, "Перевод", true) == 0)
                MessageBox.Show("Ничего не найдено", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PrevCellButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrevFocusCell(MainDataGrid, "Перевод", true) == 0)
                MessageBox.Show("Ничего не найдено", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SettingsMenu_Click(object sender, RoutedEventArgs e)
        {
            g_OptionsWindow.Owner = null;

            g_OptionsWindow.Show();

            g_OptionsWindow.Owner = this;
        }

        private void SearchMenu_Click(object sender, RoutedEventArgs e)
        {
            g_SearchWindow.Owner = null;

            g_SearchWindow.Show();

            g_SearchWindow.Owner = this;
        }

        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            var Window = new AboutWindow();

            Window.Show();

            Window.Owner = this;
        }

        private static List<string> GetAllColumnText(DataGrid TableData, DataGridColumn Column, bool OnlyVisible = true)
        {
            var Result = new List<string>();

            if (TableData == null || Column == null)
                return Result;

            var ColumnIndex = TableData.Columns.IndexOf(Column);

            if (ColumnIndex == -1)
                return Result;

            for (int RowIndex = 0; RowIndex < TableData.Items.Count; RowIndex++)
            {
                var CellValue = GetCellStringValue(TableData, RowIndex, ColumnIndex, OnlyVisible);

                if (CellValue != null)
                {
                    Result.Add(CellValue);
                }
            }
            return Result;
        }

        private bool SaveColumnDataDialog(DataGrid TableGrid, DataGridColumn Column, bool ReWrite = false)
        {
            var FileDialog = new SaveFileDialog();

            FileDialog.Title = $"Сохранить столбец {Column.Header} в";

            FileDialog.FileName = $"{Column.Header}_text.txt";

            FileDialog.DefaultExt = "txt";

            FileDialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";

            if (FileDialog.ShowDialog() == true)
            {
                if (FileDialog.FileName != null)
                {
                    var Result = GetAllColumnText(TableGrid, Column, true);

                    File.WriteAllLines(FileDialog.FileName, Result, Encoding.UTF8);

                    if (Result.Count == GetVisibleCount())
                        return true;
                }
            }
            return false;
        }

        private bool ImportColumnDataDialog(DataGrid TableGrid, DataGridColumn Column)
        {
            var FileDialog = new OpenFileDialog();

            FileDialog.Title = $"Загрузить данные в столбец {Column.Header}";

            FileDialog.FileName = $"{Column.Header}_text.txt";

            FileDialog.DefaultExt = "txt";

            FileDialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";

            if (FileDialog.ShowDialog() == true)
            {
                if (FileDialog.FileName != null)
                {
                    var AllLines = File.ReadAllLines(FileDialog.FileName, Encoding.UTF8);

                    if (AllLines.Length > GetVisibleCount()) // Загружаемых данных не должно быть больше, чем видно в таблице
                    {
                        MessageBox.Show($"Файл {FileDialog.FileName} содержит больше полей, чем есть в таблице.", "Ошибка загрузки", MessageBoxButton.OK, MessageBoxImage.Error);

                        return false;
                    }

                    if (AllLines.Length > 0)
                    {
                        int ColumnIndex = TableGrid.Columns.IndexOf(Column);

                        if (ColumnIndex >= 0)
                        {
                            int LineIndex = 0, ItemCount = 0;

                            while ((ItemCount < TableGrid.Items.Count) && (LineIndex < AllLines.Length)) //
                            {
                                var RowData = GetRowDataByIndex(ItemCount);

                                if (RowData != null && IsVisibleRow(RowData))
                                {
                                    SetCellValue(TableGrid, ItemCount, ColumnIndex, AllLines[LineIndex]);

                                    LineIndex++;
                                }
                                ItemCount++;
                            }

                            TableGrid.Items.Refresh(); // Обновляем табличку.

                            SetTranslateCountLabel();

                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Ищем столбец по PlacementTarget контестного меню.
        /// </summary>
        /// <param name="Sender"></param>
        /// <returns></returns>
        private static DataGridColumn? GetColumnByMenuContext(object Sender)
        {
            if (Sender is MenuItem Menu)
            {
                ContextMenu? MenuContext = Menu.Parent as ContextMenu;

                if (MenuContext != null)
                {
                    bool isBoundColumn = MenuContext.PlacementTarget is DataGrid;

                    if (MenuContext.PlacementTarget is DataGrid Grid)
                    {
                        DataGridColumn Column = Grid.CurrentColumn;

                        return Column;
                    }
                }
            }
            return null;
        }

        private void DataGridMenuSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var SelectedColumn = GetColumnByMenuContext(sender);

            if (SelectedColumn != null)
            {
                SaveColumnDataDialog(MainDataGrid, SelectedColumn);
            }
        }

        private void DataGridMenuImport_Click(object sender, RoutedEventArgs e)
        {
            var SelectedColumn = GetColumnByMenuContext(sender);

            if (SelectedColumn != null)
            {
                ImportColumnDataDialog(MainDataGrid, SelectedColumn);
            }
        }

        private void DataGridContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (!IsLoadDataGrid()) // Не показываем меню, если не знагружена таблица.
            {
                e.Handled = true;

                return;
            }

            if (sender is DataGrid TableGrid)
            {
                var CurrentColumn = TableGrid.CurrentColumn;

                var ColumnIndex = CurrentColumn.DisplayIndex;

                if (ColumnIndex == 0)
                    e.Handled = true;
            }
        }
        
        private void MainDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            var Column = e.Column;

            if (Column.SortDirection == null)
                Column.SortDirection = ListSortDirection.Ascending;
        }

        private void MainDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var RowData = e.Row;

            var ModText = RowData.Item as ModTextRow;

            if (ModText != null)
            {
                if (!IsVisibleRow(ModText)) // Если строка помечена.
                {
                    RowData.Visibility = Visibility.Collapsed;  // скрываем ее
                }
                else
                {
                    RowData.Visibility = Visibility.Visible;

                    //DataGridRow Row = (DataGridRow)MainDataGrid.ItemContainerGenerator.ContainerFromItem(ModText);

                    RowData.ToolTip = GetRowToolTipText(ModText);

                    if (g_OptionsWindow != null && ModText.NPC != null)
                    {
                        var ShowNpc = g_OptionsWindow.ShowNPC.IsChecked ?? true; // По умолчанию включено.

                        if (ModText.NPC.IsWoman)
                        {
                            if (ShowNpc)
                                RowData.Background = Settings.FemalesColor;
                        }
                        else if (ModText.NPC.IsOther)
                        {
                            if (ShowNpc)
                                RowData.Background = Settings.UnknownsColor;
                        }
                        else 
                            RowData.Background = Settings.DefaultRowColor;
                    }
                }
            }
        }

        private string? GetRowToolTipText(ModTextRow? Mod)
        {
            StringBuilder Text = new StringBuilder();

            if (Mod != null)
            {
                // Войска
                if (Mod.NPC != null)
                {
                    var Npc = Mod.NPC;

                    if (Npc.IsOther)
                        Text.AppendLine("Неизвестно");
                    else
                        Text.AppendLine("Человек");

                    if (!Npc.IsOther)
                    {
                        if (Npc.IsWoman)
                            Text.AppendLine("Женщина");
                        else
                            Text.AppendLine("Мужчина");
                    }

                    if (Npc.IsHero)
                        Text.AppendLine("Герой");

                    if (Npc.IsMerchant)
                        Text.AppendLine("Торговец");

                    if (Text.Length > 0)
                        return Text.ToString().Trim();
                }
                // Диалоги
                else if (Mod.Dialogue != null)
                {
                    var Dialogue = Mod.Dialogue;

                    if (Dialogue.IsPlayer && Dialogue.IsAnyone)
                        Text.AppendLine("Игрок говорит с NPC");
                    else if (Dialogue.IsAnyone)
                        Text.AppendLine("NPC говорит с игроком");

                    if (Text.Length > 0)
                        return Text.ToString().Trim();
                }
            }
            return null;
        }


    }
}