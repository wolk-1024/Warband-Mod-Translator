namespace WarbandParser
{
    [Flags]
    public enum RowFlags
    {
        None = 0,
        Dublicate = 1,
        BlockSymbol = 2,
        DublicateDifferentValue = 4,
        ParseError = 8
    }

    /// <summary>
    /// Класс для биндинга с таблицей
    /// </summary>
    public class ModTextRow : ModRowInfo
    {
        //
        public int RowNum            { get; set; } = 0;
        public string RowId          { get; set; } = string.Empty;
        public string OriginalText   { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
        //
        public NpcType? NPC          { get; set; } = null;
        public WhoTalking? Dialogue  { get; set; } = null;

        public ModTextRow Clone()
        {
            return new ModTextRow
            {
                RowId          = new string(this.RowId),
                OriginalText   = new string(this.OriginalText),
                TranslatedText = new string(TranslatedText),
                Flags          = this.Flags,
                DataPos        = this.DataPos,
                RawLine        = new string(this.RawLine),
                NPC            = this.NPC?.Clone(),
                Dialogue       = this.Dialogue?.Clone() 
            };
        }
    }

    public class ModRowInfo
    {
        public RowFlags Flags { get; set; } = RowFlags.None;

        public string RawLine { get; set; } = string.Empty;

        public (int Start, int End) DataPos { get; set; } = (-1, -1); // Позиция данных в оригинальном файле. Используется пока только для menus.txt
    }

    public class NpcType
    {
        public bool IsMan      = false;  // По умолчанию нпс обычно мужик
        public bool IsWoman    = false;  // Нпс-женщина
        public bool IsOther    = false;  // Нпс не человек, а например гуль или робот
        public bool IsHero     = false;  // Нпс-герой
        public bool IsMerchant = false;  // Нпс-торговец

        public string ID         = string.Empty; // Id в войсках / группах
        public string Name       = string.Empty; // Имя нпс в ед.числе
        public string NamePlural = string.Empty; // Имя нпс в мн.числе род.падеже. (обычно)

        public NpcType Clone()
        {
            return new NpcType
            {
                IsMan      = this.IsMan,
                IsWoman    = this.IsWoman,
                IsOther    = this.IsOther,
                IsHero     = this.IsHero,
                ID         = new string(this.ID),
                Name       = new string(this.Name),
                NamePlural = new string(this.NamePlural)
            };
        }
    }

    /// <summary>
    /// Флаги из header_troops.py
    /// </summary>
    [Flags]
    public enum TroopsFlags
    {
        //tf_man = 0,                //
        //tf_woman = 1,              //
        tf_hero = 0x00000010,        //
        tf_is_merchant = 0x00001000, //
    }

    /// <summary>
    /// headers_dialogs.py
    /// </summary>
    [Flags]
    public enum DialogStates
    {
        anyone = 0x00000fff,
        plyr = 0x00010000,
        party_tpl = 0x00020000
    }

    public class WhoTalking
    {
        public bool IsPlayer = false;
        public bool IsAnyone = false;
        public bool IsParty  = false;
        public bool IsNpc    = false;

        public NpcType TalkingWith = new NpcType();

        public WhoTalking Clone()
        {
            return new WhoTalking
            {
                IsPlayer    = this.IsPlayer,
                IsAnyone    = this.IsAnyone,
                IsParty     = this.IsParty,
                IsNpc       = this.IsNpc,
                TalkingWith = this.TalkingWith.Clone()
            };
        }
    }

    public class DialogLine
    {
        public string ID = "PARSER_ERROR";
        public string Text = "PARSER_ERROR";

        public WhoTalking? WhoIs = null;
    }

    public class ParseArg
    {
        public string Value { get; set; } = string.Empty;

        public int Start    { get; set; } = -1;
        public int End      { get; set; } = -1;

        public ParseArg(string ArgVal)
        {
            Value = ArgVal ?? string.Empty;
        }

        public ParseArg(string RawVal, int RawStart = -1, int RawEnd = -1)
        {
            Value = RawVal ?? string.Empty;

            Start = RawStart;

            End = RawEnd;
        }

        public ParseArg() { }

        public static implicit operator ParseArg(string ArgVal) { return new ParseArg(ArgVal); }

        public static implicit operator string(ParseArg Wrapper) { return Wrapper?.Value ?? string.Empty; }

        public override string ToString() { return Value; }
    }

    public class OriginalFileHeader
    {
        public string Name = string.Empty;

        public int Version = 1;

        public int Entries = -1;
    }
}
