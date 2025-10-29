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
using EncodingTextFile;
using Microsoft.Win32;
using ModTranslatorSettings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
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
using static System.Net.Mime.MediaTypeNames;

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
            /// Цвет подсветки женщин-нпс
            /// </summary>
            public static SolidColorBrush FemalesColor = new SolidColorBrush(Color.FromArgb(150, 255, 182, 193)); // Светлорозовый

            /// <summary>
            /// Цвет для нпс-мужиков
            /// </summary>
            public static SolidColorBrush MansColor = new SolidColorBrush(Color.FromArgb(145, 255, 240, 225)); // Песочный

            /// <summary>
            /// Цвет для неизвестных.
            /// </summary>
            public static SolidColorBrush UnknownsColor = Brushes.LightGray;

            /// <summary>
            /// Цвет для подсветки групп
            /// </summary>
            public static SolidColorBrush GroupsColor = Brushes.Azure; // Стоит ли подсвечивать группы вообще?
        }

        public class WorkLoad
        {
            public class CatInfo
            {
                public CatInfo Clone()
                {
                    return new CatInfo
                    {
                        FileName     = FileName,
                        FullFileName = FullFileName,
                        ExportName   = ExportName,
                        Category     = Category,
                        IsChanged    = IsChanged,
                        Rows         = Rows?.ToList()
                    };
                }

                public void CopyFrom(CatInfo Data)
                {
                    this.Category     = Data.Category;
                    this.FileName     = Data.FileName;
                    this.FullFileName = Data.FullFileName;
                    this.ExportName   = Data.ExportName;
                    this.IsChanged    = Data.IsChanged;
                    this.Rows         = Data.Rows;
                }

                public string FileName        { get; set; } = string.Empty;
                public string FullFileName    { get; set; } = string.Empty;
                public string ExportName      { get; set; } = string.Empty;
                public string Category        { get; set; } = string.Empty;
                public bool IsChanged         { get; set; } = false;
                public List<ModTextRow>? Rows { get; set; } = null;
            }

            /// <summary>
            /// Все данные для загрузки и экпорта файлов мода.
            /// </summary>
            private static readonly List<CatInfo> BindingsList = new List<CatInfo>
            {
                new CatInfo { FileName = "conversation.txt",    ExportName = "dialogs.csv",         Category = "Диалоги" },
                new CatInfo { FileName = "factions.txt",        ExportName = "factions.csv",        Category = "Фракции" },
                new CatInfo { FileName = "info_pages.txt",      ExportName = "info_pages.csv",      Category = "Страницы информации" },
                new CatInfo { FileName = "item_kinds1.txt",     ExportName = "item_kinds.csv",      Category = "Виды предметов" },
                new CatInfo { FileName = "item_modifiers.txt",  ExportName = "item_modifiers.csv",  Category = "Состояние предметов" },
                new CatInfo { FileName = "menus.txt",           ExportName = "game_menus.csv",      Category = "Игровое меню" },
                new CatInfo { FileName = "parties.txt",         ExportName = "parties.csv" ,        Category = "Локации" },
                new CatInfo { FileName = "party_templates.txt", ExportName = "party_templates.csv", Category = "Группы" },
                new CatInfo { FileName = "quests.txt",          ExportName = "quests.csv",          Category = "Задания" },
                new CatInfo { FileName = "quick_strings.txt",   ExportName = "quick_strings.csv",   Category = "Быстрые строки" },
                new CatInfo { FileName = "skills.txt",          ExportName = "skills.csv",          Category = "Навыки" },
                new CatInfo { FileName = "skins.txt",           ExportName = "skins.csv" ,          Category = "Скины" },
                new CatInfo { FileName = "strings.txt",         ExportName = "game_strings.csv",    Category = "Игровые строки" },
                new CatInfo { FileName = "troops.txt",          ExportName = "troops.csv",          Category = "Персонажи" }
            };

            public static CatInfo? FindByCategory(string CategoryName, StringComparison Compare = StringComparison.OrdinalIgnoreCase)
            {
                return BindingsList.FirstOrDefault(x => (x != null) && string.Equals(x.Category, CategoryName, Compare), null);
            }

            public static CatInfo? FindByFile(string FileName, StringComparison Compare = StringComparison.OrdinalIgnoreCase)
            {
                return BindingsList.FirstOrDefault(x => (x != null) && string.Equals(x.Category, FileName, Compare), null);
            }

            /// <summary>
            /// пример: dlga_ , trp_
            /// </summary>
            public static CatInfo? FindByPrefix(string Prefix)
            {
                return BindingsList.FirstOrDefault(bind => bind.Rows != null && bind.Rows.Any(row => row.RowId.StartsWith(Prefix)));
            }

            public static List<string> GetChangedCategories()
            {
                return BindingsList.Where(x => x.IsChanged).Select(x => x.Category).ToList();
            }

            public static List<CatInfo> GetChangedModInfo()
            {
                return BindingsList.Where(x => x.IsChanged).ToList();
            }

            public static List<CatInfo> GetBindings() 
            {
                return BindingsList; 
            }

        }

        private class LoadedResult
        {
            public int LoadedRows = 0;
            public int LoadedCats = 0;
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
        //public bool g_CompareMode = false;

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

            this.g_OptionsWindow = new SettingsWindow(this);

            this.g_SearchWindow = new SearchWindow(this);

            this.g_OptionsWindow.CompMode.IsEnabled = false;
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
            this.g_OptionsWindow.CloseWindow();

            this.g_SearchWindow.CloseWindow();
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
            var ModInfo = GetCurrentBinding();

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
            {
                foreach (var Item in List)
                    Item.IsChanged = Bool;
            }
        }

        ///<summary>Установить состояние только для текущей категории</summary>
        public void CatIsChanged(bool Bool)
        {
            var CurrentMod = GetCurrentBinding();

            if (CurrentMod != null)
                CurrentMod.IsChanged = Bool;
        }

        private void EnableControlElements()
        {
            this.SelectFilesBox.IsEnabled = true;

            this.ImportButton.IsEnabled   = true;

            this.PrevCellButton.IsEnabled = true;

            this.NextCellButton.IsEnabled = true;

            this.SaveButton.IsEnabled     = true;

            this.g_OptionsWindow.CompMode.IsEnabled = true;
        }

        public bool IsLoadDataGrid()
        {
            return (this.MainDataGrid.Items.Count > 0 && File.Exists(g_CurrentOriginalFile));
        }

        /// <summary>
        /// Возвращает биндинг с таблицей. Id, текст, перевод и т.д
        /// </summary>
        private List<ModTextRow> GetMainRows()
        {
            var Bindings = this.MainDataGrid.ItemsSource as List<ModTextRow>;

            if (Bindings != null)
                return Bindings;

            return new List<ModTextRow>();
        }

        /// <summary>
        /// Fixme: ищет по ссылке. Если сменить на другую, то вернет null
        /// Возвращает структуру связанную с текущей в таблице.
        /// </summary>
        public WorkLoad.CatInfo? GetCurrentBinding()
        {
            var Bindings = this.MainDataGrid.ItemsSource as List<ModTextRow>;

            if (Bindings != null)
                return WorkLoad.GetBindings().Find(x => (x.Rows == Bindings));

            return null;
        }

        private static void SetBindFullPath(string FolderPath)
        {
            WorkLoad.GetBindings().ForEach(Bind =>
                Bind.FullFileName = Path.Combine(FolderPath, Bind.FileName));
        }

        /// <summary>
        /// Очищает таблицу от данных. (но не очищает данные в биндингах)
        /// </summary>
        public void UnloadMainGrid()
        {
            g_CurrentSelectedCell = (-1, -1);

            this.MainDataGrid.ItemsSource = null;

            this.MainDataGrid.Items.Refresh();
        }

        private void RefreshMainGrid(List<ModTextRow> Rows)
        {
            UpdateVisibleRowsNumbers(Rows);

            this.g_CurrentSelectedCell = (-1, -1);

            this.MainDataGrid.ItemsSource = null;

            this.MainDataGrid.ItemsSource = Rows;

            this.MainDataGrid.Items.Refresh();

            FocusFirstVisibleRow();
        }

        /// <summary>
        /// Обновляет таблицу, подсчитывает видимые строки.
        /// </summary>
        public void RefreshMainGrid()
        {
            var Binding = GetCurrentBinding();

            if (Binding == null || Binding.Rows == null)
                RefreshMainGrid(new List<ModTextRow>());
            else
                RefreshMainGrid(Binding.Rows);
        }

        private void RefreshMainGridAndSetCount(List<ModTextRow> Rows)
        {
            RefreshMainGrid(Rows);

            SetTranslateCountLabel();
        }

        /// <summary>
        /// Обновляет таблицу, подсчитывает видимое, и устанавливает заговок с количеством переведённых строк.
        /// </summary>
        public void RefreshMainGridAndSetCount()
        {
            var Binding = GetCurrentBinding();

            if (Binding == null || Binding.Rows == null)
                RefreshMainGridAndSetCount(new List<ModTextRow>());
            else
                RefreshMainGridAndSetCount(Binding.Rows);
        }

        /// <summary>
        /// Функция для проверки видимости строки.
        /// </summary>
        /// <param name="TextData"></param>
        /// <returns>Вернёт true, если строка будет видна в таблице.</returns>
        private static bool IsVisibleRow(ModTextRow TextData)
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
        private static void UpdateVisibleRowsNumbers(List<ModTextRow> TextMod)
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

        private static List<string> ParseCsvLine(string TextLine, char Separator)
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

        private static char? DetectCsvLineSeparator(string TextLine, char[] CandidateSeparators)
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

        private static char? DetectCsvFileSeparator(string FilePath, char[] CandidateSeparators, int SampleLines)
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

        private static List<ModTextRow> ReadModTextCsvFile(string FilePath, char[] Separators, int MaxArgs = 3)
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

        private static List<ModTextRow>? ProcessOriginalFiles(string FilePath)
        {
            if (!File.Exists(FilePath))
                return null;

            List<ModTextRow>? Result = null;

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
            if (this.MainDataGrid.Items.Count > Index && Index >= 0)
            {
                object Data = this.MainDataGrid.Items[Index];

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
        private async Task<TextImportInfo> ImportTranslate(string FilePath, bool LoadInEmpty = false)
        {
            var Result = new TextImportInfo { SuccessLoaded = 0 };

            var MainRows = GetMainRows();

            await Task.Run(() =>
            {
                var TranslateData = ReadModTextCsvFile(FilePath, Settings.CsvSeparators, 3);

                if (TranslateData.Count >= 1)
                {
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
                }
            });
            RefreshMainGrid(MainRows);

            return Result;
        }

        private long HowManyTranslatedLines()
        {
            long TranslatedCount = 0;

            foreach (var Item in this.MainDataGrid.Items)
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
            return GetVisibleCount(this.MainDataGrid);
        }

        public void SetTranslateCountLabel(long TranslateLines)
        {
            var Hiden = this.MainDataGrid.Items.Count - GetVisibleCount();

            //var NeedToTranslate = MainDataGrid.Items.Count - TranslateLines;

            this.TranslateCount.Content = string.Format($"Загружено: {this.MainDataGrid.Items.Count} | Скрыто: {Hiden} | Переведено: {TranslateLines}");
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
            var SelectedCategory = this.SelectFilesBox.SelectedValue.ToString();

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
                CsvName = Path.GetFileName(this.g_FileForExport);

            if (this.g_OptionsWindow.CompMode.IsChecked ?? false) // По умолчанию выкл.
                FileDialog.FileName = "new_" + CsvName;
            else
                FileDialog.FileName = CsvName;

            FileDialog.OverwritePrompt = true; // Спросить о перезаписи файла.

            var SourceDirectory = this.g_ModFolderPath + "\\languages";

            if (Directory.Exists(SourceDirectory))
                FileDialog.InitialDirectory = SourceDirectory;
            else
                FileDialog.InitialDirectory = this.g_ModFolderPath;

            if (FileDialog.ShowDialog() == true)
            {
                var EmptyExport = this.g_OptionsWindow.EmptyExport.IsChecked;

                if (EmptyExport == null)
                    EmptyExport = false;

                ExportModTextToFile(FileDialog.FileName, (bool)EmptyExport);

                CatIsChanged(false);

                this.g_FileForExport = FileDialog.FileName;
            }
        }

        private void DataGridSelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            foreach (DataGridCellInfo CellInfo in e.AddedCells)
            {
                var SelectedItem = CellInfo.Item;

                int RowIndex = this.MainDataGrid.Items.IndexOf(SelectedItem);

                if (RowIndex != -1)
                {
                    this.g_CurrentSelectedCell.RowIndex = RowIndex;

                    this.g_CurrentSelectedCell.ColumnIndex = CellInfo.Column.DisplayIndex;

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
                this.g_CurrentCellValue = GetCellStringValue(sender);
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

                    if (NewText != this.g_CurrentCellValue) // Если ячейка была изменена
                    {
                        CatIsChanged(true);

                        if (NewText == string.Empty) // и стала пустой, то
                        {
                            if (TranslatedLines > 0)
                                TranslatedLines--; // уменьшаем счётчик.
                        }

                        if (this.g_CurrentCellValue == string.Empty) // Если ячейка была изменена и при этом она была пустой, то
                            TranslatedLines++; // увеличиваем счётчик.
                    }
                    SetTranslateCountLabel(TranslatedLines);

                    this.g_CurrentCellValue = string.Empty;
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
            return GetColumnByName(this.MainDataGrid, Name);
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
            var Indexes = FindCellByString(this.MainDataGrid, Value, StartRow, FullSearch, OnlyVisible, CaseType);

            if (Indexes.ColumnIndex >= 0 && Indexes.RowIndex >= 0)
            {
                var Item = this.MainDataGrid.Items.GetItemAt(Indexes.RowIndex);

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
        /// Главная функция для загрузки мода
        /// </summary>
        /// <param name="ModFolderPath"></param>
        /// <returns></returns>
        private async Task<LoadedResult> LoadModFilesAndCategories(string ModFolderPath, bool CompareMode = false)
        {
            var Result = new LoadedResult();

            string FullPath = "nofile";

            try
            {
                if (!Directory.Exists(ModFolderPath))
                    return new LoadedResult();

                var Categories = new List<string> { };

                foreach (var ModFile in WorkLoad.GetBindings())
                {
                    FullPath = Path.Combine(ModFolderPath, ModFile.FileName);

                    if (File.Exists(FullPath))
                    {
                        var LoadedFile = await Task.Run(() => ProcessOriginalFiles(FullPath));

                        if (LoadedFile == null)
                        {
                            var Ask = MessageBox.Show($"При загрузке {FullPath} произшла ошибка. Всё равно продолжить загрузку?", "Ошибка загрузки", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                            if (Ask == MessageBoxResult.Yes)
                                continue;
                            else
                                return new LoadedResult();
                        }

                        if (CompareMode)
                            LoadedFile = Parser.GetModTextChanges(LoadedFile, ModFile.Rows); // Исправить код надо. Плохо работает с дубликатами.

                        if (LoadedFile.Count > 0)
                        {
                            Result.LoadedCats++;

                            ModFile.Rows = LoadedFile;

                            Result.LoadedRows += LoadedFile.Count;

                            ModFile.FullFileName = FullPath;

                            Categories.Add(ModFile.Category);
                        }
                    }
                }
                if (Result.LoadedCats > 0)
                {
                    AllCatsIsChanged(false);

                    if (!CompareMode)
                        PrepareCategoriesForTable(); // При сравнении некоректно будет работать.

                    this.SelectFilesBox.ItemsSource = Categories;

                    this.SelectFilesBox.Items.Refresh();

                    this.SelectFilesBox.SelectedIndex = 0;
                }
            }
            catch (Exception)
            {
                MessageBox.Show(
                    $"При обработке: {FullPath} произошла критическая ошибка.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop);

                return new LoadedResult();
            }
            return Result;
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

        /// <summary>
        /// Отображение данных из filebox. (база 0)
        /// </summary>
        public bool LoadCategoryFromFileBox(int CategoryIndex)
        {
            if (CategoryIndex <= this.SelectFilesBox.Items.Count)
            {
                var SelectedCategory = this.SelectFilesBox.Items[CategoryIndex].ToString();

                if (!string.IsNullOrEmpty(SelectedCategory))
                {
                    var LoadedFile = WorkLoad.FindByCategory(SelectedCategory);

                    if (LoadedFile == null || LoadedFile.Rows == null)
                    {
                        MessageBox.Show($"Не найдена категория {SelectedCategory}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                        return false;
                    }
                    if (this.SelectFilesBox.SelectedIndex != CategoryIndex)
                        this.SelectFilesBox.SelectedIndex = CategoryIndex; // Вызовет SelectFilesBox_SelectionChanged
                    else
                        RefreshMainGridAndSetCount(LoadedFile.Rows);

                    return true;
                }
            }
            return false;
        }

        private void SelectFilesBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var SelectedCategory = this.SelectFilesBox.SelectedValue?.ToString();

            if (string.IsNullOrEmpty(SelectedCategory))
                return;

            var LoadedFile = WorkLoad.FindByCategory(SelectedCategory);

            if (LoadedFile != null)
            {
                this.g_FileForExport = Path.Combine(this.g_ModFolderPath + "\\languages\\" + LoadedFile.ExportName);

                if (LoadedFile.Rows != null)
                {
                    this.g_CurrentOriginalFile = Path.Combine(this.g_ModFolderPath, LoadedFile.FileName);

                    RefreshMainGridAndSetCount(LoadedFile.Rows);
                }
            }
            ActionAfterSelectionChanged();
        }

        private void ActionAfterSelectionChanged()
        {
            if (this.g_OptionsWindow.CompMode.IsChecked != true)
            {
                var SelectedCategory = this.SelectFilesBox.SelectedValue.ToString();

                if (!string.IsNullOrEmpty(SelectedCategory))
                {
                    var LoadedFile = WorkLoad.FindByCategory(SelectedCategory);

                    if (LoadedFile != null)
                    {
                        if (string.Equals(LoadedFile.FileName, "menus.txt"))
                            this.g_OptionsWindow.FixMenus.IsEnabled = true;
                        else
                            this.g_OptionsWindow.FixMenus.IsEnabled = false;
                    }
                }
            }
        }

        private async void OpenModButton_Click(object sender, RoutedEventArgs e)
        {
            this.g_OptionsWindow.CompMode.IsChecked = false;

            this.g_OptionsWindow.g_CompareMode = false;

            var FolderDialog = new OpenFolderDialog();

            //FolderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            FolderDialog.Title = "Выбрать папку с установленным модом";

            FolderDialog.Multiselect = false;

            if (FolderDialog.ShowDialog() == true)
            {
                this.g_FileForExport  = string.Empty;

                this.g_ModFolderPath  = FolderDialog.FolderName;

                if ((await LoadModFilesAndCategories(FolderDialog.FolderName)).LoadedCats == 0)
                {
                    MessageBox.Show("Неверный мод.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                if (!LoadCategoryFromFileBox(0))
                {
                    MessageBox.Show("Ошибка загрузки категории.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                this.ModPathText.Text = FolderDialog.FolderName;

                EnableControlElements();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoadDataGrid())
                return;

            ExportFileDialog();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoadDataGrid())
                return;

            if (this.g_OptionsWindow == null)
                return;

            var DialogFile = new OpenFileDialog();

            DialogFile.Title = "Импорт перевода";

            DialogFile.Filter = "CSV файлы|*.csv|Текстовые файлы|*.txt|Все файлы|*.*";

            DialogFile.FileName = GetTranslateFileNameFromFileBox();

            var SourceDirectory = this.g_ModFolderPath + "\\languages";

            if (Directory.Exists(SourceDirectory))
                DialogFile.InitialDirectory = SourceDirectory;
            else
                DialogFile.InitialDirectory = this.g_ModFolderPath;

            if (DialogFile.ShowDialog() == true)
            {
                var Result = await ImportTranslate(DialogFile.FileName, (this.g_OptionsWindow.ImportOnlyInEmpty.IsChecked == true));

                if (Result.FailedLoad.Count > 0 && this.g_OptionsWindow.ImportLog.IsChecked == true)
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

        private async void ModPathText_KeyDown(object sender, KeyEventArgs e)
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

                    if ((await LoadModFilesAndCategories(ModPathText.Text)).LoadedCats > 0)
                    {
                        this.g_ModFolderPath = ModPathText.Text;

                        this.SelectFilesBox.IsEnabled = true;

                        EnableControlElements();
                    }
                }
            }
        }

        private void KeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) // Ctrl + S
            {
                if (IsLoadDataGrid())
                {
                    ExportFileDialog();

                    //CatIsChanged(false);
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
                //e.Handled = true;
            }
        }

        public async Task<bool> ChooseOldModAndSeeDifference()
        {
            if (IsLoadDataGrid())
            {
                var FolderDialog = new OpenFolderDialog();

                FolderDialog.Title = "Выбрать папку со старой версией мода";

                FolderDialog.Multiselect = false;

                if (Directory.Exists(this.g_ModFolderPath))
                    FolderDialog.InitialDirectory = this.g_ModFolderPath;

                if (FolderDialog.ShowDialog() == true)
                {
                    var LoadResult = await LoadModFilesAndCategories(FolderDialog.FolderName, true);

                    if (LoadResult.LoadedCats == 0)
                    {
                        MessageBox.Show($"Неверный мод {FolderDialog.FolderName}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                        return false;
                    }

                    MessageBox.Show($"Всего было изменено или добавлено: {LoadResult.LoadedRows} строк в {LoadResult.LoadedCats} категориях", "Сравнение", MessageBoxButton.OK, MessageBoxImage.Information);

                    return true;
                }
            }
            return false;
        }

        private void FocusOnRow(ModTextRow RowData)
        {
            if (RowData != null)
            {
                this.MainDataGrid.ScrollIntoView(RowData);

                this.MainDataGrid.Focus();
            }
        }

        /// <summary>
        /// Прокрутит табличку к первой видимой строке.
        /// </summary>
        private void FocusFirstVisibleRow()
        {
            for (int i = 0; i < this.MainDataGrid.Items.Count; i++)
            {
                var Row = this.MainDataGrid.Items[i] as ModTextRow;

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

            this.g_CurrentSelectedCell.ColumnIndex = ColumnIndex;

            if (this.g_CurrentSelectedCell.RowIndex == TableGrid.Items.Count) // Если мы на полследней строке, то
                this.g_CurrentSelectedCell.RowIndex = -1; // начинаем сначала

            int NextIndex = this.g_CurrentSelectedCell.RowIndex + 1; // Индекс следующей строки от текущей выделенной.

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
                this.g_CurrentSelectedCell.RowIndex = -1;

            return 0;
        }

        private int PrevFocusCell(DataGrid TableGrid, string ColumnName, bool Cycle = false)
        {
            if (!IsLoadDataGrid())
                return -1;

            var ColumnIndex = GetColumnIndexByName(TableGrid, ColumnName);

            if (ColumnIndex == -1)
                return -1;

            this.g_CurrentSelectedCell.ColumnIndex = ColumnIndex;

            int PrevIndex = this.g_CurrentSelectedCell.RowIndex;

            if (this.g_CurrentSelectedCell.RowIndex >= 0)
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
            if (NextFocusCell(this.MainDataGrid, "Перевод", true) == 0)
                MessageBox.Show("Ничего не найдено", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PrevCellButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrevFocusCell(this.MainDataGrid, "Перевод", true) == 0)
                MessageBox.Show("Ничего не найдено", "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SettingsMenu_Click(object sender, RoutedEventArgs e)
        {
            this.g_OptionsWindow.Owner = null;

            this.g_OptionsWindow.Show();

            this.g_OptionsWindow.Owner = this;
        }

        private void SearchMenu_Click(object sender, RoutedEventArgs e)
        {
            this.g_SearchWindow.Owner = null;

            this.g_SearchWindow.Show();

            this.g_SearchWindow.Owner = this;
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
                SaveColumnDataDialog(this.MainDataGrid, SelectedColumn);
            }
        }

        private void DataGridMenuImport_Click(object sender, RoutedEventArgs e)
        {
            var SelectedColumn = GetColumnByMenuContext(sender);

            if (SelectedColumn != null)
            {
                ImportColumnDataDialog(this.MainDataGrid, SelectedColumn);
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

                    RowData.ToolTip = GetRowToolTipText(ModText);

                    if (this.g_OptionsWindow != null)
                    {
                        var ShowNpc = this.g_OptionsWindow.ShowNPC.IsChecked ?? true; // По умолчанию включено.

                        if (ShowNpc)
                        {
                            if (ModText.NPC != null)
                            {
                                var NPC = ModText.NPC;

                                if (NPC.IsWoman)
                                {
                                   RowData.Background = Settings.FemalesColor;
                                }
                                else if (NPC.IsMan)
                                {
                                    RowData.Background = Settings.MansColor;
                                }
                                else if (NPC.IsOther)
                                {
                                   RowData.Background = Settings.UnknownsColor;
                                }
                                else
                                    RowData.Background = Settings.DefaultRowColor;
                            }

                            if (ModText.Dialogue != null)
                            {
                                var Talking = ModText.Dialogue;

                                var TalkingWith = ModText.Dialogue.TalkingWith;

                                // Если нпс-женщина говорит с игроком или игрок обращается к женщине-нпс 
                                if ((Talking.IsNpc || Talking.IsPlayer) && TalkingWith.IsWoman)
                                {
                                    RowData.Background = Settings.FemalesColor;
                                }
                                // Если нпс-мужик говорит с игроком или игрок обращается к мужику 
                                else if ((Talking.IsNpc || Talking.IsPlayer) && TalkingWith.IsMan)
                                {
                                    RowData.Background = Settings.MansColor;
                                }
                                // Если непонятно какой нпс говорит с игроком или игрок обращается к непонято кому)
                                else if ((Talking.IsNpc || Talking.IsPlayer) && TalkingWith.IsOther)
                                {
                                    RowData.Background = Settings.UnknownsColor;
                                }
                                // Если группа
                                else if (Talking.IsParty)
                                {
                                    RowData.Background = Settings.GroupsColor;
                                }
                                else
                                    RowData.Background = Settings.DefaultRowColor;
                            }
                        }
                    }
                }
            }
        }

        private string GetNpcSex(NpcType? Npc)
        {
            if (Npc != null)
            {
                if (Npc.IsOther)
                    return "Неизвестно";

                if (Npc.IsWoman)
                    return "Женщина";
                else if (Npc.IsMan)
                    return "Мужчина";
            }
            return "Неизвестно";
        }

        private string? GetRowToolTipText(ModTextRow? Row)
        {
            StringBuilder Text = new StringBuilder();

            if (Row != null)
            {
                // Персонажи
                if (Row.NPC != null)
                {
                    var WhoIsNpc = Row.NPC;

                    Text.AppendLine(GetNpcSex(WhoIsNpc));

                    if (WhoIsNpc.IsHero)
                        Text.AppendLine("Герой");

                    if (WhoIsNpc.IsMerchant)
                        Text.AppendLine("Торговец");

                    if (Text.Length > 0)
                        return Text.ToString().Trim();
                }
                // Диалоги
                if (Row.Dialogue != null)
                {
                    var Talking = Row.Dialogue;

                    if (Talking.IsPlayer && Talking.IsAnyone)
                    {
                        Text.Append($"Кто: Игрок\nКому: Кому-то");
                    }
                    else if (Talking.IsAnyone && !Talking.IsPlayer)
                    {
                        Text.Append($"Кто: Кто-то\nКому: Игроку");
                    }
                    else if (Talking.IsParty && !Talking.IsPlayer)
                    {
                        Text.Append($"Кто: Группа {Talking.TalkingWith.ID}\nКому: Игроку");
                    }
                    else if (Talking.IsParty && Talking.IsPlayer)
                    {
                        Text.Append($"Кто: Игрок\nКому: Группе {Talking.TalkingWith.ID}");
                    }
                    else if (Talking.IsPlayer && Talking.IsNpc)
                    {
                        Text.Append($"Кто: Игрок\nКому: {Talking.TalkingWith.ID} ({GetNpcSex(Talking.TalkingWith).ToLower()})");
                    }
                    else if (Talking.IsNpc)
                    {
                        Text.Append($"Кто: {Talking.TalkingWith.ID} ({GetNpcSex(Talking.TalkingWith).ToLower()})\nКому: Игроку");
                    }

                    if (Text.Length > 0)
                        return Text.ToString();
                }
            }
            return null;
        }

        // Fixme: cделать всё в парсере, а не так.
        private async void PrepareCategoriesForTable()
        {
            await Task.Run(() =>
            {
                var TroopsCategory = WorkLoad.FindByPrefix("trp_");

                if (TroopsCategory != null && TroopsCategory.Rows != null)
                {
                    var DialogCategory = WorkLoad.FindByPrefix("dlga_");

                    if (DialogCategory != null && DialogCategory.Rows != null)
                    {
                        var PartyTemplateCategory = WorkLoad.FindByPrefix("pt_");

                        if (PartyTemplateCategory != null && PartyTemplateCategory.Rows != null)
                        {
                            foreach (var Dialog in DialogCategory.Rows)
                            {
                                Dialog.Dialogue = Parser.GetDialogueCondition(Dialog.RawLine, TroopsCategory.Rows, PartyTemplateCategory.Rows);
                            }
                        }
                    }
                }
            });
        }

    }
}