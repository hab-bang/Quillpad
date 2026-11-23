// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.TextEditor
{
    using Notepads.Services;
    using Notepads.Utilities;
    using Windows.UI.Core;
    using Windows.UI.Text;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;

    /// <summary>
    /// Curly quotes, em dashes and ellipses while typing in markdown and plain text files (a setting, off by default).
    /// The typed character goes in first, and the replacement is a second undo step, so Ctrl+Z right after
    /// gives back exactly what was typed. Only real key presses count: an undo or a paste is never replaced.
    /// </summary>
    public partial class TextEditorCore
    {
        private void HookSmartPunctuationEvents()
        {
            CharacterReceived += OnCharacterReceivedForSmartPunctuation;
        }

        private void UnhookSmartPunctuationEvents()
        {
            CharacterReceived -= OnCharacterReceivedForSmartPunctuation;
        }

        private void OnCharacterReceivedForSmartPunctuation(UIElement sender, CharacterReceivedRoutedEventArgs args)
        {
            if (!AppSettingsService.IsSmartPunctuationEnabled) return;
            if (FileType != FileType.MarkdownFile && FileType != FileType.TextFile) return;

            var typed = args.Character;
            if (!SmartPunctuationUtility.IsTrigger(typed)) return;

            // The character is in the text once this event is done
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => ApplySmartPunctuation(typed));
        }

        private void ApplySmartPunctuation(char typed)
        {
            if (_isTextComposing || !IsEnabled) return;

            var selection = Document.Selection;
            if (selection.StartPosition != selection.EndPosition) return;

            var caret = selection.StartPosition;
            var text = GetText();
            if (caret <= 0 || caret > text.Length || text[caret - 1] != typed) return;

            var edit = SmartPunctuationUtility.GetReplacement(text, caret);
            if (!edit.HasValue) return;
            if (FileType == FileType.MarkdownFile && IsInFencedCode(caret - 1)) return;

            Document.BeginUndoGroup();
            Document.GetRange(edit.Value.Start, edit.Value.Start + edit.Value.Length).SetText(TextSetOptions.None, edit.Value.Replacement);
            Document.EndUndoGroup();
            Document.Selection.SetRange(edit.Value.SelectionStart, edit.Value.SelectionStart);
        }

        private bool IsInFencedCode(int position)
        {
            var lineIndex = Document.GetRange(position, position).GetIndex(TextRangeUnit.Paragraph) - 1;
            if (lineIndex < 0) return false;

            // The live styling knows the code blocks already
            var contexts = _markdownStyledContexts;
            if (_isMarkdownStylingEnabled && contexts != null && lineIndex < contexts.Length)
            {
                return contexts[lineIndex] != MarkdownLineContext.Normal;
            }

            // Live styling is off, or the file is too long for it: find them now. This reads the whole text,
            // but only when a quote, dash or dot is typed in such a file.
            var all = MarkdownLiveStyleParser.GetLineContexts(GetText().Split(RichEditBoxDefaultLineEnding));
            return lineIndex < all.Length && all[lineIndex] != MarkdownLineContext.Normal;
        }
    }
}
