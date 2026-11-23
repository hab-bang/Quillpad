// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.TextEditor
{
    using System;
    using Notepads.Utilities;
    using Windows.UI.Text;

    public partial class TextEditorCore
    {
        /// <summary>The type of the open file. Some writing aids work only in markdown or plain text.</summary>
        public FileType FileType { get; set; }

        /// <summary>
        /// Ctrl+B and Ctrl+I in a markdown file. In other files they do nothing, as before:
        /// the RichEditBox would make the text bold, and that format is never saved.
        /// </summary>
        private void ToggleMarkdownEmphasis(int marker)
        {
            if (FileType != FileType.MarkdownFile || !IsEnabled) return;

            var selection = Document.Selection;
            var start = Math.Min(selection.StartPosition, selection.EndPosition);
            var end = Math.Max(selection.StartPosition, selection.EndPosition);

            var edit = MarkdownEmphasisUtility.Toggle(GetText(), start, end, marker);
            if (edit.Length == 0 && edit.Replacement.Length == 0) return;

            // One range edit is one undo step
            Document.GetRange(edit.Start, edit.Start + edit.Length).SetText(TextSetOptions.None, edit.Replacement);
            Document.Selection.SetRange(edit.SelectionStart, edit.SelectionStart + edit.SelectionLength);
        }
    }
}
