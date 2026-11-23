// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Turns a markdown chapter into the plain HTML that AO3's HTML editor takes: &lt;p&gt;, &lt;em&gt;,
    /// &lt;strong&gt;, headings and &lt;hr /&gt;, with no classes or styles.
    /// A blank line ends a paragraph. A single line break inside a paragraph stays a line break (&lt;br /&gt;),
    /// as on AO3. A line of three or more *, -, _ or ~ (spaces allowed) is a scene break.
    /// Anything else, like links, lists or code, is kept as its text.
    /// </summary>
    public static class Ao3HtmlConverter
    {
        private static readonly Regex SceneBreakRegex = new Regex(@"^\s{0,3}([*\-_~])(\s*\1){2,}\s*$", RegexOptions.Compiled);
        private static readonly Regex HeadingRegex = new Regex(@"^\s{0,3}(#{1,6})(?:\s+(.*?))?(?:\s+#+)?\s*$", RegexOptions.Compiled);

        private static readonly Regex StrongEmRegex = new Regex(@"\*\*\*(?=\S)(.+?)(?<=\S)\*\*\*", RegexOptions.Compiled);
        private static readonly Regex StrongStarRegex = new Regex(@"\*\*(?=\S)(.+?)(?<=\S)\*\*", RegexOptions.Compiled);
        private static readonly Regex StrongUnderscoreRegex = new Regex(@"(?<![\p{L}\p{N}_])__(?=\S)(.+?)(?<=\S)__(?![\p{L}\p{N}_])", RegexOptions.Compiled);
        private static readonly Regex EmStarRegex = new Regex(@"(?<!\*)\*(?=[^\s*])(.+?)(?<=[^\s*])\*(?!\*)", RegexOptions.Compiled);
        private static readonly Regex EmUnderscoreRegex = new Regex(@"(?<![\p{L}\p{N}_])_(?=[^\s_])(.+?)(?<=[^\s_])_(?![\p{L}\p{N}_])", RegexOptions.Compiled);

        // Stand-ins for escaped characters, so emphasis does not see them. Private use characters never typed.
        private const char EscapedStar = '';
        private const char EscapedUnderscore = '';

        /// <summary>The HTML for the text, one block per line, for pasting into AO3.</summary>
        public static string ToHtml(string markdown)
        {
            var html = new StringBuilder();
            var paragraph = new List<string>();

            void EndParagraph()
            {
                if (paragraph.Count == 0) return;
                html.Append("<p>").Append(string.Join("<br />\n", paragraph)).Append("</p>\n");
                paragraph.Clear();
            }

            var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    EndParagraph();
                    continue;
                }

                if (SceneBreakRegex.IsMatch(line))
                {
                    EndParagraph();
                    html.Append("<hr />\n");
                    continue;
                }

                var heading = HeadingRegex.Match(line);
                if (heading.Success)
                {
                    EndParagraph();
                    var level = System.Math.Min(6, heading.Groups[1].Length + 1); // "#" is the chapter title, AO3 shows it as <h2>
                    html.Append("<h").Append(level).Append('>')
                        .Append(ConvertInline(heading.Groups[2].Value))
                        .Append("</h").Append(level).Append(">\n");
                    continue;
                }

                paragraph.Add(ConvertInline(line.Trim()));
            }

            EndParagraph();
            return html.ToString();
        }

        /// <summary>A whole HTML file with the converted text, for "Export as HTML".</summary>
        public static string ToHtmlDocument(string markdown, string title)
        {
            return "<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\" />\n<title>" + Escape(title ?? string.Empty) +
                   "</title>\n</head>\n<body>\n" + ToHtml(markdown) + "</body>\n</html>\n";
        }

        private static string ConvertInline(string text)
        {
            text = Escape(ReplaceBackslashEscapes(text));

            text = StrongEmRegex.Replace(text, "<strong><em>$1</em></strong>");
            text = StrongStarRegex.Replace(text, "<strong>$1</strong>");
            text = StrongUnderscoreRegex.Replace(text, "<strong>$1</strong>");
            text = EmStarRegex.Replace(text, "<em>$1</em>");
            text = EmUnderscoreRegex.Replace(text, "<em>$1</em>");

            return text.Replace(EscapedStar, '*').Replace(EscapedUnderscore, '_');
        }

        // \* and \_ are a plain star and underscore, \\ is one backslash
        private static string ReplaceBackslashEscapes(string text)
        {
            if (text.IndexOf('\\') < 0) return text;

            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var next = i + 1 < text.Length ? text[i + 1] : '\0';
                if (text[i] == '\\' && (next == '*' || next == '_' || next == '\\'))
                {
                    builder.Append(next == '*' ? EscapedStar : next == '_' ? EscapedUnderscore : '\\');
                    i++;
                }
                else
                {
                    builder.Append(text[i]);
                }
            }

            return builder.ToString();
        }

        // Text only, never attribute values, so quotes can stay as they are
        private static string Escape(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
