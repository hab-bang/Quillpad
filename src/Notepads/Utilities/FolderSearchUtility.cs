// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Collections.Generic;

    public sealed class TextMatch
    {
        public TextMatch(int line, int column, string snippet)
        {
            Line = line;
            Column = column;
            Snippet = snippet;
        }

        /// <summary>1-based line of the match.</summary>
        public int Line { get; }

        /// <summary>1-based column of the match.</summary>
        public int Column { get; }

        /// <summary>The line around the match, shortened to fit one row of a list.</summary>
        public string Snippet { get; }
    }

    /// <summary>
    /// Plain text search in one file's text, for Find in Folder. One result per matching line.
    /// </summary>
    public static class FolderSearchUtility
    {
        public const int SnippetLength = 90;
        private const int SnippetLead = 30; // Characters kept before the match in a long line

        public static List<TextMatch> FindInText(string text, string query, bool matchCase, int maxResults)
        {
            var matches = new List<TextMatch>();
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query) || maxResults <= 0) return matches;

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var lineNumber = 1;
            var lineStart = 0;

            while (lineStart <= text.Length && matches.Count < maxResults)
            {
                var lineEnd = IndexOfLineEnd(text, lineStart);
                var index = text.IndexOf(query, lineStart, lineEnd - lineStart, comparison);

                if (index >= 0)
                {
                    var line = text.Substring(lineStart, lineEnd - lineStart);
                    matches.Add(new TextMatch(lineNumber, index - lineStart + 1, GetSnippet(line, index - lineStart)));
                }

                if (lineEnd >= text.Length) break;

                // "\r\n" is one line end
                lineStart = text[lineEnd] == '\r' && lineEnd + 1 < text.Length && text[lineEnd + 1] == '\n' ? lineEnd + 2 : lineEnd + 1;
                lineNumber++;
            }

            return matches;
        }

        private static int IndexOfLineEnd(string text, int start)
        {
            for (var i = start; i < text.Length; i++)
            {
                if (text[i] == '\r' || text[i] == '\n') return i;
            }

            return text.Length;
        }

        private static string GetSnippet(string line, int matchIndex)
        {
            var start = 0;
            var prefix = string.Empty;

            if (matchIndex > SnippetLead && line.Length > SnippetLength)
            {
                start = Math.Min(matchIndex - SnippetLead, line.Length - SnippetLength);
                prefix = "…";
            }

            var snippet = line.Substring(start, Math.Min(SnippetLength, line.Length - start));
            var suffix = start + snippet.Length < line.Length ? "…" : string.Empty;

            return (prefix + snippet.Trim() + suffix).Replace('\t', ' ');
        }
    }
}
