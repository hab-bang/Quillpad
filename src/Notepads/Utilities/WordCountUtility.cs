// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System.Globalization;

    public struct TextStatistics
    {
        public int Words;
        public int Characters;
        public int CharactersWithoutSpaces;
    }

    public static class WordCountUtility
    {
        /// <summary>
        /// Counts words the way a writer expects: a run of non-space characters is a word only if it
        /// holds a letter or a digit, so markdown markers on their own ("#", "*", "-", "---") do not count.
        /// Dashes split words as described in IsWordBreakingDash. Each Chinese or Japanese character counts
        /// as one word, because those scripts do not use spaces.
        /// </summary>
        public static TextStatistics Count(string text, int start = 0, int length = -1)
        {
            var stats = new TextStatistics();
            if (string.IsNullOrEmpty(text)) return stats;

            if (start < 0) start = 0;
            if (start > text.Length) start = text.Length;
            var end = length < 0 ? text.Length : start + length;
            if (end > text.Length) end = text.Length;

            var inToken = false;
            var tokenHasWordChar = false;

            for (var i = start; i < end; i++)
            {
                var c = text[i];

                if (char.IsLowSurrogate(c)) continue; // Counted with its high surrogate

                stats.Characters++;

                if (char.IsWhiteSpace(c))
                {
                    if (inToken && tokenHasWordChar) stats.Words++;
                    inToken = false;
                    tokenHasWordChar = false;
                    continue;
                }

                stats.CharactersWithoutSpaces++;

                if (IsWordBreakingDash(text, i, start, end))
                {
                    if (inToken && tokenHasWordChar) stats.Words++;
                    inToken = false;
                    tokenHasWordChar = false;
                    continue;
                }

                if (IsIdeograph(c))
                {
                    if (inToken && tokenHasWordChar) stats.Words++;
                    stats.Words++;
                    inToken = false;
                    tokenHasWordChar = false;
                    continue;
                }

                inToken = true;
                if (!tokenHasWordChar && char.IsLetterOrDigit(c)) tokenHasWordChar = true;
            }

            if (inToken && tokenHasWordChar) stats.Words++;

            return stats;
        }

        /// <summary>
        /// "paused—then" is two words. An em dash, an en dash, or two hyphens typed for an em dash
        /// separate words. A single hyphen joins them ("well-known").
        /// </summary>
        private static bool IsWordBreakingDash(string text, int i, int start, int end)
        {
            var c = text[i];
            if (c == '–' || c == '—' || c == '―') return true;
            return c == '-' && ((i > start && text[i - 1] == '-') || (i + 1 < end && text[i + 1] == '-'));
        }

        private static bool IsIdeograph(char c)
        {
            return (c >= '一' && c <= '鿿') || // CJK Unified Ideographs
                   (c >= '㐀' && c <= '䶿') || // CJK Extension A
                   (c >= '぀' && c <= 'ヿ') || // Hiragana and Katakana
                   (c >= '豈' && c <= '﫿');   // CJK Compatibility Ideographs
        }

        public static string FormatNumber(int value)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture);
        }
    }
}
