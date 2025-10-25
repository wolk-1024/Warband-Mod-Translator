/*
 *  Парсер v24.10.2025
 *  
 *  Известные проблемы: 
 *  
 *  1) Если строка, например, в диалогах будет состоять только из чисел, то парсер не сможет их найти, т.к нет пока возможности отделить флаги от текста.
 */
using EncodingTextFile;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WarbandParser
{
    public static class Parser
    {
        /// <summary>
        /// Настройка для удаления дубликатов ID.
        /// </summary>
        public static bool g_DeleteDublicatesIDs = true;

        /// <summary>
        /// Настройка для игнорирование {!} в строках. 
        /// </summary>
        public static bool g_IgnoreBlockingSymbol = false;

        /// <summary>
        /// Регулярка для не юникод чисел
        /// </summary>
        private static readonly Regex RegOnlyNumbers = new Regex(@"^\s*-?[0-9]+\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Не юникод-float и НЕ экспонента.
        /// </summary>
        private static Regex RegOnlyFloatNumbers = new Regex(@"^[+-]?(?:[0-9]+\.[0-9]+|[0-9]+)(?<!\.[+-]?)$", RegexOptions.Compiled);

        /// <summary>
        /// Регулярка для слов и знаков.
        /// Юникод-числа и экспоненциальные записи будут считаться строкой.
        /// </summary>
        private static readonly Regex RegWordsAndSymbols = new Regex(@"^(?!\s*$)(?!\s*-?[0-9]+(?:\.[0-9]+)?\s*$).+", RegexOptions.Compiled);

        /// <summary>
        /// Список поддерживаемых префиксов.
        /// </summary>
        private readonly static string[] g_IdPrefixesList =
        {
            "dlga_",    // dialogs
            "fac_",     // factions
            "ip_",      // info pages
            "itm_",     // item kinds
            "imod_",    // item modifiers
            "menu_",    // game menus
            "mno_",     // game menus 2
            "p_",       // parties
            "pt_",      // party templates
            "qst_",     // quests
            "qstr_",    // quick strings
            "skl_",     // skills
            "skinkey_", // skins
            "str_",     // strings
            "trp_",     // troops
            "ui_"       // ui
        };

        public static bool IsDummyRow(ModTextRow Data)
        {
            var RowId = Data.RowId;

            if (IsLineStartsWithPrefix(RowId))
            {
                if (RowId.EndsWith("_1164"))
                    return true;
            }
            return false;
        }

        public static string ExtractPrefixFromId(string FullID)
        {
            if (IsLineStartsWithPrefix(FullID))
            {
                var Result = FullID.Split("_");

                if (Result.Length >= 0)
                    return Result[0] + "_";
            }
            return string.Empty;
        }

        public static bool IsLineStartsWithPrefix(string Input, string Prefix)
        {
            if (!string.IsNullOrEmpty(Input))
            {
                if (Input.StartsWith(Prefix) && Input.EndsWith(string.Empty))
                    return true;
            }
            return false;
        }

        public static bool IsLineStartsWithPrefix(string Input)
        {
            if (!string.IsNullOrEmpty(Input))
            {
                foreach (string Prefix in g_IdPrefixesList)
                {
                    if (IsLineStartsWithPrefix(Input, Prefix))
                        return true;
                }
            }
            return false;
        }

        private static bool IsLineStartsWithPrefixes(string Input, string[] Prefixes)
        {
            if (!string.IsNullOrEmpty(Input))
            {
                foreach (var Pref in Prefixes)
                {
                    if (!IsLineStartsWithPrefix(Input, Pref))
                        return false;
                }
                return true;
            }
            return false;
        }

        private static bool IsBlockedLine(string Input)
        {
            if (!string.IsNullOrEmpty(Input) && !g_IgnoreBlockingSymbol)
            {
                var Result = Input.TrimStart(); // Иногда блок стоит не в самом конце, а его разделяют пробелы.

                return Result.StartsWith("{!}", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// Для любых строк, знаков, включая юникод.
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        private static bool IsStringArg(string Value)
        {
            return RegWordsAndSymbols.IsMatch(Value);
        }

        /// <summary>
        /// Только обычные не юникод 32-битные числа.
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        private static bool IsInteger32Arg(string Value)
        {
            if (RegOnlyNumbers.IsMatch(Value))
                return int.TryParse(Value, NumberStyles.Integer, CultureInfo.GetCultureInfo("en-US"), out _);
            else
                return false;
        }

        /// <summary>
        /// Только обычные не юникод 64-битные числа.
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        private static bool IsInteger64Arg(string Value)
        {
            if (RegOnlyNumbers.IsMatch(Value))
                return Int64.TryParse(Value, NumberStyles.Integer, CultureInfo.GetCultureInfo("en-US"), out _);
            else
                return false;
        }

        /// <summary>
        /// Не пропускает числа вида +100, .100 и 100. и экспоненциальные записи
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        private static bool IsFloatArg(string Value)
        {
            if (RegOnlyFloatNumbers.IsMatch(Value))
                return float.TryParse(Value, NumberStyles.Float, CultureInfo.GetCultureInfo("en-US"), out _);
            else
                return false;
        }

        private static Type? DetectArgType(string Value)
        {
            if (!string.IsNullOrEmpty(Value))
            {
                if (IsInteger32Arg(Value))
                    return typeof(int);

                if (IsInteger64Arg(Value))
                    return typeof(Int64);

                if (IsFloatArg(Value))
                    return typeof(float);

                if (IsStringArg(Value))
                    return typeof(string);
            }
            return null;
        }

        private static object? GetArgByType(string Data, int ArgNum, Type ArgType)
        {
            if (string.IsNullOrEmpty(Data))
                return null;

            var TextLines = Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (TextLines.Count() >= ArgNum)
            {
                if (IsLineStartsWithPrefix(TextLines[0])) // Всегда начинатьcя должно с id
                {
                    var ArgCount = 0;

                    for (int Count = 0; Count < TextLines.Count(); Count++)
                    {
                        var ResultArg = TextLines[Count];

                        if (DetectArgType(ResultArg) == ArgType)
                        {
                            ArgCount++;

                            if (ArgCount == ArgNum)
                                return ResultArg;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>Получение строкового параметра</summary>
        /// <returns>Параметр или string.Empty</returns>
        private static string GetStringArg(string TextData, int ArgNum)
        {
            if (!string.IsNullOrEmpty(TextData))
            {
                var Result = GetArgByType(TextData, ArgNum, typeof(string));

                if (Result != null)
                    return Result.ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        /// <returns>Параметр или null</returns>
        private static int? GetIntArg(string TextData, int ArgNum)
        {
            if (!string.IsNullOrEmpty(TextData))
            {
                var Result = GetArgByType(TextData, ArgNum, typeof(int));

                if (Result != null)
                {
                    var StrArg = Result.ToString();

                    if (!string.IsNullOrEmpty(StrArg))
                        return int.Parse(StrArg);
                }
            }
            return null;
        }

        /// <returns>Параметр или string.Empty</returns>
        public static string GetAnyArg(string TextData, int ArgNum)
        {
            if (!string.IsNullOrEmpty(TextData))
            {
                var Result = TextData.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (ArgNum <= Result.Length)
                    return Result[ArgNum - 1];
            }
            return string.Empty;
        }

        private static List<ParseArg> ParseTextData(string TextData, string[] Prefixes)
        {
            var Result = new List<ParseArg>();

            if (string.IsNullOrEmpty(TextData) || Prefixes.Count() == 0)
                return Result;

            var RegPrefixes = Prefixes.Select(p => $"{p}\\w+");

            string Pattern = $@"(?:^|\W)({string.Join("|", RegPrefixes)})(?=$|\W)";

            var PrefixesMatches = Regex.Matches(TextData, Pattern);

            for (int Count = 0; Count < PrefixesMatches.Count; Count++)
            {
                var CurrentMatch = PrefixesMatches[Count];

                var CurrentVal = CurrentMatch.Value;

                if (IsLineStartsWithPrefixes(CurrentVal.TrimStart(), Prefixes))
                {
                    int PosStart = CurrentMatch.Index; // Начальная позиция префикса.

                    var PosEnd = TextData.Length; // Конечная позиция будет концом текста, но

                    if ((Count + 1) < PrefixesMatches.Count) // если количество найденых префиксов будет меньше, чем текущий индекс префикса + 1, то
                    {
                        PosEnd = PrefixesMatches[Count + 1].Index; // конечная позиция станет началом следующего префикса.
                    }

                    var ArgLength = PosEnd - PosStart;

                    var ArgResult = TextData.Substring(PosStart, ArgLength).Trim();

                    Result.Add(
                        new ParseArg()
                        {
                            Value = ArgResult,
                            Start = PosStart,
                            End = PosEnd
                        });
                }
            }
            return Result;
        }

        private static List<ParseArg> ParseTextData(string TextData, string Prefix)
        {
            return ParseTextData(TextData, [Prefix]);
        }

        public static List<ParseArg> LoadAndParseFile(string FilePath, string[] Prefixes)
        {
            var Result = new List<ParseArg>();

            if (File.Exists(FilePath))
            {
                string TextData = EncodingText.ReadTextFileAndConvertTo(FilePath, Encoding.Unicode);

                if (TextData.Length > 0)
                    Result = ParseTextData(TextData, Prefixes);
            }
            return Result;
        }

        public static List<ParseArg> LoadAndParseFile(string FilePath, string Prefix)
        {
            return LoadAndParseFile(FilePath, [Prefix]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ModText"></param>
        /// <returns></returns>
        private static List<ModTextRow> MarkDuplicateIDs(List<ModTextRow> ModText)
        {
            var Result = new List<ModTextRow>(ModText);

            var GroupIDs = Result.GroupBy(Row => Row.RowId);

            foreach (var Group in GroupIDs)
            {
                var GroupList = Group.ToList();

                if (GroupList.Count > 1)
                {
                    var FirstItem = GroupList[0];

                    for (int i = 1; i < GroupList.Count; i++)
                    {
                        var Dublicate = GroupList[i];

                        RowFlags NewFlags = Dublicate.Flags | RowFlags.Dublicate;

                        if (!string.Equals(Dublicate.OriginalText, FirstItem.OriginalText))
                        {
                            NewFlags |= RowFlags.DublicateDifferentValue;
                        }

                        int ResultIndex = Result.IndexOf(Dublicate);

                        if (ResultIndex >= 0)
                        {
                            Result[ResultIndex].Flags = NewFlags;
                        }
                    }
                }
            }
            return Result;
        }

        private static OriginalFileHeader GetFileHeader(string FilePath)
        {
            var Result = new OriginalFileHeader();

            if (File.Exists(FilePath))
            {
                using (var Reader = new StreamReader(FilePath))
                {
                    var FirstLine = Reader.ReadLine();

                    var SecondLine = Reader.ReadLine();

                    if (string.IsNullOrEmpty(FirstLine) || string.IsNullOrEmpty(SecondLine))
                        return Result;

                    var SplitFirstLine = FirstLine.Split(" ");

                    if (SplitFirstLine.Count() < 3)
                        return Result;

                    Result.Name = SplitFirstLine.First();

                    int Ver;

                    if (int.TryParse(SplitFirstLine.Last(), out Ver))
                        Result.Version = Ver;

                    var SplitSecondLine = SecondLine.Split(" "); // В parties.txt почему-то по 2 одинаковых числа. хз зачем второе.

                    int Count;

                    if (int.TryParse(SplitSecondLine.First(), out Count))
                        Result.Entries = Count;
                }
            }
            return Result;
        }

        private static int GetCountEntries(string FilePath)
        {
            if (File.Exists(FilePath))
                return GetFileHeader(FilePath).Entries;
            else
                return -1;
        }

        public static WhoTalking GetDialogueStates(string DialogeLine)
        {
            WhoTalking Result = new WhoTalking();

            if (string.IsNullOrEmpty(DialogeLine))
                return Result;

            if (IsLineStartsWithPrefix(DialogeLine, "dlga_"))
            {
                var DialogueFlags = GetIntArg(DialogeLine, 1);

                if (DialogueFlags != null)
                {
                    var Flags = (DialogStates)(DialogueFlags);

                    if (Flags.HasFlag(DialogStates.plyr)) // Строку произносит игрок.
                        Result.Player = true;

                    if (Flags.HasFlag(DialogStates.anyone)) // Общие диалоги для всех нпс.
                    {
                        Result.Anyone = true;
                    }
                    else if (Flags.HasFlag(DialogStates.party_tpl)) // Разговор с группой
                    {
                        Result.Party = true;
                    }
                }
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseConversationFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var Dialogs = LoadAndParseFile(FilePath, ["dlga_"]);

            if (Dialogs.Count == GetCountEntries(FilePath))
            {
                foreach (var Line in Dialogs)
                {
                    // ID, Название, Название (мн.числ), 0, Флаги

                    var DialogID = GetStringArg(Line, 1);

                    var DialogText = GetStringArg(Line, 2).Replace("_", " ");

                    if (string.IsNullOrEmpty(DialogID) || string.IsNullOrEmpty(DialogText))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = DialogID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (Line.Start, Line.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(DialogText) == true ? RowFlags.BlockSymbol : RowFlags.None;

                    var DialogStates = GetDialogueStates(Line);

                    Result.Add(new ModTextRow
                    {
                        RowId        = DialogID,
                        OriginalText = DialogText,
                        Dialogue     = DialogStates,
                        Flags        = NewFlags,
                        DataPos      = (Line.Start, Line.End)
                    });
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseFactionsFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var Factions = LoadAndParseFile(FilePath, "fac_");

            if (Factions.Count == GetCountEntries(FilePath))
            {
                foreach (var FacLine in Factions)
                {
                    var FactionID = GetStringArg(FacLine, 1);

                    var FactionName = GetStringArg(FacLine, 2).Replace("_", " ");

                    if (string.IsNullOrEmpty(FactionID) || string.IsNullOrEmpty(FactionName))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = FactionID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (FacLine.Start, FacLine.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(FactionName) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = FactionID,
                        OriginalText = FactionName,
                        Flags        = NewFlags,
                        DataPos      = (FacLine.Start, FacLine.End)
                    });
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseInfoPagesFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var InfoPages = LoadAndParseFile(FilePath, "ip_");

            if (InfoPages.Count == GetCountEntries(FilePath))
            {
                foreach (var Page in InfoPages)
                {
                    var InfoID = GetAnyArg(Page, 1);

                    var DescriptionText = GetAnyArg(Page, 2).Replace("_", " ");

                    if (string.IsNullOrEmpty(InfoID) || string.IsNullOrEmpty(DescriptionText))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId = InfoID ?? "ERROR_ID",
                            Flags = RowFlags.ParseError,
                            DataPos = (Page.Start, Page.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(DescriptionText) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = InfoID,
                        OriginalText = DescriptionText,
                        Flags        = NewFlags,
                        DataPos      = (Page.Start, Page.End)
                    });

                    var InfoText = GetAnyArg(Page, 3).Replace("_", " ");

                    if (!string.IsNullOrEmpty(InfoText)) // Бывают пустые
                    {
                        string NewID = InfoID + "_text"; // ip_name + _text

                        NewFlags = IsBlockedLine(InfoText) ? RowFlags.BlockSymbol : RowFlags.None;

                        Result.Add(new ModTextRow
                        {
                            RowId        = NewID,
                            OriginalText = InfoText,
                            Flags        = NewFlags,
                            DataPos      = (Page.Start, Page.End)
                        });
                    }
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseItemKindsFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var ItemKinds = LoadAndParseFile(FilePath, "itm_");

            if (ItemKinds.Count == GetCountEntries(FilePath))
            {
                foreach (var Item in ItemKinds)
                {
                    var ItemID = GetStringArg(Item, 1);

                    var ItemName = GetStringArg(Item, 2).Replace("_", " ");

                    if (string.IsNullOrEmpty(ItemID) || string.IsNullOrEmpty(ItemName))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = ItemID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (Item.Start, Item.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(ItemName) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = ItemID,
                        OriginalText = ItemName,
                        Flags        = NewFlags,
                        DataPos      = (Item.Start, Item.End)
                    });

                    var ItemNamePlural = GetStringArg(Item, 3).Replace("_", " ");

                    NewFlags = IsBlockedLine(ItemNamePlural) ? RowFlags.BlockSymbol : RowFlags.None;

                    string NewID = ItemID + "_pl";

                    Result.Add(new ModTextRow
                    {
                        RowId        = NewID,
                        OriginalText = ItemNamePlural,
                        Flags        = NewFlags,
                        DataPos      = (Item.Start, Item.End)
                    });

                }
               return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseItemModifiersFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var ItemMods = LoadAndParseFile(FilePath, "imod_");

            if (ItemMods.Count == GetCountEntries(FilePath))
            {
                foreach (var Mod in ItemMods)
                {
                    var ModID = GetStringArg(Mod, 1);

                    var ModName = GetStringArg(Mod, 2).Replace("_", " ");

                    if (string.IsNullOrEmpty(ModID) || string.IsNullOrEmpty(ModName))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = ModID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (Mod.Start, Mod.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(ModName) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = ModID,
                        OriginalText = ModName,
                        Flags        = NewFlags,
                        DataPos      = (Mod.Start, Mod.End)
                    });
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        private static List<ModTextRow> ParseMenuBlock(ParseArg MenuBlock)
        {
            var Result = new List<ModTextRow>();

            var PartsOfMenu = ParseTextData(MenuBlock, "mno_");

            if (PartsOfMenu.Count() > 0)
            {
                foreach (var SubMenu in PartsOfMenu)
                {
                    var SubMenuID = GetStringArg(SubMenu, 1);

                    var SubMenuText = GetStringArg(SubMenu, 2).Replace("_", " ");

                    // Т.к мы читаем блоки с меню, то адреса подменю будут ОТНОСИТЕЛЬНО начала menu_.
                    var MnoDataPos = ((SubMenu.Start + MenuBlock.Start), (SubMenu.End + MenuBlock.Start));

                    if (string.IsNullOrEmpty(SubMenuID) || string.IsNullOrEmpty(SubMenuText))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = SubMenuID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = MnoDataPos
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(SubMenuText) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = SubMenuID,
                        OriginalText = SubMenuText,
                        Flags        = NewFlags,
                        DataPos      = MnoDataPos
                    });
                }
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseMenuFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var MenuBlocks = LoadAndParseFile(FilePath, "menu_"); // Читаем блоками.

            if (MenuBlocks.Count() == GetCountEntries(FilePath))
            {
                foreach (var Block in MenuBlocks)
                {
                    var MenuID = GetStringArg(Block, 1);

                    var MenuText = GetStringArg(Block, 2).Replace('_', ' ');

                    if (string.IsNullOrEmpty(MenuID) || string.IsNullOrEmpty(MenuText))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = MenuID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (Block.Start, Block.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(MenuText) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = MenuID,
                        OriginalText = MenuText,
                        Flags        = NewFlags,
                        DataPos      = (Block.Start, Block.End)
                    });

                    var SubMenus = ParseMenuBlock(Block);

                    if (SubMenus != null)
                        Result.AddRange(SubMenus);
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParsePartiesFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var Parties = LoadAndParseFile(FilePath, "p_");

            if (Parties.Count == GetCountEntries(FilePath))
            {
                foreach (var Part in Parties)
                {
                    string PartyID = GetStringArg(Part, 1);

                    string PatrtyName = GetStringArg(Part, 2).Replace("_", " ");

                    if (string.IsNullOrEmpty(PartyID) || string.IsNullOrEmpty(PatrtyName))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = PartyID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (Part.Start, Part.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(PatrtyName) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = PartyID,
                        OriginalText = PatrtyName,
                        Flags        = NewFlags,
                        DataPos      = (Part.Start, Part.End)
                    });
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParsePartyTemplatesFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var PartyTemp = LoadAndParseFile(FilePath, "pt_");

            if (PartyTemp.Count == GetCountEntries(FilePath))
            {
                foreach (var Party in PartyTemp)
                {
                    string PartyTempID = GetStringArg(Party, 1);

                    string PartyTempName = GetStringArg(Party, 2).Replace("_", " ");

                    if (string.IsNullOrEmpty(PartyTempID) || string.IsNullOrEmpty(PartyTempName))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = PartyTempID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (Party.Start, Party.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(PartyTempName) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = PartyTempID,
                        OriginalText = PartyTempName,
                        Flags        = NewFlags,
                        DataPos      = (Party.Start, Party.End)
                    });
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseQuestsFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var AllQuests = LoadAndParseFile(FilePath, "qst_");

            if (AllQuests.Count == GetCountEntries(FilePath))
            {
                foreach (var Quest in AllQuests)
                {
                    string QuestID = GetStringArg(Quest, 1);

                    string QuestName = GetStringArg(Quest, 2).Replace("_", " ");

                    if (string.IsNullOrEmpty(QuestID) || string.IsNullOrEmpty(QuestName))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = QuestID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (Quest.Start, Quest.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(QuestName) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = QuestID,
                        OriginalText = QuestName,
                        Flags        = NewFlags,
                        DataPos      = (Quest.Start, Quest.End)
                    });

                    string QuestText = GetStringArg(Quest, 3).Replace("_", " ");

                    if (!string.IsNullOrEmpty(QuestText)) // Бывают квесты без текста
                    {
                        NewFlags = IsBlockedLine(QuestText) ? RowFlags.BlockSymbol : RowFlags.None;

                        string NewID = QuestID + "_text";

                        Result.Add(new ModTextRow
                        {
                            RowId        = NewID,
                            OriginalText = QuestText,
                            Flags        = NewFlags,
                            DataPos      = (Quest.Start, Quest.End)
                        });
                    }
                }
                return MarkDuplicateIDs(Result);
            }
            return Result; 
        }

        public static List<ModTextRow> LoadAndParseQuickStringsFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var QuickStrings = LoadAndParseFile(FilePath, "qstr_");

            if (QuickStrings.Count() == GetCountEntries(FilePath))
            {
                foreach (var String in QuickStrings)
                {
                    string StringID = GetAnyArg(String, 1);

                    string StringText = GetAnyArg(String, 2).Replace("_", " ");

                    if (string.IsNullOrEmpty(StringID) || string.IsNullOrEmpty(StringText))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = StringID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (String.Start, String.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(StringText) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = StringID,
                        OriginalText = StringText,
                        Flags        = NewFlags,
                        DataPos      = (String.Start, String.End)
                    });
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseSkillsFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var AllSkills = LoadAndParseFile(FilePath, "skl_");

            if (AllSkills.Count == GetCountEntries(FilePath))
            {
                foreach (var Skill in AllSkills)
                {
                    string SkillID = GetStringArg(Skill, 1);

                    string SkillName = GetStringArg(Skill, 2).Replace("_", " ");

                    string SkillDescription = GetStringArg(Skill, 3).Replace("_", " ");

                    if (string.IsNullOrEmpty(SkillID) || string.IsNullOrEmpty(SkillName) || string.IsNullOrEmpty(SkillDescription))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = SkillID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (Skill.Start, Skill.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(SkillName) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = SkillID,
                        OriginalText = SkillName,
                        Flags        = NewFlags,
                        DataPos      = (Skill.Start, Skill.End)
                    });

                    NewFlags = IsBlockedLine(SkillDescription) ? RowFlags.BlockSymbol : RowFlags.None;

                    string NewID = SkillID + "_desc";

                    Result.Add(new ModTextRow
                    {
                        RowId        = NewID,
                        OriginalText = SkillDescription,
                        Flags        = NewFlags,
                        DataPos      = (Skill.Start, Skill.End)
                    });
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseSkinsFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var AllSkins = LoadAndParseFile(FilePath, "skinkey_");

            if (AllSkins.Count == GetCountEntries(FilePath))
            {
                foreach (var Skin in AllSkins)
                {
                    string SkinID = GetStringArg(Skin, 1);

                    string SkinName = GetStringArg(Skin, 2).Replace("_", " ");

                    if (string.IsNullOrEmpty(SkinID) || string.IsNullOrEmpty(SkinName))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = SkinID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (Skin.Start, Skin.End)
                        });

                        continue;
                    }

                    Result.Add(new ModTextRow
                    {
                        RowId        = SkinID,
                        OriginalText = SkinName,
                        Flags        = RowFlags.None,
                        DataPos      = (Skin.Start, Skin.End)
                    });
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseStringsFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var AllStrings = LoadAndParseFile(FilePath, "str_");

            if (AllStrings.Count == GetCountEntries(FilePath))
            {
                foreach (var String in AllStrings)
                {
                    string StringID = GetAnyArg(String, 1);

                    string StringText = GetAnyArg(String, 2).Replace("_", " ");

                    // Могут быть строки.

                    if (string.IsNullOrEmpty(StringID) || string.IsNullOrEmpty(StringText))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = StringID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (String.Start, String.End)
                        });

                        continue;
                    }

                    var NewFlags = IsBlockedLine(StringText) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = StringID,
                        OriginalText = StringText,
                        Flags        = NewFlags,
                        DataPos      = (String.Start, String.End)
                    });
                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        private static NpcType GetTroopType(string TroopLine)
        {
            var Result = new NpcType();

            if (string.IsNullOrEmpty(TroopLine))
                return Result;

            if (IsLineStartsWithPrefix(TroopLine, "trp_"))
            {
                var TroopFlags = GetIntArg(TroopLine, 2);

                if (TroopFlags != null)
                {
                    var Flags = (TroopsFlags)(TroopFlags);

                    if (Flags.HasFlag(TroopsFlags.tf_man))
                        Result.IsMan = true;

                    if (Flags.HasFlag(TroopsFlags.tf_woman))
                        Result.IsWoman = true;

                    if (Flags.HasFlag(TroopsFlags.tf_hero))
                        Result.IsHero = true;

                    if (Flags.HasFlag(TroopsFlags.tf_is_merchant))
                        Result.IsMerchant = true;

                    if ((TroopFlags & 0x0000000f) >= 2)
                        Result.IsNotHuman = true;

                    // Там ещё куча других флагов, но они не нужны.
                }
            }
            return Result;
        }

        public static List<ModTextRow> LoadAndParseTroopsFile(string FilePath)
        {
            var Result = new List<ModTextRow>();

            if (!File.Exists(FilePath))
                return Result;

            var Troops = LoadAndParseFile(FilePath, ["trp_"]);

            if (Troops.Count() == GetCountEntries(FilePath))
            {
                foreach (var Npc in Troops)
                {
                    // ID, Название, Название (мн.числ), 0, Флаги

                    var TroopID = GetStringArg(Npc, 1);

                    var TroopName = GetStringArg(Npc, 2).Replace("_", " ");

                    var TroopNamePlural = GetStringArg(Npc, 3).Replace("_", " ");

                    if (string.IsNullOrEmpty(TroopID) || string.IsNullOrEmpty(TroopName) || string.IsNullOrEmpty(TroopNamePlural))
                    {
                        Result.Add(new ModTextRow
                        {
                            RowId   = TroopID ?? "ERROR_ID",
                            Flags   = RowFlags.ParseError,
                            DataPos = (Npc.Start, Npc.End)
                        });
                        continue;
                    }

                    RowFlags NewFlags = IsBlockedLine(TroopName) ? RowFlags.BlockSymbol : RowFlags.None;

                    var NpcType = GetTroopType(Npc);

                    Result.Add(new ModTextRow
                    {
                        RowId        = TroopID,
                        OriginalText = TroopName,
                        NPC          = NpcType,
                        Flags        = NewFlags,
                        DataPos      = (Npc.Start, Npc.End)
                    });

                    string NewTroopID = TroopID + "_pl";

                    var PluralFlags = IsBlockedLine(TroopNamePlural) ? RowFlags.BlockSymbol : RowFlags.None;

                    Result.Add(new ModTextRow
                    {
                        RowId        = NewTroopID,
                        OriginalText = TroopNamePlural,
                        NPC          = NpcType,
                        Flags        = PluralFlags,
                        DataPos      = (Npc.Start, Npc.End)
                    });

                }
                return MarkDuplicateIDs(Result);
            }
            return Result;
        }

        /// <summary>
        /// Возвращает разницу между новым и старым списком переводов.
        /// </summary>
        /// <param name="NewRows">Новый перевод</param>
        /// <param name="OldRows">Старый перевод</param>
        /// <returns></returns>
        public static List<ModTextRow> GetModTextChanges(List<ModTextRow> NewRows, List<ModTextRow> OldRows, StringComparison CompareType = StringComparison.OrdinalIgnoreCase)
        {
            var Result = new List<ModTextRow>();

            foreach (var RowData in NewRows)
            {
                var FoundRow = OldRows.FirstOrDefault(x => x.RowId == RowData.RowId); // Ищем в старом переводе строки ID

                if (FoundRow == null) // Если их нет, то значит это новые строки
                {
                    Result.Add(RowData); // добавляем в список.
                }
                else
                {
                    if (!string.Equals(RowData.OriginalText, FoundRow.OriginalText, CompareType)) // Сравниваем оригинальные тексты.
                    {
                        Result.Add(RowData); // Если отличаются, то добавляем.
                    }
                }
            }
            return Result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="TextData"></param>
        /// <param name="StartIndex"></param>
        /// <param name="OldLength">Длина ЗАМЕНЯЕМОЙ строки (не NewWord)</param>
        /// <param name="NewWord"></param>
        /// <returns></returns>
        private static string ReplaceWordByIndex(string TextData, int StartIndex, int OldLength, string NewWord)
        {
            if (TextData == null || NewWord == null)
                return string.Empty;

            if (StartIndex < 0 || StartIndex > TextData.Length) // Если индекс выходит за пределы строки
                return string.Empty;

            if (OldLength < 0 || StartIndex + OldLength > TextData.Length) // Длина выходит за пределы строки.
                return string.Empty;

            try
            {
                string Before = TextData.Substring(0, StartIndex);

                string After = TextData.Substring(StartIndex + OldLength);

                return Before + NewWord + After;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static int GetMaxNumSuffix(List<ModTextRow> Data, string RowId, string Suffix)
        {
            int MaxSuffixNumber = 0;

            string Pattern = $@"{RowId}{Suffix}(\d+)"; //

            var Reg = new Regex(Pattern, RegexOptions.Compiled);

            foreach (var Row in Data)
            {
                var Match = Reg.Match(Row.RowId);

                if (Match.Success)
                {
                    var CurrentNumber = int.Parse(Match.Groups[1].Value);

                    if (CurrentNumber > MaxSuffixNumber)
                        MaxSuffixNumber = CurrentNumber;
                }
            }
            return MaxSuffixNumber;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="MenuFile"></param>
        /// <param name="ParsedMenu"></param>
        /// <param name="Suffix"></param>
        /// <param name="RenamedIds"></param>
        /// <param name="Flags"></param>
        /// <returns></returns>
        public static string ReCreateMenuFile(string MenuFile, List<ModTextRow> ParsedMenu, string Suffix, out int RenamedIds, RowFlags Flags)
        {
            RenamedIds = 0;

            if (!File.Exists(MenuFile) || string.IsNullOrEmpty(Suffix))
                return string.Empty;

            if (ParsedMenu.Count > 0)
            {
                var ResultMenu = EncodingText.ReadTextFileAndConvertTo(MenuFile, Encoding.Unicode);

                if (!string.IsNullOrEmpty(ResultMenu))
                {
                    var RelIndex = 0;

                    var WorkedIds = new Dictionary<string, int>(); // id:количество вхождений

                    foreach (var Item in ParsedMenu)
                    {
                        var PosStart = Item.DataPos.Start + RelIndex;

                        if (Item.Flags.HasFlag(Flags)) // Только дубликаты
                        {
                            if (IsLineStartsWithPrefix(Item.RowId, "mno_")) // и подменю.
                            {
                                if (!WorkedIds.ContainsKey(Item.RowId))
                                {
                                    var MaxNum = GetMaxNumSuffix(ParsedMenu, Item.RowId, Suffix);

                                    WorkedIds.Add(Item.RowId, MaxNum);
                                }

                                var CurrentEnter = WorkedIds[Item.RowId]; // Количество вхождений id

                                var NewSuffix = $"{Suffix}{CurrentEnter + 1}";

                                var IdWithSuffix = Item.RowId + NewSuffix; // Пример: mno_continue.wt1

                                ResultMenu = ReplaceWordByIndex(ResultMenu, PosStart, Item.RowId.Length, IdWithSuffix);

                                if (ResultMenu == null)
                                    return string.Empty;

                                RelIndex += NewSuffix.Length; // Каждая замена строки увеличивает результат на длину суффикса.

                                WorkedIds[Item.RowId] = CurrentEnter + 1;

                                RenamedIds++;
                            }
                        }
                    }
                }
                return ResultMenu;
            }
            return string.Empty;
        }

        public static string ReCreateMenuFile(string MenuFile, string Suffix, out int RenamedIds, RowFlags Flags)
        {
            RenamedIds = 0;

            if (!File.Exists(MenuFile) || string.IsNullOrEmpty(Suffix))
                return string.Empty;

            var ParsedMenu = LoadAndParseMenuFile(MenuFile);

            if (ParsedMenu != null)
                return ReCreateMenuFile(MenuFile, ParsedMenu, Suffix, out RenamedIds, Flags);

            return string.Empty;
        }

    }
}
