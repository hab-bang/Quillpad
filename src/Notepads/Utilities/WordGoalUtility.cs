// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Word count goals: reading what the user typed, the progress, and the words written since the file was opened.
    /// </summary>
    public static class WordGoalUtility
    {
        public const int MaxGoal = 10000000;

        /// <summary>
        /// Reads a goal like "3000", "3,000" or "3 000". Returns false for anything else, including 0.
        /// </summary>
        public static bool TryParseGoal(string input, out int goal)
        {
            goal = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var digits = new StringBuilder();
            foreach (var c in input.Trim())
            {
                if (c >= '0' && c <= '9') digits.Append(c);
                else if (c == ',' || c == '.' || c == ' ' || c == '\'' || c == '\u00A0' || c == '\u202F') continue; // Group separators
                else return false;
            }

            if (digits.Length == 0 || digits.Length > 9) return false;

            goal = int.Parse(digits.ToString(), CultureInfo.InvariantCulture);
            return goal > 0 && goal <= MaxGoal;
        }

        /// <summary>Words written since the file was opened. Negative when the text got shorter.</summary>
        public static int WordsAddedThisSession(int currentWords, int wordsAtFirstOpen)
        {
            return currentWords - wordsAtFirstOpen;
        }

        /// <summary>"+412", "-12" or "0", with the culture's group separator.</summary>
        public static string FormatSigned(int value, CultureInfo culture)
        {
            var number = Math.Abs((long)value).ToString("N0", culture);
            if (value > 0) return "+" + number;
            if (value < 0) return culture.NumberFormat.NegativeSign + number;
            return number;
        }

        /// <summary>Whole percent of the goal reached, at most 999.</summary>
        public static int ProgressPercent(int words, int goal)
        {
            if (goal <= 0 || words <= 0) return 0;
            return (int)Math.Min(999, (long)words * 100 / goal);
        }

        /// <summary>
        /// A short, stable key for a file path, for a settings store whose keys are limited to 255 characters.
        /// Windows paths ignore case, so "C:\A.md" and "c:\a.md" get the same key.
        /// </summary>
        public static string GetStoreKey(string filePath)
        {
            const ulong offsetBasis = 14695981039346656037;
            const ulong prime = 1099511628211;

            var hash = offsetBasis;
            foreach (var c in (filePath ?? string.Empty).ToUpperInvariant())
            {
                hash ^= c;
                hash *= prime;
            }

            return "Goal_" + hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
