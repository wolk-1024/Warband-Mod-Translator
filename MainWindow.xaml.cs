/*
 *  (c) wolk-1024
 *  
 *  Планы на релизную версию: 
 *  
 *  Исправить недостатки парсера.
 *  Добавить локализацию на английский.
 *  Доработать режим сравнения.
 *  Добавить работу с несколькими категориями одновременно, без перезагрузки таблицы.
 *  Добавить больше горячих клавиш для поиска.
 */
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

using WarbandAbout;
using WarbandSearch;
using ModTranslatorSettings;
using static WarbandParser.Parser;

//#nullable disable

namespace ModTranslator
{
    public partial class MainTranslatorWindow : Window
    {
        public string AppTitle = "Warband Mod Translator v0.9";

        public int AppWidth = 1366;

        public int AppHeight = 768;

        /// <summary>
        /// Станет true, если ячейка перевода будет изменена.
        /// </summary>
        public bool g_DataHasBeenChanged = false;

        /// <summary>
        /// Путь к моду.
        /// </summary>
        public string g_ModFolderPath = string.Empty;

        /// <summary>
        /// Теущий обрабатываемый файл.
        /// </summary>
        public string g_CurrentOriginalFile = string.Empty;

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
        /// Тут все данные для биндинга с таблицей. Id, текст, перевод.
        /// </summary>
        private List<ModTextRow> g_CurrentMainGridData = new List<ModTextRow>();

        /// <summary>
        /// Режим сравнения версий.
        /// </summary>
        public bool g_CompareMode = false;

        /// <summary>
        /// Допустимые разделители в csv файлах.
        /// Для Warband ['|']
        /// Для Google Sheets [',']
        /// </summary>
        public char[] g_CsvFileSeparators = { '|', ',' };

        /// <summary>
        /// Глобалка со списком замен знаков, которые искаженно отображаются в игре.
        /// </summary>
        private readonly Dictionary<char, char> g_TextForbiddenChars = new Dictionary<char, char>
        {
            { '—', '-' }
            //{ 'ё', 'е' },
            //{ 'Ё', 'Е' }
        };

        //private List<CollectionTextData> g_CollectionTextData = new List<CollectionTextData>(); //

        /// <summary>
        /// Окно настроек.
        /// </summary>
        private readonly SettingsWindow g_OptionsWindow;

        /// <summary>
        /// Окошко поиска.
        /// </summary>
        private readonly SearchWindow g_SearchWindow;

        /// <summary>
        /// Текущий столбец, выбранный правой кнокой мыши.
        /// </summary>
        //private DataGridColumn? g_CurrentColumn = null;

        public class TextImportInfo
        {
            public int SuccessLoaded { get; set; }

            public List<ModTextRow> FailedLoad = new List<ModTextRow> { };
        }

        /// <summary>
        /// Данные для FileBox
        /// </summary>
        private readonly Dictionary<string, (string OriginalFile, string CsvFile)> g_BindingsFileNames = new Dictionary<string, (string, string)>
        {
            { "Диалоги",             ("conversation.txt",    "dialogs.csv") },
            { "Фракции",             ("factions.txt",        "factions.csv") },
            { "Страницы информации", ("info_pages.txt",      "info_pages.csv") },
            { "Виды предметов",      ("item_kinds1.txt",     "item_kinds.csv") },
            { "Состояние предметов", ("item_modifiers.txt",  "item_modifiers.csv") },
            { "Игровое меню",        ("menus.txt",           "game_menus.csv")},
            { "Места",               ("parties.txt",         "parties.csv") },
            { "Шаблоны мест",        ("party_templates.txt", "party_templates.csv") },
            { "Задания",             ("quests.txt",          "quests.csv") },
            { "Быстрые строки",      ("quick_strings.txt",   "quick_strings.csv") },
            { "Навыки",              ("skills.txt",          "skills.csv") },
            { "Скины",               ("skins.txt",           "skins.csv") },
            { "Игровые строки",      ("strings.txt",         "game_strings.csv") },
            { "Войска",              ("troops.txt",          "troops.csv") },
        };

        /*
        public class CollectionTextData
        {
            public bool IsLoaded { get; set; }

            public bool DataHasBeenChanged { get; set; }

            public string? FileForExport { get; set; }

            public string? FileForTranslate { get; set; }

            public List<ModTextRow> TextData = new List<ModTextRow>();
        }
        */

        public void InitMainWindow()
        {
            this.Title = AppTitle;

            this.Width = AppWidth;

            this.Height = AppHeight;

            this.WindowState = WindowState.Normal;

            this.Closing += MainWindowClosing; // Обработка закрытия окна.

            this.KeyDown += KeyDownHandler; // Глобальная обработка нажатий клавиш.

            MainDataGrid.BeginningEdit += DataGridCellBegining;

            MainDataGrid.CellEditEnding += DataGridCellEditEnding;

            MainDataGrid.SelectedCellsChanged += DataGridSelectedCellsChanged;

            SelectFilesBox.SelectionChanged += SelectFilesBox_SelectionChanged;

            ModPathText.KeyDown += ModPathText_KeyDown;

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            MainDataGrid.LoadingRow += MainDataGrid_LoadingRow;

            MainDataGrid.Sorting += MainDataGrid_Sorting;

            //MainDataGrid.ContextMenu.ContextMenuOpening += DataGridContextMenuOpening;
        }

        private void InitControlElementsHints()
        {
            NextCellButton.ToolTip = "Перейти к следующей непереведённой строке";

            PrevCellButton.ToolTip = "Перейти к предыдущей непереведённой строке";

            SelectFilesBox.ToolTip = "Список игровых категорий";

            SearchMenu.ToolTip = "Ctrl + F";
        }

        public MainTranslatorWindow()
        {
            InitializeComponent();

            InitMainWindow();

            InitControlElementsHints();

            g_OptionsWindow = new SettingsWindow(this);

            g_SearchWindow = new SearchWindow(this);
        }

        private void MainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (g_DataHasBeenChanged)
            {
                MessageBoxResult Result = MessageBox.Show("Данные были изменены. Выйти без сохранения?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (Result == MessageBoxResult.Yes)
                    e.Cancel = false;
                else
                {
                    e.Cancel = true;

                    return;
                }
            }
            g_OptionsWindow.CloseWindow();

            g_SearchWindow.CloseWindow();
        }

        private void DataTextChangedMessage()
        {
            if (g_DataHasBeenChanged && IsLoadedTextData())
            {
                MessageBoxResult Message = MessageBox.Show("Данные не сохранены. Сохранить?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (Message == MessageBoxResult.Yes)
                    ExportFileDialog();

                g_DataHasBeenChanged = false;
            }
        }

        public bool IsLoadedTextData()
        {
            return (g_CurrentMainGridData.Count > 0);
        }

        private void EnableControlButtons()
        {
            ImportButton.IsEnabled = true;

            PrevCellButton.IsEnabled = true;

            NextCellButton.IsEnabled = true;

            SaveButton.IsEnabled = true;
        }

        private void UnloadTableTextData()
        {
            if (IsLoadedTextData())
            {
                g_CurrentSelectedCell.RowIndex = -1;

                g_CurrentSelectedCell.ColumnIndex = -1;

                g_FileForExport = string.Empty;

                g_CurrentCellValue = string.Empty;

                MainDataGrid.ItemsSource = null;

                MainDataGrid.Items.Refresh();

                g_CurrentMainGridData.Clear();
            }
        }

        private void UpdateTableTextData(List<ModTextRow> TextData)
        {
            g_CurrentMainGridData = TextData;

            MainDataGrid.ItemsSource = TextData;

            MainDataGrid.Items.Refresh();
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

            bool inQuotes = false;

            var CurrentField = "";

            for (int i = 0; i < TextLine.Length; i++)
            {
                char Char = TextLine[i];

                if (Char == '"')
                {
                    if (i + 1 < TextLine.Length && TextLine[i + 1] == '"')
                    {
                        CurrentField += '"';

                        i++;
                    }
                    else
                        inQuotes = !inQuotes;
                }
                else if (Char == Separator && !inQuotes)
                {
                    Result.Add(CurrentField);

                    CurrentField = "";
                }
                else
                    CurrentField += Char;
            }
            Result.Add(CurrentField);

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

                if (LineArgs.Count <= 1 || LineArgs.Count > MaxArgs) // Параметров не должно быть больше MaxArgs, если больше, то явная ошибка.
                {
                    Result.Clear();

                    return Result;
                }

                Result.Add(new ModTextRow
                {
                    RowId = LineArgs[0],
                    TranslatedText = LineArgs[1]
                });
            }
            return Result;
        }

        /*
        public class TranslationRecordMap : ClassMap<ModTextRow>
        {
            public TranslationRecordMap()
            {
                Map(m => m.RowId).Index(0);

                Map(m => m.TranslatedText).Index(1);

                Map(m => m.OriginalText).Index(2).Optional();
            }
        }

        public static List<ModTextRow> ReadCsvFile(string FilePath)
        {
            try
            {
                var Separator = DetectCsvFileSeparator(FilePath).ToString();

                if (Separator != null)
                {
                    using (var FileReader = new StreamReader(FilePath))
                    {
                        var CsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture);

                        CsvConfig.Delimiter = Separator;

                        CsvConfig.HasHeaderRecord = false;

                        CsvConfig.MissingFieldFound = null;

                        CsvConfig.BadDataFound = null;

                        using (var Csv = new CsvHelper.CsvReader(FileReader, CsvConfig))
                        {
                            Csv.Context.RegisterClassMap<TranslationRecordMap>();

                            return Csv.GetRecords<ModTextRow>().ToList();
                        }
                    }
                }
                return new List<ModTextRow>();
            }
            catch (Exception)
            {
                return new List<ModTextRow>();
            }
        }

        public static List<ModTextRow> ReadCsvFile(string FileName)
        {
            var Result = new List<ModTextRow>();

            try
            {
                string[] FileLines = File.ReadAllLines(FileName, Encoding.UTF8);

                foreach (string Line in FileLines)
                {
                    var SplitLine = Line.Split('|', StringSplitOptions.RemoveEmptyEntries);

                    if (SplitLine.Length > 1)
                    {
                        Result.Add(
                            new ModTextRow
                            {
                                RowId = SplitLine[0],
                                TranslatedText = SplitLine[1]
                            });
                    }
                }
                return Result;
            }
            catch (Exception)
            {
                return Result;
            }
        }
        */

        private List<ModTextRow>? ProcessOriginalFiles(string FilePath)
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
                            Result = LoadAndParseConversationFile(FilePath);
                            break;
                        }
                    case "factions.txt":
                        {
                            Result = LoadAndParseFactionsFile(FilePath);
                            break;
                        }
                    case "info_pages.txt":
                        {
                            Result = LoadAndParseInfoPagesFile(FilePath);
                            break;
                        }
                    case "item_kinds1.txt":
                        {
                            Result = LoadAndParseItemKindsFile(FilePath);
                            break;
                        }
                    case "item_modifiers.txt": // Этого файла часто не бывает в модах.
                        {
                            Result = LoadAndParseItemModifiersFile(FilePath);
                            break;
                        }
                    case "menus.txt":
                        {
                            Result = LoadAndParseMenuFile(FilePath);
                            break;
                        }
                    case "parties.txt":
                        {
                            Result = LoadAndParsePartiesFile(FilePath);
                            break;
                        }
                    case "party_templates.txt":
                        {
                            Result = LoadAndParsePartyTemplatesFile(FilePath);
                            break;
                        }
                    case "quests.txt":
                        {
                            Result = LoadAndParseQuestsFile(FilePath);
                            break;
                        }
                    case "quick_strings.txt":
                        {
                            Result = LoadAndParseQuickStringsFile(FilePath);
                            break;
                        }
                    case "skills.txt":
                        {
                            Result = LoadAndParseSkillsFile(FilePath);
                            break;
                        }
                    case "skins.txt":
                        {
                            Result = LoadAndParseSkinsFile(FilePath);
                            break;
                        }
                    case "strings.txt":
                        {
                            Result = LoadAndParseStringsFile(FilePath);
                            break;
                        }
                    case "troops.txt":
                        {
                            Result = LoadAndParseTroopsFile(FilePath);
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

        private bool ProcessAndLoadOriginalFiles(string FilePath)
        {
            if (!File.Exists(FilePath))
                return false;

            var LoadedData = ProcessOriginalFiles(FilePath);

            if (LoadedData != null)
            {
                UnloadTableTextData(); // Очищаем таблицу.

                UpdateTableTextData(LoadedData);

                SetTranslateCountLabel();

                return true;
            }
            return false;
        }

        // Fixme: Вызывает срабатывание SelectFilesBox_SelectionChanged
        private int LoadOriginalFilesToFileBox(string FolderPath)
        {
            var Result = 0;

            var NewFilesList = new List<string> { };

            for (int i = 0; i < g_BindingsFileNames.Count; i++)
            {
                var FileNames = g_BindingsFileNames.Values.ElementAt(i);

                var TitleName = g_BindingsFileNames.Keys.ElementAt(i);

                var OrginalFilePath = FolderPath + "\\" + FileNames.OriginalFile;

                if (File.Exists(OrginalFilePath))
                {
                    Result++;

                    NewFilesList.Add(TitleName);
                }
            }
            if (Result > 0)
            {
                SelectFilesBox.ItemsSource = NewFilesList; // Херня вызывает срабатывание SelectFilesBox_SelectionChanged как итог файл может грузиться более 1 раза.

                SelectFilesBox.SelectedIndex = 0; // Начинаем список с 1 позиции. Не трогать.

                SelectFilesBox.Items.Refresh();
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


        public static string? GetCellStringValue(DataGrid Table, int RowIndex, int ColumnIndex)
        {
            var Item = Table.Items[RowIndex];

            var Column = Table.Columns[ColumnIndex];

            if (Column is DataGridBoundColumn BoundColumn)
            {
                var Binding = BoundColumn.Binding as Binding;

                if (Binding != null)
                {
                    var PropertyInfo = Item.GetType().GetProperty(Binding.Path.Path);

                    if (PropertyInfo == null)
                        return null;

                    var Value = PropertyInfo.GetValue(Item);

                    if (Value != null)
                        return Value.ToString();
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

            var item = TableGrid.Items[RowIndex];

            var PropertyInfo = item.GetType().GetProperties()[ColumnIndex];

            try
            {
                //var ConvertedValue = Convert.ChangeType(newValue, PropertyInfo.PropertyType);

                PropertyInfo.SetValue(item, NewValue);

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

                //MainDataGrid.Items.Refresh();

                return true;
            }
            return false;
        }

        private bool LoadFileBoxItemByIndex(int SelectedIndex)
        {
            if (SelectedIndex <= SelectFilesBox.Items.Count)
            {
                //SelectFilesBox.SelectedIndex = SelectedIndex;

                string? SelectedElement = SelectFilesBox.Items[SelectedIndex].ToString();

                if (SelectedElement != null)
                {
                    string FilePath = g_ModFolderPath + "\\" + g_BindingsFileNames[SelectedElement].OriginalFile;

                    if (ProcessAndLoadOriginalFiles(FilePath))
                    {
                        g_CurrentOriginalFile = FilePath;

                        SetTranslateCountLabel();

                        return true;
                    }
                }
            }
            return false;
        }

        private TextImportInfo ImportTranslate(string FilePath)
        {
            var Result = new TextImportInfo { SuccessLoaded = 0 };

            var TranslateData = ReadModTextCsvFile(FilePath, g_CsvFileSeparators, 3);

            if (TranslateData.Count >= 1)
            {
                foreach (ModTextRow Data in TranslateData)
                {
                    if (IsDummyRow(Data)) // Не импортируем "противобаговую" строку.
                        continue;

                    // Чувствителен к регистру.
                    var DataIndex = g_CurrentMainGridData.FindIndex(0, g_CurrentMainGridData.Count, Item => string.Equals(Item.RowId, Data.RowId, StringComparison.Ordinal)); // Ищем совпадения по Id

                    // Не чуствителен
                    //var DataIndex = g_CurrentMainGridData.FindIndex(0, g_CurrentMainGridData.Count, Item => string.Equals(Item.RowId, Data.RowId, StringComparison.OrdinalIgnoreCase));

                    if (DataIndex >= 0)
                    {
                        g_CurrentMainGridData[DataIndex].TranslatedText = Data.TranslatedText;

                        Result.SuccessLoaded++;
                    }
                    else
                        Result.FailedLoad.Add(Data); // В файле-переводе и оригинале нет совпадения ID. (Вероятно, импортируемый файл может быть старой версией перевода)
                }
                UpdateTableTextData(g_CurrentMainGridData);
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
                    if (!string.IsNullOrEmpty(TextMod.TranslatedText))
                        TranslatedCount++;
                }
            }
            return TranslatedCount;
        }

        private void SetTranslateCountLabel(long TranslateLines, long AllLines)
        {
            TranslateCount.Content = string.Format("Переведено: {0}\\{1}", TranslateLines, AllLines);
        }

        private void SetTranslateCountLabel()
        {
            SetTranslateCountLabel(HowManyTranslatedLines(), g_CurrentMainGridData.Count);
        }

        private string ReplaceForbiddenChars(string Input)
        {
            if (string.IsNullOrEmpty(Input))
                return Input;

            return new string(Input.Select(c => g_TextForbiddenChars.GetValueOrDefault(c, c)).ToArray());
        }

        private void ExportModTextToFile(string FilePath)
        {
            bool WriteDummy = false;

            using (StreamWriter WriteText = new StreamWriter(FilePath))
            {
                foreach (ModTextRow TextData in g_CurrentMainGridData)
                {
                    if (WriteDummy == false)
                    {
                        var Prefix = ExtractPrefixFromId(TextData.RowId);

                        WriteText.WriteLine(Prefix + "1164|Do not delete this line");

                        WriteDummy = true;
                    }

                    string TranslatedData = TextData.TranslatedText;

                    if (g_OptionsWindow.FreeExport.IsChecked == true)
                    {
                        if (string.IsNullOrEmpty(TranslatedData))
                            TranslatedData = TextData.OriginalText;
                    }

                    if (!string.IsNullOrEmpty(TranslatedData)) // Не записываем в файл поля без перевода.
                    {
                        var ResultTranlatedText = ReplaceForbiddenChars(TranslatedData);

                        string NewLine = TextData.RowId + "|" + ResultTranlatedText;

                        WriteText.WriteLine(NewLine.TrimEnd());
                    }
                }
            }
        }

        private string GetTranslateFileNameFromFileBox()
        {
            string? FileName = SelectFilesBox.SelectedValue.ToString();

            if (FileName != null)
            {
                string Result = g_BindingsFileNames[FileName].CsvFile;

                if (string.IsNullOrEmpty(Result))
                    return "translate.csv"; //
                else
                    return Result;
            }
            return string.Empty;
        }

        private void ExportFileDialog()
        {
            var FileDialog = new SaveFileDialog();

            FileDialog.Filter = "Файл перевода|*.csv";

            var CsvName = GetTranslateFileNameFromFileBox();

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
                ExportModTextToFile(FileDialog.FileName);

                g_DataHasBeenChanged = false;

                g_FileForExport = FileDialog.FileName;
            }
        }

        private void DataGridCellBegining(object? sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Column.Header.ToString() == "ID")
            {
                //
            }

            if (e.Column.Header.ToString() == "Оригинал")
            {
                //
            }

            if (e.Column.Header.ToString() == "Перевод")
            {
                g_CurrentCellValue = GetCellStringValue(sender);
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

        private void DataGridCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.ToString() == "ID")
            {
                //
            }

            if (e.Column.Header.ToString() == "Оригинал")
            {
                //
            }

            if (e.Column.Header.ToString() == "Перевод")
            {
                if (e.EditAction == DataGridEditAction.Commit)
                {
                    var TranslatedLines = HowManyTranslatedLines();

                    var NewText = ((TextBox)e.EditingElement).Text;

                    if (NewText != g_CurrentCellValue) // Если ячейка была изменена
                    {
                        if (NewText == string.Empty) // и стала пустой, то
                        {
                            if (TranslatedLines > 0)
                                TranslatedLines--; // уменьшаем счётчик.
                        }

                        if (g_CurrentCellValue == string.Empty) // Если ячейка была изменена и при этом она была пустой, то
                            TranslatedLines++; // увеличиваем счётчик.

                        g_DataHasBeenChanged = true;
                    }
                    SetTranslateCountLabel(TranslatedLines, g_CurrentMainGridData.Count);

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

        public ModTextRow? SearchModTextByValue(string Value, int StartRow = 0, bool FullSearch = true, bool VisibleColumn = true, StringComparison CaseType = StringComparison.Ordinal)
        {
            var Indexes = FindCellByString(MainDataGrid, Value, StartRow, FullSearch, VisibleColumn, CaseType);

            if (Indexes.ColumnIndex >= 0 && Indexes.RowIndex >= 0)
            {
                var Item = MainDataGrid.Items.GetItemAt(Indexes.RowIndex);

                var Result = Item as ModTextRow;

                return Result;
            }
            return null;
        }


        /// <summary>
        /// 
        /// </summary>
        /// 
        /// <param name="Table"></param>
        /// <param name="Value"></param>
        /// <param name="StartRow">Стартовый индекс строки от которой будет идти поиск</param>
        /// <param name="FullSearch">Поиск строки целиком, а не по первому вхождению.</param>
        /// <param name="VisibleColumn">Поиск только в отображаемых столбцах</param>
        /// <param name="Case"></param>
        /// <returns></returns>
        public (int RowIndex, int ColumnIndex) FindCellByString(DataGrid Table, string Value, int StartRow, bool FullSearch = false, bool VisibleColumn = true, StringComparison Case = StringComparison.Ordinal)
        {
            for (int Row = StartRow; Row < Table.Items.Count; Row++)
            {
                for (int Col = 0; Col < Table.Columns.Count; Col++)
                {
                    if (VisibleColumn) // Поиск только в видимых столбцах.
                    {
                        if (Table.Columns[Col].Visibility != Visibility.Visible)
                            continue;
                    }

                    var CellValue = GetCellStringValue(Table, Row, Col);

                    if (CellValue != null)
                    {
                        string Result = CellValue.ToString();

                        if (FullSearch)
                        {
                            if (Result.Equals(Value, Case))
                                return (Row, Col);
                        }
                        else
                        {
                            if (Result.IndexOf(Value, Case) >= 0)
                                return (Row, Col);
                        }
                    }
                }
            }
            return (-1, -1);
        }

        private void SelectFilesBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            g_CompareMode = false;

            string? SelectedValue = SelectFilesBox.SelectedValue.ToString();

            if (!string.IsNullOrEmpty(SelectedValue))
            {
                string FilePath = g_ModFolderPath + "\\" + g_BindingsFileNames[SelectedValue].OriginalFile;

                if (!string.Equals(g_CurrentOriginalFile, FilePath))
                {
                    DataTextChangedMessage();

                    if (ProcessAndLoadOriginalFiles(FilePath))
                    {
                        FocusFirstRow();

                        EnableControlButtons();

                        SetTranslateCountLabel();

                        g_CurrentOriginalFile = FilePath;
                    }
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var FolderDialog = new OpenFolderDialog();

            //FolderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            FolderDialog.Title = "Выбрать папку с установленным модом";

            FolderDialog.Multiselect = false;

            if (FolderDialog.ShowDialog() == true)
            {
                SelectFilesBox.IsEnabled = true;

                if (LoadOriginalFilesToFileBox(FolderDialog.FolderName) > 0) // Fixme: Вызывает срабатывание SelectFilesBox_SelectionChanged
                {
                    g_ModFolderPath = FolderDialog.FolderName;

                    if (LoadFileBoxItemByIndex(0))
                    {
                        ModPathText.Text = FolderDialog.FolderName;

                        EnableControlButtons();
                    }
                    else
                        MessageBox.Show("Ошибка загрузки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                    MessageBox.Show("Неверный мод.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoadedTextData())
                return;

            ExportFileDialog();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoadedTextData())
                return;

            var DialogFile = new OpenFileDialog();

            DialogFile.Filter = "Файл перевода|*.csv";

            DialogFile.FileName = GetTranslateFileNameFromFileBox();

            var SourceDirectory = g_ModFolderPath + "\\languages";

            if (Directory.Exists(SourceDirectory))
                DialogFile.InitialDirectory = SourceDirectory;
            else
                DialogFile.InitialDirectory = g_ModFolderPath;

            if (DialogFile.ShowDialog() == true)
            {
                var Result = ImportTranslate(DialogFile.FileName);

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

                g_DataHasBeenChanged = true;
            }
        }

        private void ModPathText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) // Ввод
            {
                if (Directory.Exists(ModPathText.Text))
                {
                    DataTextChangedMessage();

                    if (LoadOriginalFilesToFileBox(ModPathText.Text) > 0)
                    {
                        g_ModFolderPath = ModPathText.Text;

                        SelectFilesBox.IsEnabled = true;

                        if (LoadFileBoxItemByIndex(0))
                        {
                            EnableControlButtons();
                        }
                    }
                }
                else
                    MessageBox.Show("Неверный путь.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                e.Handled = true;
            }
        }

        private void KeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) // Ctrl + S
            {
                g_DataHasBeenChanged = false;

                if (string.IsNullOrEmpty(g_FileForExport) && IsLoadedTextData())
                {
                    ExportFileDialog();
                }
                else if (IsLoadedTextData())
                {
                    this.Cursor = Cursors.Wait;

                    Thread.Sleep(150);

                    this.Cursor = null;

                    ExportModTextToFile(g_FileForExport);
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
                DataTextChangedMessage();

                if (g_CompareMode)
                {
                    g_CompareMode = false;

                    ProcessAndLoadOriginalFiles(g_CurrentOriginalFile);
                }
                else
                {
                    if (ChooseModAndSeeDifference())
                        g_CompareMode = true;
                }
                e.Handled = true;
            }
        }

        private bool ChooseModAndSeeDifference()
        {
            if (!IsLoadedTextData())
                return false;

            var FolderDialog = new OpenFolderDialog();

            FolderDialog.Title = "Выбрать папку со старой версией мода";

            FolderDialog.Multiselect = false;

            if (!string.IsNullOrEmpty(g_ModFolderPath))
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

                var ModChanges = GetModTextChanges(g_CurrentMainGridData, LoadedData);

                if (ModChanges.Count == 0)
                {
                    MessageBox.Show($"Разницы нет", "Сравнение", MessageBoxButton.OK, MessageBoxImage.Information);

                    //return;
                }

                UnloadTableTextData();

                UpdateTableTextData(ModChanges);

                SetTranslateCountLabel();

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

        private void FocusFirstRow()
        {
            var Data = GetRowDataByIndex(0);

            if (Data != null)
            {
                //MainDataGrid.SelectedIndex = 0;

                FocusOnRow(Data);
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
                var Item = Data.Items[RowIndex];

                var Column = Data.Columns[ColumnIndex];

                Data.ScrollIntoView(Item, Column); // Фокус на строку.

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

        /// <summary>
        /// Фокус на ближайшую+1 пустую ячейку
        /// </summary>
        /// <param name="TableGrid"></param>
        /// <param name="ColumnName"></param>
        /// <param name="Cycle"></param>
        /// <returns></returns>
        private int NextFocusCell(DataGrid TableGrid, string ColumnName, bool Cycle = false)
        {
            if (!IsLoadedTextData())
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

                if (ModText.TranslatedText == string.Empty)
                {
                    FocusCell(TableGrid, NextIndex, ColumnIndex);

                    return 1;
                }
                NextIndex++;
            }

            if (Cycle)
                g_CurrentSelectedCell.RowIndex = -1;

            return 0;
        }

        private int PrevFocusCell(DataGrid TableGrid, string ColumnName, bool Cycle = false)
        {
            if (!IsLoadedTextData())
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

                if (ModText.TranslatedText == string.Empty)
                {
                    FocusCell(TableGrid, PrevIndex, ColumnIndex);

                    return 1;
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

            g_OptionsWindow.Owner = this; // Создаём дочернее окно, а не отдельное.
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

        private void MainDataGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            //e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void MainDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
        }

        private void DataGridColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            DataGridColumnHeader? Header = sender as DataGridColumnHeader;

            if (Header != null && IsLoadedTextData())
            {
                DataGridColumn Column = Header.Column;

                if (Column.Header.ToString() == "№")
                {
                    if (Column != null && Column.CanUserSort)
                    {
                        int RowNum = 0;

                        int.TryParse(g_CurrentMainGridData[0].RowNum, out RowNum);

                        if (Column.SortDirection == null && RowNum == 1)
                        {
                            Column.SortDirection = ListSortDirection.Ascending;
                        }
                        else if (Column.SortDirection == ListSortDirection.Ascending)
                        {
                            Column.SortDirection = ListSortDirection.Descending;
                        }
                        else if (Column.SortDirection == ListSortDirection.Descending)
                        {
                            Column.SortDirection = ListSortDirection.Ascending;
                        }

                        g_CurrentMainGridData.Reverse(); // Вместо коллекций и прочего сортируем данные в лоб.

                        UpdateTableTextData(g_CurrentMainGridData);
                    }
                }
            }
        }

        /*
        public static List<string> GetAllColumnText(DataGrid TableData, DataGridColumn Column)
        {
            var Result = new List<string>();

            DataGridBoundColumn? BoundColumn = Column as DataGridBoundColumn;

            if (BoundColumn == null)
                return Result;

            string? BindingPath = (BoundColumn.Binding as Binding)?.Path?.Path;

            if (string.IsNullOrEmpty(BindingPath))
                return Result;

            foreach (var Item in TableData.Items)
            {
                if (Item == CollectionView.NewItemPlaceholder) // хз зачем это.
                    continue;

                var Property = Item.GetType().GetProperty(BindingPath);

                var Value = Property?.GetValue(Item, null);

                if (Value != null)
                {
                    var StringValue = Value.ToString();

                    Result.Add(StringValue ?? string.Empty);
                }
            }
            return Result;
        }
        */

        public static List<string> GetAllColumnText(DataGrid TableData, DataGridColumn Column)
        {
            var Result = new List<string>();

            if (TableData == null || Column == null)
                return Result;

            var ColumnIndex = TableData.Columns.IndexOf(Column);

            if (ColumnIndex == -1)
                return Result;

            for (int RowIndex = 0; RowIndex < TableData.Items.Count; RowIndex++)
            {
                var CellValue = GetCellStringValue(TableData, RowIndex, ColumnIndex);

                if (CellValue == null)
                    CellValue = string.Empty;

                Result.Add(CellValue);
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
                    var Result = GetAllColumnText(TableGrid, Column);

                    File.WriteAllLines(FileDialog.FileName, Result, Encoding.UTF8);

                    if (Result.Count == TableGrid.Items.Count)
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

                    if (AllLines.Length > TableGrid.Items.Count)
                    {
                        MessageBox.Show($"Файл {FileDialog.FileName} содержит больше полей, чем есть в таблице.", "Ошибка загрузки", MessageBoxButton.OK, MessageBoxImage.Error);

                        return false;
                    }

                    if (AllLines.Length > 0)
                    {
                        int ColumnIndex = TableGrid.Columns.IndexOf(Column);

                        if (ColumnIndex >= 0)
                        {
                            for (int i = 0; i < AllLines.Length; i++)
                            {
                                SetCellValue(TableGrid, i, ColumnIndex, AllLines[i]);

                                TableGrid.Items.Refresh(); // Обновляем табличку.

                                SetTranslateCountLabel();
                            }
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
            if (!IsLoadedTextData()) // Не показываем меню, если не знагружена таблица.
                e.Handled = true;

            /*
            if (sender is DataGrid TableGrid)
                g_CurrentColumn = TableGrid.CurrentColumn;
            */
        }

    }
}