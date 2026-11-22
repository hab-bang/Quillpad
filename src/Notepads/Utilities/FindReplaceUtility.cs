// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using Notepads.Controls.FindAndReplace;
    using Notepads.Extensions;

    /// <summary>
    /// One replacement in the text as it was before any replacement: Length characters from Start become Text.
    /// </summary>
    public struct TextReplacement
    {
        public int Start;
        public int Length;
        public string Text;
    }

    /// <summary>
    /// The search and replace logic of Find and Replace, without the editor. Texts use the editor's "\r"
    /// line endings. A regex sees "\n" instead, so that "$" and "\n" work as people expect.
    /// </summary>
    public static class FindReplaceUtility
    {
        private const char EditorLineEnding = '\r';
        private const char RegexLineEnding = '\n';

        /// <summary>
        /// A pattern like (a+)+b can take minutes on a long chapter. After this long the search stops
        /// with a RegexMatchTimeoutException, which Find and Replace shows like a pattern error.
        /// </summary>
        public static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

        public static Regex CreateRegex(SearchContext searchContext, bool rightToLeft = false)
        {
            var options = RegexOptions.Multiline;
            if (!searchContext.MatchCase) options |= RegexOptions.IgnoreCase;
            if (rightToLeft) options |= RegexOptions.RightToLeft;
            return new Regex(searchContext.SearchText, options, RegexTimeout);
        }

        public static string ToRegexText(string text) => text.Replace(EditorLineEnding, RegexLineEnding);

        /// <summary>
        /// The text that replaces a regex match: "$1" and the other substitutions are expanded,
        /// and "\n", "\r" and "\t" typed in the Replace box become a line break or a tab.
        /// </summary>
        public static string ExpandRegexReplacement(Match match, string replaceText)
        {
            return match.Result(UnescapeReplaceText(replaceText)).Replace(RegexLineEnding, EditorLineEnding);
        }

        public static string UnescapeReplaceText(string text)
        {
            return text.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t");
        }

        /// <summary>
        /// Finds every match, from the start, without overlaps. Throws for a bad pattern or a timeout.
        /// </summary>
        public static List<TextReplacement> FindAll(string text, SearchContext searchContext, string replaceText)
        {
            var replacements = new List<TextReplacement>();
            if (string.IsNullOrEmpty(searchContext.SearchText)) return replacements;

            if (searchContext.UseRegex)
            {
                foreach (Match match in CreateRegex(searchContext).Matches(ToRegexText(text)))
                {
                    replacements.Add(new TextReplacement { Start = match.Index, Length = match.Length, Text = ExpandRegexReplacement(match, replaceText) });
                }

                return replacements;
            }

            var comparison = searchContext.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var searchLength = searchContext.SearchText.Length;
            var position = 0;

            while (position <= text.Length)
            {
                position = searchContext.MatchWholeWord
                    ? text.IndexOfWholeWord(searchContext.SearchText, position, comparison)
                    : text.IndexOf(searchContext.SearchText, position, comparison);
                if (position < 0) break;

                replacements.Add(new TextReplacement { Start = position, Length = searchLength, Text = replaceText });
                position += searchLength;
            }

            return replacements;
        }

        public static string Apply(string text, IReadOnlyList<TextReplacement> replacements)
        {
            var result = new StringBuilder(text.Length);
            var position = 0;

            foreach (var replacement in replacements)
            {
                result.Append(text, position, replacement.Start - position).Append(replacement.Text);
                position = replacement.Start + replacement.Length;
            }

            return result.Append(text, position, text.Length - position).ToString();
        }

        /// <summary>
        /// Where a position (a caret) ends up after the replacements, so it stays on the same words.
        /// A position inside a replaced range moves to the start of the new text.
        /// </summary>
        public static int MapPosition(int position, IReadOnlyList<TextReplacement> replacements)
        {
            var shift = 0;

            foreach (var replacement in replacements)
            {
                if (position <= replacement.Start) break;
                if (position < replacement.Start + replacement.Length) return replacement.Start + shift;
                shift += replacement.Text.Length - replacement.Length;
            }

            return position + shift;
        }
    }
}
