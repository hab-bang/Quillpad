// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System.Text;

    /// <summary>
    /// Checks if an encoding can hold a text. A code page like Windows-1252 has no Korean or emoji,
    /// and Encoding.GetBytes writes each such character as "?" without an error.
    /// </summary>
    public static class EncodingFitUtility
    {
        /// <summary>
        /// The index of the first character that <paramref name="encoding"/> cannot hold, or -1 when it holds them all.
        /// </summary>
        public static int FindFirstUnencodableIndex(string text, Encoding encoding)
        {
            if (string.IsNullOrEmpty(text) || encoding == null || IsUnicode(encoding)) return -1;

            var strictEncoding = (Encoding)encoding.Clone();
            strictEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;

            try
            {
                strictEncoding.GetByteCount(text);
                return -1;
            }
            catch (EncoderFallbackException ex)
            {
                return ex.Index;
            }
        }

        public static bool CanEncode(string text, Encoding encoding)
        {
            return FindFirstUnencodableIndex(text, encoding) < 0;
        }

        /// <summary>
        /// The character at <paramref name="index"/>, with its low surrogate when it is a pair (an emoji, say).
        /// </summary>
        public static string GetCharacterAt(string text, int index)
        {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                return text.Substring(index, 2);
            }

            return text.Substring(index, 1);
        }

        /// <summary>
        /// The 1-based line of <paramref name="index"/>. CR, LF and CRLF each end one line.
        /// </summary>
        public static int GetLineNumber(string text, int index)
        {
            int line = 1;

            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n' || (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
                {
                    line++;
                }
            }

            return line;
        }

        /// <summary>
        /// The encoding for a session backup of <paramref name="text"/>. Backups are read back with the tab's own
        /// encoding, and the reader follows a byte order mark. So text the tab's encoding cannot hold goes into
        /// UTF-8 with a byte order mark instead of losing characters.
        /// </summary>
        public static Encoding GetBackupEncoding(string text, Encoding fileEncoding)
        {
            return CanEncode(text, fileEncoding) ? fileEncoding : new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        // These hold every character. A lone surrogate is the only thing they can't, and no other encoding helps there.
        private static bool IsUnicode(Encoding encoding)
        {
            switch (encoding.CodePage)
            {
                case 65001: // UTF-8
                case 65000: // UTF-7
                case 1200: // UTF-16 LE
                case 1201: // UTF-16 BE
                case 12000: // UTF-32 LE
                case 12001: // UTF-32 BE
                    return true;
                default:
                    return false;
            }
        }
    }
}
