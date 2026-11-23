// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    public enum OutlineItemKind
    {
        Heading,
        SceneBreak,
    }

    public sealed class OutlineItem
    {
        public OutlineItem(OutlineItemKind kind, int line, int level, string title)
        {
            Kind = kind;
            Line = line;
            Level = level;
            Title = title;
        }

        public OutlineItemKind Kind { get; }

        /// <summary>0-based line of the heading or scene break.</summary>
        public int Line { get; }

        /// <summary>1 to 6 for headings. A scene break takes the level of the heading above it, plus one.</summary>
        public int Level { get; }

        /// <summary>The heading text, or for a scene break the start of the text after it.</summary>
        public string Title { get; }
    }

    /// <summary>
    /// The headings and scene breaks of a markdown file, for the outline. Code blocks are skipped.
    /// </summary>
    public static class OutlineUtility
    {
        public const int MaxTitleLength = 60;

        private static readonly Regex HeadingRegex = new Regex(@"^\s{0,3}(#{1,6})\s+(.*?)(?:\s+#+)?\s*$", RegexOptions.Compiled);

        public static List<OutlineItem> GetOutline(string text)
        {
            var items = new List<OutlineItem>();
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var contexts = MarkdownLiveStyleParser.GetLineContexts(lines);
            var headingLevel = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                if (contexts[i] != MarkdownLineContext.Normal) continue;

                var heading = HeadingRegex.Match(lines[i]);
                if (heading.Success)
                {
                    headingLevel = heading.Groups[1].Length;
                    items.Add(new OutlineItem(OutlineItemKind.Heading, i, headingLevel, Shorten(heading.Groups[2].Value)));
                }
                else if (Ao3HtmlConverter.IsSceneBreak(lines[i]))
                {
                    items.Add(new OutlineItem(OutlineItemKind.SceneBreak, i, headingLevel + 1, Shorten(FirstTextAfter(lines, i))));
                }
            }

            return items;
        }

        private static string FirstTextAfter(string[] lines, int index)
        {
            for (var i = index + 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (HeadingRegex.IsMatch(line) || Ao3HtmlConverter.IsSceneBreak(line)) return string.Empty;
                return line;
            }

            return string.Empty;
        }

        private static string Shorten(string title)
        {
            title = title.Trim();
            return title.Length <= MaxTitleLength ? title : title.Substring(0, MaxTitleLength - 1).TrimEnd() + "…";
        }
    }
}
