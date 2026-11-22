// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.TextEditor
{
    using System;
    using System.Collections.Generic;
    using Windows.UI.Core;
    using Windows.UI.Text;
    using Notepads.Controls.FindAndReplace;
    using Notepads.Extensions;
    using Notepads.Services;
    using Notepads.Utilities;

    public partial class TextEditorCore
    {
        public string GetSearchString()
        {
            var searchString = Document.Selection.Text.Trim();

            if (searchString.Contains(RichEditBoxDefaultLineEnding.ToString())) return string.Empty;

            var document = GetText();

            if (string.IsNullOrEmpty(searchString) && Document.Selection.StartPosition < document.Length)
            {
                var startIndex = Document.Selection.StartPosition;
                var endIndex = startIndex;

                for (; startIndex >= 0; startIndex--)
                {
                    if (!char.IsLetterOrDigit(document[startIndex]))
                        break;
                }

                for (; endIndex < document.Length; endIndex++)
                {
                    if (!char.IsLetterOrDigit(document[endIndex]))
                        break;
                }

                if (startIndex != endIndex) return document.Substring(startIndex + 1, endIndex - startIndex - 1);
            }

            return searchString;
        }

        public bool TryFindNextAndSelect(SearchContext searchContext, bool stopAtEof, out bool regexError)
        {
            regexError = false;

            if (string.IsNullOrEmpty(searchContext.SearchText))
            {
                return false;
            }

            var document = GetText();

            if (Document.Selection.EndPosition > document.Length) Document.Selection.EndPosition = document.Length;

            if (searchContext.UseRegex)
            {
                return TryFindNextAndSelectUsingRegex(document, searchContext, stopAtEof, out regexError);
            }
            else
            {
                StringComparison comparison = searchContext.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                var index = searchContext.MatchWholeWord
                    ? document.IndexOfWholeWord(searchContext.SearchText, Document.Selection.EndPosition, comparison)
                    : document.IndexOf(searchContext.SearchText, Document.Selection.EndPosition, comparison);

                if (index != -1)
                {
                    Document.Selection.StartPosition = index;
                    Document.Selection.EndPosition = index + searchContext.SearchText.Length;
                }
                else
                {
                    if (!stopAtEof)
                    {
                        index = searchContext.MatchWholeWord
                            ? document.IndexOfWholeWord(searchContext.SearchText, 0, comparison)
                            : document.IndexOf(searchContext.SearchText, 0, comparison);

                        if (index != -1)
                        {
                            Document.Selection.StartPosition = index;
                            Document.Selection.EndPosition = index + searchContext.SearchText.Length;
                        }
                    }
                }

                if (index == -1)
                {
                    Document.Selection.StartPosition = Document.Selection.EndPosition;
                    return false;
                }

                return true;
            }
        }

        public bool TryFindPreviousAndSelect(SearchContext searchContext, bool stopAtBof, out bool regexError)
        {
            regexError = false;

            if (string.IsNullOrEmpty(searchContext.SearchText))
            {
                return false;
            }

            var document = GetText();

            if (searchContext.UseRegex)
            {
                return TryFindPreviousAndSelectUsingRegex(document, searchContext, out regexError, stopAtBof);
            }
            else
            {
                var searchIndex = Document.Selection.StartPosition - 1;
                if (searchIndex < 0)
                {
                    if (stopAtBof)
                    {
                        return false;
                    }
                    else
                    {
                        searchIndex = document.Length - 1;
                    }
                }

                StringComparison comparison = searchContext.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                var index = searchContext.MatchWholeWord
                    ? document.LastIndexOfWholeWord(searchContext.SearchText, searchIndex, comparison)
                    : document.LastIndexOf(searchContext.SearchText, searchIndex, comparison);

                if (index != -1)
                {
                    Document.Selection.StartPosition = index;
                    Document.Selection.EndPosition = index + searchContext.SearchText.Length;
                }
                else
                {
                    index = searchContext.MatchWholeWord
                        ? document.LastIndexOfWholeWord(searchContext.SearchText, document.Length - 1, comparison)
                        : document.LastIndexOf(searchContext.SearchText, document.Length - 1, comparison);

                    if (index != -1)
                    {
                        Document.Selection.StartPosition = index;
                        Document.Selection.EndPosition = index + searchContext.SearchText.Length;
                    }
                }

                if (index == -1)
                {
                    Document.Selection.StartPosition = Document.Selection.EndPosition;
                    return false;
                }

                return true;
            }
        }

        public bool TryFindNextAndReplace(SearchContext searchContext, string replaceText, out bool regexError)
        {
            Document.Selection.EndPosition = Document.Selection.StartPosition;

            if (TryFindNextAndSelect(searchContext, stopAtEof: true, out regexError))
            {
                if (searchContext.UseRegex)
                {
                    replaceText = ExpandReplacementForSelectedMatch(searchContext, replaceText);
                }

                Document.Selection.SetText(TextSetOptions.None, replaceText);
                TryFindNextAndSelect(searchContext, stopAtEof: true, out _);
                return true;
            }

            return false;
        }

        public bool TryFindPreviousAndReplace(SearchContext searchContext, string replaceText, out bool regexError)
        {
            Document.Selection.StartPosition = Document.Selection.EndPosition;

            if (TryFindPreviousAndSelect(searchContext, stopAtBof: true, out regexError))
            {
                if (searchContext.UseRegex)
                {
                    replaceText = ExpandReplacementForSelectedMatch(searchContext, replaceText);
                }

                Document.Selection.SetText(TextSetOptions.None, replaceText);
                TryFindPreviousAndSelect(searchContext, stopAtBof: true, out _);
                return true;
            }

            return false;
        }

        public bool TryFindAndReplaceAll(SearchContext searchContext, string replaceText, out bool regexError)
        {
            regexError = false;

            List<TextReplacement> replacements;
            try
            {
                replacements = FindReplaceUtility.FindAll(GetText(), searchContext, replaceText);
            }
            catch (Exception)
            {
                regexError = true; // A bad pattern, or one that ran past the timeout
                return false;
            }

            if (replacements.Count == 0) return false;

            ReplaceRanges(replacements);
            return true;
        }

        /// <summary>
        /// Above this many replacements, Replace All sets the whole text at once. Each range edit makes the
        /// editor read the whole text again, so very many of them would be slower than one full restyle.
        /// </summary>
        private const int MaxRangeReplacements = 1000;

        /// <summary>
        /// Replaces each match in place, as one undo step. Only the changed lines get restyled, and the
        /// caret and the view stay on the same words.
        /// </summary>
        private void ReplaceRanges(IReadOnlyList<TextReplacement> replacements)
        {
            if (replacements.Count > MaxRangeReplacements)
            {
                SetText(FindReplaceUtility.Apply(GetText(), replacements), keepPosition: true);
                return;
            }

            var selectionStart = Document.Selection.StartPosition;
            var selectionEnd = Document.Selection.EndPosition;
            var scrollViewer = _contentScrollViewer;
            var horizontalOffset = scrollViewer?.HorizontalOffset;
            var verticalOffset = scrollViewer?.VerticalOffset;

            Document.BatchDisplayUpdates();
            Document.BeginUndoGroup();
            try
            {
                // From the end, so the positions of the matches before it stay valid
                for (var i = replacements.Count - 1; i >= 0; i--)
                {
                    var replacement = replacements[i];
                    Document.GetRange(replacement.Start, replacement.Start + replacement.Length).SetText(TextSetOptions.None, replacement.Text);
                }
            }
            finally
            {
                Document.EndUndoGroup();
                Document.ApplyDisplayUpdates();
            }

            Document.Selection.SetRange(
                FindReplaceUtility.MapPosition(selectionStart, replacements),
                FindReplaceUtility.MapPosition(selectionEnd, replacements));

            if (scrollViewer != null)
            {
                // The new text is laid out after this call, so scroll back once that is done
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low,
                    () => scrollViewer.ChangeView(horizontalOffset, verticalOffset, zoomFactor: null, disableAnimation: true));
            }
        }

        /// <summary>
        /// Single Replace in regex mode: expands "$1" and the other substitutions for the selected match,
        /// the same as Replace All does.
        /// </summary>
        private string ExpandReplacementForSelectedMatch(SearchContext searchContext, string replaceText)
        {
            var start = Document.Selection.StartPosition;
            var length = Document.Selection.EndPosition - start;

            try
            {
                var match = FindReplaceUtility.CreateRegex(searchContext).Match(FindReplaceUtility.ToRegexText(GetText()), start);
                if (match.Success && match.Index == start && match.Length == length)
                {
                    return FindReplaceUtility.ExpandRegexReplacement(match, replaceText);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(TextEditorCore)}] Failed to expand the regex replacement: {ex.Message}");
            }

            return FindReplaceUtility.UnescapeReplaceText(replaceText);
        }

        private bool TryFindNextAndSelectUsingRegex(string content, SearchContext searchContext, bool stopAtEof,
            out bool regexError)
        {
            try
            {
                regexError = false;
                content = FindReplaceUtility.ToRegexText(content);
                var regex = FindReplaceUtility.CreateRegex(searchContext);

                var match = regex.Match(content, Document.Selection.EndPosition);

                if (!match.Success && !stopAtEof)
                {
                    match = regex.Match(content, 0);
                }

                if (match.Success)
                {
                    var index = match.Index;
                    Document.Selection.StartPosition = index;
                    Document.Selection.EndPosition = index + match.Length;
                    return true;
                }
                else
                {
                    Document.Selection.StartPosition = Document.Selection.EndPosition;
                    return false;
                }
            }
            catch (Exception)
            {
                regexError = true;
                return false;
            }
        }

        private bool TryFindPreviousAndSelectUsingRegex(string content, SearchContext searchContext, out bool regexError, bool stopAtBof)
        {
            try
            {
                regexError = false;
                content = FindReplaceUtility.ToRegexText(content);
                var regex = FindReplaceUtility.CreateRegex(searchContext, rightToLeft: true);

                var match = regex.Match(content, Document.Selection.StartPosition);

                if (!match.Success && !stopAtBof)
                {
                    match = regex.Match(content, content.Length);
                }

                if (match.Success)
                {
                    var index = match.Index;
                    Document.Selection.StartPosition = index;
                    Document.Selection.EndPosition = index + match.Length;
                    return true;
                }
                else
                {
                    Document.Selection.StartPosition = Document.Selection.EndPosition;
                    return false;
                }
            }
            catch (Exception)
            {
                regexError = true;
                return false;
            }
        }
    }
}