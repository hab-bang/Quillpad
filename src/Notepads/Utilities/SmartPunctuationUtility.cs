// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    /// <summary>
    /// Typographer's punctuation while typing: "--" becomes an em dash, "..." an ellipsis, and straight
    /// quotes become curly quotes. Only the current line is read, so the cost does not grow with the file.
    /// Code spans, fenced code, HTML comments and markdown rules ("---", tables) are left alone.
    /// </summary>
    public static class SmartPunctuationUtility
    {
        public const char EmDash = '—';
        public const char Ellipsis = '…';
        public const char LeftDoubleQuote = '“';
        public const char RightDoubleQuote = '”';
        public const char LeftSingleQuote = '‘';
        public const char RightSingleQuote = '’';

        /// <summary>True for the characters that can start a replacement.</summary>
        public static bool IsTrigger(char c)
        {
            return c == '"' || c == '\'' || c == '-' || c == '.';
        }

        /// <summary>
        /// Returns the replacement for the character just typed, which is text[caret - 1], or null.
        /// The edit leaves the caret after the replacement.
        /// </summary>
        public static TextEdit? GetReplacement(string text, int caret)
        {
            if (text == null || caret <= 0 || caret > text.Length) return null;

            var typed = text[caret - 1];
            if (!IsTrigger(typed)) return null;

            var lineStart = caret - 1;
            while (lineStart > 0 && text[lineStart - 1] != '\r' && text[lineStart - 1] != '\n') lineStart--;

            var before = text.Substring(lineStart, caret - 1 - lineStart); // The line before the typed character
            if (IsInsideCodeSpan(before) || IsInsideHtmlComment(before)) return null;

            switch (typed)
            {
                case '"':
                    return Replace(caret - 1, 1, IsOpeningPosition(before) ? LeftDoubleQuote : RightDoubleQuote);
                case '\'':
                    return Replace(caret - 1, 1, IsOpeningPosition(before) ? LeftSingleQuote : RightSingleQuote);
                case '-':
                    return GetDashReplacement(before, caret);
                case '.':
                    return before.EndsWith("..") && !before.EndsWith("...") ? Replace(caret - 3, 3, Ellipsis) : (TextEdit?)null;
                default:
                    return null;
            }
        }

        private static TextEdit? GetDashReplacement(string before, int caret)
        {
            if (!before.EndsWith("-") || before.EndsWith("--")) return null;

            // At the start of a line, "---" is a scene break or front matter, and "--" may become one
            var textBefore = before.Substring(0, before.Length - 1);
            if (textBefore.Trim().Length == 0) return null;

            // Markdown table rows like "| --- |"
            if (textBefore.TrimStart().StartsWith("|")) return null;

            return Replace(caret - 2, 2, EmDash);
        }

        private static TextEdit Replace(int start, int length, char replacement)
        {
            return new TextEdit(start, length, replacement.ToString(), start + 1, 0);
        }

        /// <summary>
        /// A quote opens at the start of a line, after a space, after an opening bracket or dash, or after
        /// markdown emphasis that itself opens there, as in *"Run!"*.
        /// </summary>
        private static bool IsOpeningPosition(string before)
        {
            var i = before.Length - 1;
            while (i >= 0 && (before[i] == '*' || before[i] == '_')) i--;
            if (i < 0) return true;

            var previous = before[i];
            return char.IsWhiteSpace(previous) ||
                   previous == '(' || previous == '[' || previous == '{' || previous == '<' ||
                   previous == EmDash || previous == '–' || previous == '-' ||
                   previous == LeftDoubleQuote || previous == LeftSingleQuote || previous == '"' || previous == '\'';
        }

        /// <summary>
        /// True when a run of backticks opened on this line and has not closed yet. The closing run may not be
        /// typed yet, so an open run counts as code.
        /// </summary>
        public static bool IsInsideCodeSpan(string lineBeforeCaret)
        {
            var open = 0;
            var i = 0;

            while (i < lineBeforeCaret.Length)
            {
                if (lineBeforeCaret[i] != '`')
                {
                    i++;
                    continue;
                }

                var run = 0;
                while (i < lineBeforeCaret.Length && lineBeforeCaret[i] == '`')
                {
                    run++;
                    i++;
                }

                if (open == 0) open = run;
                else if (run == open) open = 0;
            }

            return open > 0;
        }

        private static bool IsInsideHtmlComment(string lineBeforeCaret)
        {
            var start = lineBeforeCaret.LastIndexOf("<!-", System.StringComparison.Ordinal);
            return start >= 0 && lineBeforeCaret.IndexOf("-->", start, System.StringComparison.Ordinal) < 0;
        }
    }
}
