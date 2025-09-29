/*
 * Надоб лучше найти какую-нибудь нормальную библиотеку, а не использовать это...
 */
using System.IO;
using System.Text;

static class EncodingTextFile
{
    private static bool IsAscii(char Char)
    {
        return Char >= 0 && Char <= 127;
    }

    private static bool IsUnicodeChar(char Char)
    {
        return !char.IsSurrogate(Char);
    }

    public static bool TextContainsReplacementChar(string TextData)
    {
        return TextData.Any(c => c == 0xFFFD);
    }

    public static int DetectEncoding(byte[] TextBytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        if (IsUtf8WithBom(TextBytes))
            return 65001;

        if (IsUtf8WithoutBom(TextBytes))
            return 65001;

        if (IsWindows1251(TextBytes))
            return 1251;

        return -1;
    }

    private static bool IsUtf8WithoutBom(byte[] TextBytes)
    {
        try
        {
            Encoding UTF8 = new UTF8Encoding(false, true);

            string Decoded = UTF8.GetString(TextBytes);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWindows1251(byte[] TextData)
    {
        try
        {
            Encoding Encoding1251 = Encoding.GetEncoding(1251);

            string DecodedText = Encoding1251.GetString(TextData);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUtf8WithBom(byte[] Bytes)
    {
        return Bytes.Length >= 3 &&
            Bytes[0] == 0xEF &&
            Bytes[1] == 0xBB &&
            Bytes[2] == 0xBF;
    }

    public static bool IsValidEncode(byte[] ByteText, Encoding Code)
    {
        try
        {
            var Text = Code.GetString(ByteText);

            return Code.GetBytes(Text).SequenceEqual(ByteText);
        }
        catch
        {
            return false;
        }
    }

    public static string ReadTextFileAndConvertTo(string FilePath, Encoding ConvertTo)
    {
        var Buffer = File.ReadAllBytes(FilePath);

        if (Buffer.Length > 0)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var EncodingPage = DetectEncoding(Buffer);

            if (EncodingPage != -1)
            {
                var Encode = Encoding.GetEncoding(EncodingPage);

                var Result = Encoding.Convert(Encode, Encoding.Unicode, Buffer);

                return ConvertTo.GetString(Result);
            }
        }
        return string.Empty;
    }

    public static int GetTextFileEncoding(string FilePath)
    {
        //
        return -1;
    }

}
