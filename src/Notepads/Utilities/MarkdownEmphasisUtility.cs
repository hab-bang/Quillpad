// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// One replacement in the text, and where the selection goes after it.
    /// </summary>
    public readonly struct TextEdit
    {
        public TextEdit(int start, int length, string replacement, int selectionStart, int selectionLength)
        {
            Start = start;
            Length = length;
            Replacement = replacement;
            SelectionStart = selectionStart;
            SelectionLength = selectionLength;
        }

        public int Start { get; }

        public int Length { get; }

        public string Replacement { get; }

        public int SelectionStart { get; }

        public int SelectionLength { get; }
    }

    /// <summary>
    /// Ctrl+B and Ctrl+I in a markdown file: puts ** or * around the selection, or takes them away.
    /// Bold and italic are both written with "*", so the run of stars around the text tells what is there:
    /// 1 is italic, 2 is bold, 3 is both. Spaces at the ends of the selection stay outside the stars,
    /// because "**word **" is not bold in markdown. Each line of a selection gets its own stars.
    /// </summary>
    public static class MarkdownEmphasisUtility
    {
        public const int Italic = 1;
        public const int Bold = 2;

        public static TextEdit Toggle(string text, int selectionStart, int selectionEnd, int marker)
        {
            if (marker != Italic && marker != Bold) throw new ArgumentOutOfRangeException(nameof(marker));

            text = text ?? string.Empty;
            var start = Math.Max(0, Math.Min(selectionStart, selectionEnd));
            var end = Math.Min(text.Length, Math.Max(selectionStart, selectionEnd));

            var segments = SplitIntoLines(text, start, end);
            var parts = new List<(int Start, int End, string Replacement, int CoreOffset, int CoreLength)>();

            foreach (var (segmentStart, segmentEnd) in segments)
            {
                // A blank line in a longer selection gets no stars
                if (segments.Count > 1 && IsBlank(text, segmentStart, segmentEnd)) continue;
                parts.Add(ToggleSegment(text, segmentStart, segmentEnd, marker));
            }

            if (parts.Count == 0) return new TextEdit(start, 0, string.Empty, start, end - start);

            var editStart = parts[0].Start;
            var editEnd = parts[parts.Count - 1].End;
            var builder = new StringBuilder();
            var position = editStart;
            var newSelectionStart = -1;
            var newSelectionEnd = 0;

            foreach (var part in parts)
            {
                builder.Append(text, position, part.Start - position);
                var coreStart = editStart + builder.Length + part.CoreOffset;
                if (newSelectionStart < 0) newSelectionStart = coreStart;
                newSelectionEnd = coreStart + part.CoreLength;
                builder.Append(part.Replacement);
                position = part.End;
            }

            return new TextEdit(editStart, editEnd - editStart, builder.ToString(), newSelectionStart, newSelectionEnd - newSelectionStart);
        }

        private static (int Start, int End, string Replacement, int CoreOffset, int CoreLength) ToggleSegment(string text, int start, int end, int marker)
        {
            // Spaces at the ends stay outside the stars
            while (start < end && char.IsWhiteSpace(text[start])) start++;
            while (end > start && char.IsWhiteSpace(text[end - 1])) end--;

            // Stars selected along with the text count as around it
            var coreStart = start;
            while (coreStart < end && text[coreStart] == '*') coreStart++;
            var coreEnd = end;
            while (coreEnd > coreStart && text[coreEnd - 1] == '*') coreEnd--;

            var outerStart = start;
            while (outerStart > 0 && text[outerStart - 1] == '*') outerStart--;
            var outerEnd = end;
            while (outerEnd < text.Length && text[outerEnd] == '*') outerEnd++;

            var left = coreStart - outerStart;
            var right = outerEnd - coreEnd;
            var matched = Math.Min(left, right);

            int newMatched;
            if (marker == Bold)
            {
                newMatched = matched >= 2 ? matched - 2 : matched + 2;
            }
            else
            {
                newMatched = matched % 2 == 1 ? matched - 1 : matched + 1;
            }

            var leftStars = left - matched + newMatched;
            var rightStars = right - matched + newMatched;
            var core = text.Substring(coreStart, coreEnd - coreStart);
            var replacement = new string('*', leftStars) + core + new string('*', rightStars);

            return (outerStart, outerEnd, replacement, leftStars, core.Length);
        }

        private static List<(int Start, int End)> SplitIntoLines(string text, int start, int end)
        {
            var segments = new List<(int, int)>();
            var segmentStart = start;

            for (var i = start; i < end; i++)
            {
                if (text[i] == '\r' || text[i] == '\n')
                {
                    segments.Add((segmentStart, i));
                    segmentStart = i + 1;
                }
            }

            segments.Add((segmentStart, end));
            return segments;
        }

        private static bool IsBlank(string text, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                if (!char.IsWhiteSpace(text[i]) && text[i] != '*') return false;
            }

            return true;
        }
    }
}
