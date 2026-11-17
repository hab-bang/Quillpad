// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    public enum MarkdownLineContext : byte
    {
        Normal,
        Fence,  // A ``` or ~~~ line that opens or closes a code block
        InCode, // A line inside a fenced code block
    }

    public struct MarkdownLink
    {
        public int Start;
        public int Length;
        public string Url;
    }

    /// <summary>
    /// Works out how each character of a markdown line should look in the editor. It only returns
    /// style flags: nothing here renders HTML, runs scripts or loads anything. The text itself never
    /// changes, so the file on disk stays exactly what the user typed.
    /// </summary>
    public static class MarkdownLiveStyleParser
    {
        public const byte Bold = 1;
        public const byte Italic = 2;
        public const byte Strikethrough = 4;
        public const byte Underline = 8;
        public const byte ColorMask = 0x30;
        public const byte ColorBase = 0x00;
        public const byte ColorDim = 0x10;  // Markdown syntax characters
        public const byte ColorLink = 0x20;
        public const byte ColorCode = 0x30;

        private const char Masked = '\u0001'; // Replaces characters that emphasis must not see

        private static readonly Regex FenceRegex = new Regex(@"^ {0,3}(`{3,}|~{3,})", RegexOptions.CultureInvariant);
        private static readonly Regex HorizontalRuleRegex = new Regex(@"^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$", RegexOptions.CultureInvariant);
        private static readonly Regex HeadingRegex = new Regex(@"^ {0,3}(#{1,6})(?=[ \t]|$)", RegexOptions.CultureInvariant);
        private static readonly Regex QuoteRegex = new Regex(@"^(?: {0,3}>[ \t]?)+", RegexOptions.CultureInvariant);
        private static readonly Regex ListRegex = new Regex(@"^[ \t]*(?:[-*+]|\d{1,9}[.)])(?=[ \t])(?:[ \t]+\[[ xX]\](?=[ \t]))?", RegexOptions.CultureInvariant);

        private static readonly Regex InlineLinkRegex = new Regex(
            @"(!?)\[([^\]\r\n]*)\]\([ \t]*(<[^>\r\n]*>|[^\s)]+)(?:[ \t]+(?:""[^""\r\n]*""|'[^'\r\n]*'))?[ \t]*\)",
            RegexOptions.CultureInvariant);
        private static readonly Regex AutoLinkRegex = new Regex(@"<((?:https?://|mailto:)[^\s<>]+)>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex BareUrlRegex = new Regex(
            @"(?<![\w/@.])(?:https?://|www\.)[^\s<>\[\]" + Masked + @"]*[^\s<>\[\]" + Masked + @".,;:!?'"")*_~]",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex StrikethroughRegex = new Regex(@"~~(?=\S)(.+?)(?<=\S)~~", RegexOptions.CultureInvariant);
        private static readonly Regex BoldItalicStarRegex = new Regex(@"\*\*\*(?=[^\s*])(.+?)(?<=[^\s*])\*\*\*", RegexOptions.CultureInvariant);
        private static readonly Regex BoldItalicUnderscoreRegex = new Regex(@"(?<![\w_])___(?=[^\s_])(.+?)(?<=[^\s_])___(?![\w_])", RegexOptions.CultureInvariant);
        private static readonly Regex BoldStarRegex = new Regex(@"\*\*(?=[^\s*])(.+?)(?<=[^\s*])\*\*", RegexOptions.CultureInvariant);
        private static readonly Regex BoldUnderscoreRegex = new Regex(@"(?<![\w_])__(?=[^\s_])(.+?)(?<=[^\s_])__(?![\w_])", RegexOptions.CultureInvariant);
        private static readonly Regex ItalicStarRegex = new Regex(@"(?<!\*)\*(?=[^\s*])(.+?)(?<=[^\s*])\*(?!\*)", RegexOptions.CultureInvariant);
        private static readonly Regex ItalicUnderscoreRegex = new Regex(@"(?<![\w_])_(?=[^\s_])(.+?)(?<=[^\s_])_(?![\w_])", RegexOptions.CultureInvariant);

        /// <summary>
        /// Links in a document open only when they are web or mail links. A file:, ms-settings: or
        /// other app protocol link could start a program or open a local file, so it is ignored.
        /// </summary>
        public static bool IsOpenableLink(Uri uri)
        {
            return uri != null && uri.IsAbsoluteUri &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto);
        }

        public static MarkdownLineContext[] GetLineContexts(string[] lines)
        {
            var contexts = new MarkdownLineContext[lines.Length];
            char fenceChar = '\0';
            var fenceLength = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var match = FenceRegex.Match(lines[i]);

                if (fenceChar == '\0')
                {
                    if (match.Success && !(match.Groups[1].Value[0] == '`' && lines[i].IndexOf('`', match.Index + match.Length) >= 0))
                    {
                        fenceChar = match.Groups[1].Value[0];
                        fenceLength = match.Groups[1].Length;
                        contexts[i] = MarkdownLineContext.Fence;
                    }
                    else
                    {
                        contexts[i] = MarkdownLineContext.Normal;
                    }
                }
                else if (match.Success &&
                         match.Groups[1].Value[0] == fenceChar &&
                         match.Groups[1].Length >= fenceLength &&
                         lines[i].Substring(match.Index + match.Length).Trim().Length == 0)
                {
                    fenceChar = '\0';
                    contexts[i] = MarkdownLineContext.Fence;
                }
                else
                {
                    contexts[i] = MarkdownLineContext.InCode;
                }
            }

            return contexts;
        }

        /// <summary>
        /// Fills styles (one entry for each character of line) and returns the links found in the line.
        /// Link offsets are relative to the start of the line.
        /// </summary>
        public static List<MarkdownLink> StyleLine(string line, MarkdownLineContext context, byte[] styles)
        {
            var links = new List<MarkdownLink>();
            Array.Clear(styles, 0, line.Length);
            if (line.Length == 0) return links;

            if (context == MarkdownLineContext.Fence)
            {
                Fill(styles, 0, line.Length, ColorDim);
                return links;
            }

            if (context == MarkdownLineContext.InCode)
            {
                Fill(styles, 0, line.Length, ColorCode);
                return links;
            }

            if (HorizontalRuleRegex.IsMatch(line))
            {
                Fill(styles, 0, line.Length, ColorDim);
                return links;
            }

            var start = 0;

            var quote = QuoteRegex.Match(line);
            if (quote.Success)
            {
                for (var i = 0; i < quote.Length; i++)
                {
                    if (line[i] == '>') styles[i] = ColorDim;
                }
                start = quote.Length;
            }

            byte baseStyle = 0;
            var rest = line.Substring(start);

            var heading = HeadingRegex.Match(rest);
            if (heading.Success)
            {
                Fill(styles, start + heading.Groups[1].Index, heading.Groups[1].Length, ColorDim);
                start += heading.Length;
                baseStyle = Bold;
            }
            else
            {
                var list = ListRegex.Match(rest);
                if (list.Success)
                {
                    for (var i = 0; i < list.Length; i++)
                    {
                        if (!char.IsWhiteSpace(rest[i])) styles[start + i] = ColorDim;
                    }
                    start += list.Length;
                }
            }

            if (baseStyle != 0) Fill(styles, start, line.Length - start, baseStyle, additive: true);

            StyleInline(line, start, styles, links);
            return links;
        }

        private static void StyleInline(string line, int start, byte[] styles, List<MarkdownLink> links)
        {
            var work = line.ToCharArray();
            for (var i = 0; i < start; i++) work[i] = Masked;

            // Backslash escapes: the backslash is syntax, the escaped character is plain text
            for (var i = start; i < work.Length - 1; i++)
            {
                if (work[i] == '\\' && IsAsciiPunctuation(work[i + 1]))
                {
                    styles[i] = (byte)((styles[i] & ~ColorMask) | ColorDim);
                    work[i] = Masked;
                    work[i + 1] = Masked;
                    i++;
                }
            }

            StyleCodeSpans(work, start, styles);

            var text = new string(work);

            foreach (Match match in AutoLinkRegex.Matches(text))
            {
                SetColor(styles, match.Index, 1, ColorDim);
                SetColor(styles, match.Index + 1, match.Length - 2, ColorLink);
                AddFlag(styles, match.Index + 1, match.Length - 2, Underline);
                SetColor(styles, match.Index + match.Length - 1, 1, ColorDim);
                links.Add(new MarkdownLink { Start = match.Index + 1, Length = match.Length - 2, Url = match.Groups[1].Value });
                Mask(work, match.Index, match.Length);
            }

            text = new string(work);

            foreach (Match match in InlineLinkRegex.Matches(text))
            {
                var isImage = match.Groups[1].Length > 0;
                var label = match.Groups[2];
                var url = match.Groups[3].Value.Trim('<', '>');

                // Everything except the label is syntax: "![", "](url \"title\")"
                SetColor(styles, match.Index, label.Index - match.Index, ColorDim);
                SetColor(styles, label.Index + label.Length, match.Index + match.Length - label.Index - label.Length, ColorDim);

                if (isImage)
                {
                    SetColor(styles, label.Index, label.Length, ColorDim);
                }
                else
                {
                    SetColor(styles, label.Index, label.Length, ColorLink);
                    AddFlag(styles, label.Index, label.Length, Underline);
                    links.Add(new MarkdownLink { Start = label.Index, Length = label.Length, Url = url });
                }

                Mask(work, match.Index, label.Index - match.Index);
                Mask(work, label.Index + label.Length, match.Index + match.Length - label.Index - label.Length);
                if (isImage) Mask(work, label.Index, label.Length);
            }

            text = new string(work);

            foreach (Match match in BareUrlRegex.Matches(text))
            {
                SetColor(styles, match.Index, match.Length, ColorLink);
                AddFlag(styles, match.Index, match.Length, Underline);
                var url = match.Value.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + match.Value : match.Value;
                links.Add(new MarkdownLink { Start = match.Index, Length = match.Length, Url = url });
                Mask(work, match.Index, match.Length);
            }

            ApplyEmphasis(work, styles, StrikethroughRegex, 2, Strikethrough);
            ApplyEmphasis(work, styles, BoldItalicStarRegex, 3, Bold | Italic);
            ApplyEmphasis(work, styles, BoldItalicUnderscoreRegex, 3, Bold | Italic);
            ApplyEmphasis(work, styles, BoldStarRegex, 2, Bold);
            ApplyEmphasis(work, styles, BoldUnderscoreRegex, 2, Bold);
            ApplyEmphasis(work, styles, ItalicStarRegex, 1, Italic);
            ApplyEmphasis(work, styles, ItalicUnderscoreRegex, 1, Italic);
        }

        private static void StyleCodeSpans(char[] work, int start, byte[] styles)
        {
            var i = start;
            while (i < work.Length)
            {
                if (work[i] != '`')
                {
                    i++;
                    continue;
                }

                var openLength = RunLength(work, i, '`');
                var j = i + openLength;
                var closeAt = -1;

                while (j < work.Length)
                {
                    if (work[j] == '`')
                    {
                        var runLength = RunLength(work, j, '`');
                        if (runLength == openLength)
                        {
                            closeAt = j;
                            break;
                        }
                        j += runLength;
                    }
                    else
                    {
                        j++;
                    }
                }

                if (closeAt < 0)
                {
                    i += openLength; // No closing run: the backticks are plain text
                    continue;
                }

                SetColor(styles, i, openLength, ColorDim);
                SetColor(styles, i + openLength, closeAt - i - openLength, ColorCode);
                SetColor(styles, closeAt, openLength, ColorDim);
                Mask(work, i, closeAt + openLength - i);
                i = closeAt + openLength;
            }
        }

        private static void ApplyEmphasis(char[] work, byte[] styles, Regex regex, int markerLength, byte flags)
        {
            // Matches are found on the text as it is before this pass, and markers are masked after,
            // so "**a** and *b*" never reuses the stars of the bold run for the italic run.
            var text = new string(work);
            foreach (Match match in regex.Matches(text))
            {
                var content = match.Groups[1];
                SetColor(styles, match.Index, markerLength, ColorDim);
                SetColor(styles, match.Index + match.Length - markerLength, markerLength, ColorDim);
                AddFlag(styles, match.Index, match.Length, flags);
                Mask(work, match.Index, markerLength);
                Mask(work, match.Index + match.Length - markerLength, markerLength);
            }
        }

        private static int RunLength(char[] work, int index, char c)
        {
            var length = 0;
            while (index + length < work.Length && work[index + length] == c) length++;
            return length;
        }

        private static bool IsAsciiPunctuation(char c)
        {
            return (c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~');
        }

        private static void Mask(char[] work, int index, int length)
        {
            for (var i = index; i < index + length && i < work.Length; i++) work[i] = Masked;
        }

        private static void Fill(byte[] styles, int index, int length, byte style, bool additive = false)
        {
            for (var i = index; i < index + length && i < styles.Length; i++)
            {
                styles[i] = additive ? (byte)(styles[i] | style) : style;
            }
        }

        private static void SetColor(byte[] styles, int index, int length, byte color)
        {
            for (var i = index; i < index + length && i < styles.Length; i++)
            {
                styles[i] = (byte)((styles[i] & ~ColorMask) | color);
            }
        }

        private static void AddFlag(byte[] styles, int index, int length, byte flags)
        {
            for (var i = index; i < index + length && i < styles.Length; i++)
            {
                styles[i] = (byte)(styles[i] | flags);
            }
        }
    }
}
