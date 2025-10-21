/*
 * Надоб лучше найти какую-нибудь нормальную библиотеку, а не использовать это...
 */
using System.IO;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace EncodingTextFile
{
    public static class EncodingText
    {
        static bool IsAscii(char Char)
        {
            return Char >= 0 && Char <= 127;
        }

        static bool IsUnicodeChar(char Char)
        {
            return !char.IsSurrogate(Char);
        }

        public static Encoding? DetectEncoding(byte[] Data)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (Data == null || Data.Length == 0)
                return null;

            var BomEncoding = DetectEncodingFromBom(Data);

            if (BomEncoding != null)
                return BomEncoding;

            if (IsValidUtf8(Data))
                return Encoding.UTF8;

            if (IsValidEncoding(Data, 1251))
                return Encoding.GetEncoding(1251);

            if (IsValidEncoding(Data, 28591))
                return Encoding.GetEncoding(28591);

            if (IsValidAscii(Data))
                return Encoding.ASCII;

            return null;
        }

        private static Encoding? DetectEncodingFromBom(byte[] Bytes)
        {
            if (Bytes == null || Bytes.Length < 2)
                return null;

            if (Bytes.Length >= 3 && Bytes[0] == 0xEF && Bytes[1] == 0xBB && Bytes[2] == 0xBF)
            {
                return Encoding.UTF8;
            }

            if (Bytes.Length >= 2 && Bytes[0] == 0xFE && Bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode; // UTF-16 Big Endian
            }

            if (Bytes.Length >= 2 && Bytes[0] == 0xFF && Bytes[1] == 0xFE)
            {
                if (Bytes.Length >= 4 && Bytes[2] == 0x00 && Bytes[3] == 0x00) // Проверяем, не UTF-32 Little Endian ли это
                {
                    return Encoding.UTF32;
                }
                return Encoding.Unicode; // UTF-16 Little Endian
            }

            if (Bytes.Length >= 4 && Bytes[0] == 0x00 && Bytes[1] == 0x00 && Bytes[2] == 0xFE && Bytes[3] == 0xFF)
            {
                return new UTF32Encoding(true, true); // UTF-32 Big Endian
            }

            return null;
        }

        // Бом настолько полезен, что его приходится удалять, чтоб не искажал текст при конвертации.
        private static byte[] DeleteBom(byte[] Data, out bool Deleted)
        {
            Deleted = false;

            var Encode = DetectEncodingFromBom(Data);

            if (Encode != null)
            {
                byte[] Bom = Encode.GetPreamble();

                if (Bom.Length > 0)
                {
                    Deleted = false;

                    return Data.AsSpan(Bom.Length).ToArray();
                }
            }
            return Data;
        }

        private static bool IsValidAscii(byte[] Data)
        {
            foreach (byte Char in Data)
            {
                if (!IsAscii((char)Char))
                    return false;
            }
            return true;
        }

        private static bool IsValidUtf8(byte[] Data)
        {
            try
            {
                var Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

                Utf8.GetString(Data);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidEncoding(byte[] Data, int CodePage)
        {
            try
            {
                var Result = Encoding.GetEncoding(CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

                Result.GetString(Data);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidEncoding(byte[] Data, string CodePageName)
        {
            try
            {
                var Encode = Encoding.GetEncoding(CodePageName);

                if (Encode != null)
                    return IsValidEncoding(Data, Encode.CodePage);
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }

        /*
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
        */

        public static string ReadTextFileAndConvertTo(string FilePath, Encoding ConvertTo)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                var Buffer = File.ReadAllBytes(FilePath);

                if (Buffer.Length > 0)
                {
                    var EncodingPage = DetectEncoding(Buffer);

                    if (EncodingPage != null)
                    {
                        var Result = Encoding.Convert(EncodingPage, ConvertTo, DeleteBom(Buffer, out _));

                        return ConvertTo.GetString(Result);
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
            return string.Empty;
        }

        public static string[] ReadAllLinesAndConvertTo(string FilePath, Encoding ConvertTo)
        {
            if (File.Exists(FilePath))
            {
                var Text = ReadTextFileAndConvertTo(FilePath, ConvertTo);

                try
                {
                    if (!string.IsNullOrEmpty(Text))
                    {
                        var Result = Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                        return Result;
                    }
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }
            return Array.Empty<string>();
        }

        public static Encoding? GetTextFileEncoding(string FilePath)
        {
            return null;
        }

    }
}