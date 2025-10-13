/*
 *  Парсер v08.10.2025
 *  
 *  Известные проблемы: 
 *  
 *  Код игнорует численные параметры, т.к я не знаю, как отличить их от мусора (например p_bridge_30 30)
 *  Так же игнорируются значения, полностью состоящие из пробелов, знаков препинания и пустых строк. (например qstr_____ ____)
 *  Не обрабатываются у id слитные конструкции типа: qstr_{!}blabla
 *  
 *  Тем не менее, пока этого достаточно для перевода 99% контента.
 */

using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using EncodingTextFile;

namespace WarbandParser
{
    public static class Parser
    {
        /// <summary>
        /// Удалять дубликаты Id.
        /// </summary>
        public static bool g_DeleteDublicatesIDs = true;

        /// <summary>
        /// Регулярка для поиска знаков и цифр.
        /// </summary>
        private static readonly Regex RegSymbolsAndNumbers = new Regex(@"^[^\p{L}]*$", RegexOptions.Compiled);

        /// <summary>
        /// Регулярка для слов и знаков
        /// </summary>
        private static readonly Regex RegWordsAndSymbols = new Regex(@"^(?=.*\p{L})[\p{L}\p{N}\p{P}\p{S}]+$", RegexOptions.Compiled);

        /// <summary>
        /// Игнорирование {!} в строках. Не трогать.
        /// </summary>
        private static bool g_IgnoreBlockingSymbol = false;

        private readonly static string[] IdPrefixesList =
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

        public class ModTextRow
        {
            public string RowNum { get; set; } = string.Empty;

            public string RowId { get; set; } = string.Empty;

            public string OriginalText { get; set; } = string.Empty;

            public string TranslatedText { get; set; } = string.Empty;
        }


        public static bool IsLineStartsWithPrefix(string Input)
        {
            if (!string.IsNullOrEmpty(Input))
            {
                foreach (string Prefix in IdPrefixesList)
                {
                    if (Input.StartsWith(Prefix) && Input.EndsWith(string.Empty))
                        return true;
                }
            }
            return false;
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

        public static bool IsLineСontainsPrefix(string Input)
        {
            if (!string.IsNullOrEmpty(Input))
            {
                foreach (string Prefix in IdPrefixesList)
                {
                    if (Input.IndexOf(Prefix) >= 0 && Input.EndsWith(string.Empty))
                        return true;
                }
            }
            return false;
        }

        /*
        public static string RemoveAllNumbers(string Input)
        {
            try
            {
                string Pattern = @"(?<!\S)-?\d*\.?\d+(?!\S)";

                string Result = Regex.Replace(Input, Pattern, string.Empty);

                Result = Regex.Replace(Result, @"\s+", " ").Trim();

                return Result;
            }
            catch (Exception)
            {
                return "";
            }
        }
        */

        private static bool TextStartWithError(string Input)
        {
            if (g_IgnoreBlockingSymbol == true)
                return false;

            if (!string.IsNullOrEmpty(Input))
            {
                var Result = Input.TrimStart(); // Иногда блок стоит не в самом конце, а его разделяют пробелы.

                return Result.StartsWith("{!}", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static string RemoveWordFromStart(string Input, string WordToRemove)
        {
            if (string.IsNullOrEmpty(Input) || string.IsNullOrEmpty(WordToRemove))
                return Input;

            if (Input.StartsWith(WordToRemove))
                return Input.Substring(WordToRemove.Length);
            else
                return Input;
        }

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

        public static List<ModTextRow> UpdateNumber(List<ModTextRow> Data)
        {
            int Count = 1;

            foreach (var Res in Data)
                Res.RowNum = Count++.ToString();

            return Data;
        }

        public static List<ModTextRow> RemoveDuplicateIDs(List<ModTextRow> Data, out int DuplicatesRemoved)
        {
            DuplicatesRemoved = 0;

            var Result = new List<ModTextRow>();

            var GroupedData = Data.GroupBy(x => x.RowId);

            foreach (var Group in GroupedData)
            {
                int GroupCount = Group.Count();

                DuplicatesRemoved += GroupCount - 1;

                var PreferredItem = Group.FirstOrDefault(x => !string.IsNullOrEmpty(x.OriginalText));

                if (PreferredItem == null)
                {
                    PreferredItem = Group.First();
                }
                Result.Add(PreferredItem);
            }
            return Result;
        }

        private static bool IsWordCharacter(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static int GetPrefixEnter(string TextData, int StartPos, string Prefix)
        {
            if (string.IsNullOrEmpty(TextData) || StartPos < 0 || string.IsNullOrEmpty(Prefix))
                return -1;

            int CurrentPos = StartPos;

            while (CurrentPos < TextData.Length) // Пока позиция в строке меньше, чем её длина.
            {
                int EnterPos = TextData.IndexOf(Prefix, CurrentPos, StringComparison.Ordinal); // Ищем вхождение префикса

                if (EnterPos == 0) // Если нашли префикс в самом начале, то
                    return EnterPos; // выходим с результатом.

                if (EnterPos == -1) // Если ничего не нашли, то
                    return -1; // просто выходим с ошибкой.

                if (!IsWordCharacter(TextData[EnterPos - 1])) // Проверяем предыдущий символ от найденного префикса на наличие символов. Если не пустой, то это ложный префикс.
                    return EnterPos; // Если пусто, то всё хорошо

                CurrentPos = EnterPos + Prefix.Length; // Текущая позиция = индекс найденного префикса + длина префикса
            }
            return -1;
        }

        private static List<string> ParseTextData(string TextData, string Prefix)
        {
            var Result = new List<string> { };

            if (string.IsNullOrEmpty(TextData))
                return Result;

            if (string.IsNullOrEmpty(Prefix))
                return Result;

            if (TextData.Length > 0)
            {
                int CurrentPosition = 0; // Текущая позиция в TextData

                while (CurrentPosition < TextData.Length)
                {
                    int EnterPos = GetPrefixEnter(TextData, CurrentPosition, Prefix); // Первое вхождения префикса

                    if (EnterPos == -1)
                        break;

                    int EndPos = GetPrefixEnter(TextData, EnterPos + Prefix.Length, Prefix); // Ищем начало второго вхождения.

                    if (EndPos == -1) // Если его нет, но есть первое, то
                        EndPos = TextData.Length; // мы в конце данных.

                    var ResultString = TextData.Substring(EnterPos, EndPos - EnterPos).Trim(); // Извлекаем строку и удалем пробелы.

                    if (ResultString != string.Empty)
                    {
                        if (IsLineStartsWithPrefix(ResultString))
                        {
                            Result.Add(ResultString);
                        }
                    }
                    CurrentPosition = EndPos;
                }
            }
            return Result;
        }

        private static List<string> LoadAndParseFile(string FilePath, string Prefix)
        {
            var Result = new List<string>();

            if (File.Exists(FilePath))
            {
                string TextData = EncodingText.ReadTextFileAndConvertTo(FilePath, Encoding.Unicode);

                //string TextData = File.ReadAllText(FilePath, Encoding.UTF8);

                if (TextData.Length > 0)
                    Result = ParseTextData(TextData, Prefix);
            }
            return Result;
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

        public static List<ModTextRow>? LoadAndParseConversationFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "dlga_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var InfoLine in ModText)
                {
                    var DlgaArgs = GetModTextArgs(InfoLine, "dlga_", 2);

                    if (DlgaArgs.Count < 2)
                        continue;

                    string LineID = DlgaArgs[0];

                    if (!IsLineStartsWithPrefix(LineID))
                        return null;

                    string OriginalText = DlgaArgs[1].Replace("_", " ");

                    if (OriginalText == "NO VOICEOVER")
                        continue;

                    if (!TextStartWithError(OriginalText))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = LineID,
                                OriginalText = OriginalText
                            });
                    }

                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }

            return null;
        }

        public static List<ModTextRow>? LoadAndParseFactionsFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "fac_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var FacLine in ModText)
                {
                    var FacArgs = GetModTextArgs(FacLine, "fac_", 2);

                    if (FacArgs.Count < 2)
                        continue;

                    string LineID = FacArgs[0];

                    if (!IsLineStartsWithPrefix(LineID))
                        return null;

                    string OriginalText = FacArgs[1].Replace("_", " ");

                    OriginalText = RemoveWordFromStart(OriginalText, "{!}"); // Игра вроде как игнорирует блок.символ в фракциях. Поэтому удаляем.

                    ModTextResult.Add(
                        new ModTextRow
                        {
                            RowId = LineID,
                            OriginalText = OriginalText
                        });
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        public static List<ModTextRow>? LoadAndParseInfoPagesFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "ip_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var InfoLine in ModText)
                {
                    var InfoArgs = GetModTextArgs(InfoLine, "ip_", 3);

                    if (InfoArgs.Count < 3) //
                        continue;

                    string OldID = InfoArgs[0];

                    if (!IsLineStartsWithPrefix(OldID))
                        return null;

                    string IpValue = InfoArgs[1].Replace("_", " ");

                    string IpText = InfoArgs[2].Replace("_", " ");

                    string NewID = OldID + "_text"; // ip_name + _text

                    if (!TextStartWithError(IpValue))
                    {
                        ModTextResult.Add(
                            new ModTextRow{
                                RowId = OldID, 
                                OriginalText = IpValue
                            });

                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = NewID,
                                OriginalText = IpText
                            });
                    }
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        public static List<ModTextRow>? LoadAndParseItemKindsFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "itm_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var ItemLine in ModText)
                {
                    var ItemArgs = GetModTextArgs(ItemLine, "itm_", 3);

                    if (ItemArgs.Count < 3) //
                        continue;

                    string OldID = ItemArgs[0];

                    if (!IsLineStartsWithPrefix(OldID))
                        return null;

                    string ItemName = ItemArgs[1].Replace("_", " ");

                    string ItemNamePlural = ItemArgs[2].Replace("_", " ");

                    string NewID = OldID + "_pl";

                    //ItemNamePlural += " (plural)";

                    if (!TextStartWithError(ItemName))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = OldID,
                                OriginalText = ItemName
                            });

                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = NewID,
                                OriginalText = ItemNamePlural
                            });
                    }
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        public static List<ModTextRow>? LoadAndParseItemModifiersFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "imod_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var ItemModLine in ModText)
                {
                    var ItemModArgs = GetModTextArgs(ItemModLine, "imod_", 2);

                    if (ItemModArgs.Count < 2) //
                        continue;

                    string LineID = ItemModArgs[0];

                    if (!IsLineStartsWithPrefix(LineID))
                        return null;

                    string OriginalText = ItemModArgs[1].Replace("_", " ");

                    if (!TextStartWithError(OriginalText))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = LineID,
                                OriginalText = OriginalText
                            });
                    }
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        /// <summary>
        /// Есть проблемы: в menus.txt бывают подменю mno_ дубликаты, но с разными значениями.
        /// </summary>
        /// <param name="FilePath"></param>
        /// <returns></returns>
        public static List<ModTextRow>? LoadAndParseMenuFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "menu_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var FullMenuLine in ModText)
                {
                    var MenuArgs = GetModTextArgs(FullMenuLine, "menu_", 2);

                    if (MenuArgs.Count < 2)
                        continue;

                    var MenuID = MenuArgs[0];

                    if (!IsLineStartsWithPrefix(MenuID))
                        return null;

                    var MenuText = MenuArgs[1].Replace('_', ' ');

                    if (!TextStartWithError(MenuText))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = MenuID,
                                OriginalText = MenuText
                            });
                    }

                    var PartsOfMenu = ParseTextData(FullMenuLine, "mno_");

                    if (PartsOfMenu.Count > 0)
                    {
                        var MnoList = new List<ModTextRow>();

                        foreach (var PartMenuLine in PartsOfMenu)
                        {
                            var PartMenuArgs = GetModTextArgs(PartMenuLine, "mno_", 2);

                            if (PartMenuArgs.Count < 2) //
                                continue;

                            var PartID = PartMenuArgs[0];

                            if (!IsLineStartsWithPrefix(PartID))
                                 return null;

                            var PartText = PartMenuArgs[1].Replace('_', ' ');

                            if (!TextStartWithError(PartText))
                            {
                                MnoList.Add(
                                    new ModTextRow
                                    {
                                        RowId = PartID,
                                        OriginalText = PartText
                                    });
                            }
                        }
                        // Где-то тут должна быть работа с дубликатами подменю.
                        ModTextResult.AddRange(MnoList);
                    }
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        public static List<ModTextRow>? LoadAndParsePartiesFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "p_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var PartiesLine in ModText)
                {
                    var PartiesArgs = GetModTextArgs(PartiesLine, "p_", 2);

                    if (PartiesArgs.Count < 2) //
                        continue;

                    string ID = PartiesArgs[0];

                    if (!IsLineStartsWithPrefix(ID))
                        return null;

                    string OriginalText = PartiesArgs[1].Replace("_", " ");

                    if (!TextStartWithError(OriginalText))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = ID,
                                OriginalText = OriginalText
                            });
                    }
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        public static List<ModTextRow>? LoadAndParsePartyTemplatesFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "pt_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var PartiesLine in ModText)
                {
                    var PartyTempArgs = GetModTextArgs(PartiesLine, "pt_", 2);

                    if (PartyTempArgs.Count < 2) //
                        continue;

                    string LineID = PartyTempArgs[0];

                    if (!IsLineStartsWithPrefix(LineID))
                        return null;

                    string OriginalText = PartyTempArgs[1].Replace("_", " ");

                    if (!TextStartWithError(OriginalText))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = LineID,
                                OriginalText = OriginalText
                            });
                    }
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        public static List<ModTextRow>? LoadAndParseQuestsFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "qst_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var QuestLine in ModText)
                {
                    var QuestArgs = GetModTextArgs(QuestLine, "qst_", 3);

                    if (QuestArgs.Count < 3) //
                        continue;

                    string OldID = QuestArgs[0];

                    if (!IsLineStartsWithPrefix(OldID))
                        return null;

                    string Quest = QuestArgs[1].Replace("_", " ");

                    string QuestText = QuestArgs[2].Replace("_", " ");

                    string NewID = OldID + "_text";

                    if (!TextStartWithError(Quest))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = OldID,
                                OriginalText = Quest
                            });
                    }

                    if (!TextStartWithError(QuestText))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = NewID,
                                OriginalText = QuestText
                            });
                    }
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        public static List<ModTextRow>? LoadAndParseQuickStringsFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "qstr_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var QuickLine in ModText)
                {
                    var QuickArgs = GetModTextArgs(QuickLine, "qstr_", 2);

                    if (QuickArgs.Count < 2) //
                        continue;

                    string LineID = QuickArgs[0];

                    if (!IsLineStartsWithPrefix(LineID))
                        return null;

                    string OriginalText = QuickArgs[1].Replace("_", " ");

                    if (!TextStartWithError(OriginalText))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = LineID,
                                OriginalText = OriginalText
                            });
                    }
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        public static List<ModTextRow>? LoadAndParseSkillsFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "skl_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var SkillLine in ModText)
                {
                    var SkillArgs = GetModTextArgs(SkillLine, "skl_", 3);

                    if (SkillArgs.Count < 3) //
                        continue;

                    string OldID = SkillArgs[0];

                    if (!IsLineStartsWithPrefix(OldID))
                        return null;

                    string SkillName = SkillArgs[1].Replace("_", " ");

                    string SkillDescription = SkillArgs[2].Replace("_", " ");

                    string NewID = OldID + "_desc";

                    // Нужны проверки на блок?

                    ModTextResult.Add(
                        new ModTextRow
                        {
                            RowId = OldID,
                            OriginalText = SkillName
                        });

                    ModTextResult.Add(
                        new ModTextRow
                        {
                            RowId = NewID,
                            OriginalText = SkillDescription
                        });

                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }

            return null;
        }

        public static List<ModTextRow>? LoadAndParseSkinsFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "skinkey_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var SkinsLine in ModText)
                {
                    var SkinsArgs = GetModTextArgs(SkinsLine, "skinkey_", 2);

                    if (SkinsArgs.Count < 2) //
                        continue;

                    string LineID = SkinsArgs[0];

                    if (!IsLineStartsWithPrefix(LineID))
                        return null;

                    string Skins = SkinsArgs[1].Replace("_", " ");

                    ModTextResult.Add(
                        new ModTextRow
                        {
                            RowId = LineID,
                            OriginalText = Skins
                        });
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        public static List<ModTextRow>? LoadAndParseStringsFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "str_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var StrLine in ModText)
                {
                    var StrArgs = GetModTextArgs(StrLine, "str_", 2);

                    if (StrArgs.Count < 2) //
                        continue;

                    string LineID = StrArgs[0];

                    if (!IsLineStartsWithPrefix(LineID))
                        return null;

                    string OriginalText = StrArgs[1].Replace("_", " ");

                    if (!TextStartWithError(OriginalText))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = LineID,
                                OriginalText = OriginalText
                            });
                    }
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        public static List<ModTextRow>? LoadAndParseTroopsFile(string FilePath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return null;

            var ModText = LoadAndParseFile(FilePath, "trp_");

            if (ModText.Count > 0)
            {
                var ModTextResult = new List<ModTextRow>();

                foreach (var TroopsLine in ModText)
                {
                    var TroopsArgs = GetModTextArgs(TroopsLine, "trp_", 3);

                    if (TroopsArgs.Count < 3) //
                        continue;

                    string OldID = TroopsArgs[0];

                    if (!IsLineStartsWithPrefix(OldID))
                        return null;

                    string TroopsName = TroopsArgs[1].Replace("_", " ");

                    string TroopsNamePlural = TroopsArgs[2].Replace("_", " ");

                    string NewID = OldID + "_pl";

                    //TroopsNamePlural += " (plural)";

                    if (!TextStartWithError(TroopsName))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = OldID,
                                OriginalText = TroopsName
                            });
                    }

                    if (!TextStartWithError(TroopsNamePlural))
                    {
                        ModTextResult.Add(
                            new ModTextRow
                            {
                                RowId = NewID,
                                OriginalText = TroopsNamePlural
                            });
                    }
                }
                if (g_DeleteDublicatesIDs)
                {
                    int RemovedIds;

                    var Result = RemoveDuplicateIDs(ModTextResult, out RemovedIds);

                    return UpdateNumber(Result);
                }
                return UpdateNumber(ModTextResult);
            }
            return null;
        }

        /// <summary>
        /// Возвращает разницу между новым и старым списком переводов.
        /// </summary>
        /// <param name="NewRows">Новый перевод</param>
        /// <param name="OldRows">Старый перевод</param>
        /// <returns></returns>
        public static List<ModTextRow> GetModTextChanges(List<ModTextRow> NewRows, List<ModTextRow> OldRows, StringComparison CompareType = StringComparison.OrdinalIgnoreCase)
        {
            var Result = new List<ModTextRow> { };

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
            return UpdateNumber(Result);
        }

        public static bool IsNumber(string Input)
        {
            if (!string.IsNullOrWhiteSpace(Input))
            {
                Input = Input.Trim();

                if (double.TryParse(Input, out _))
                    return true;
            }
            return false;
        }

        private static bool IsSelectorLine(string Input)
        {
            //
            return false;
        }

        /// <summary>
        /// Функция возвращает только строковые параметры, численные и знаковые значения не обрабатываются..
        /// </summary>
        /// <param name="Data"></param>
        /// <param name="Prefix"></param>
        /// <param name="MaxArgs"></param>
        /// <returns></returns>
        private static List<string> GetModTextArgs(string Data, string Prefix, long MaxArgs = 2)
        {
            var Result = new List<string> { };

            if (string.IsNullOrEmpty(Data) || string.IsNullOrEmpty(Prefix))
                return Result;

            long FoundedArgs = 0;

            bool FoundedPrefix = false;

            var SplitData = Data.Split(" ", StringSplitOptions.RemoveEmptyEntries);

            foreach (var Line in SplitData)
            {
                if (FoundedArgs >= MaxArgs)
                    break;

                if (IsLineStartsWithPrefix(Line, Prefix)) // Если id первый параметр
                {
                    if (FoundedPrefix) // Если ещё один префикс, то ошибка.
                    {
                        Result.Clear();

                        break;
                    }
                    else
                    {
                        Result.Add(Line);

                        FoundedArgs++;

                        FoundedPrefix = true;
                    }
                }
                else if (RegSymbolsAndNumbers.IsMatch(Line)) // Если одни знаки и цифры.
                {
                    if (TextStartWithError(Line))
                    {
                        Result.Add(Line);

                        FoundedArgs++;
                    }

                    continue;
                }
                else if (RegWordsAndSymbols.IsMatch(Line)) // Если в строке есть юникод-буквы и знаки.
                {
                    Result.Add(Line);

                    FoundedArgs++;
                }
            }
            return Result;
        }

    }
}