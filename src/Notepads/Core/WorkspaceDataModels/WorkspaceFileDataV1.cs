// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Core.WorkspaceDataModels
{
    using Notepads.Controls.TextEditor;

    /// <summary>
    /// One open tab of a workspace: which file, and where the user was in it.
    /// Only view state is kept; the text itself always comes from the file on disk.
    /// </summary>
    internal sealed class WorkspaceFileDataV1
    {
        public string Path { get; set; }

        public int SelectionStartPosition { get; set; }

        public int SelectionEndPosition { get; set; }

        public double ScrollViewerHorizontalOffset { get; set; }

        public double ScrollViewerVerticalOffset { get; set; }

        public double FontZoomFactor { get; set; } = 1;

        public bool WrapWord { get; set; } = true;

        public static WorkspaceFileDataV1 From(string path, TextEditorStateMetaData state)
        {
            return new WorkspaceFileDataV1
            {
                Path = path,
                SelectionStartPosition = state.SelectionStartPosition,
                SelectionEndPosition = state.SelectionEndPosition,
                ScrollViewerHorizontalOffset = state.ScrollViewerHorizontalOffset,
                ScrollViewerVerticalOffset = state.ScrollViewerVerticalOffset,
                FontZoomFactor = state.FontZoomFactor,
                WrapWord = state.WrapWord,
            };
        }

        public TextEditorStateMetaData ToEditorState()
        {
            return new TextEditorStateMetaData
            {
                SelectionStartPosition = SelectionStartPosition,
                SelectionEndPosition = SelectionEndPosition,
                ScrollViewerHorizontalOffset = ScrollViewerHorizontalOffset,
                ScrollViewerVerticalOffset = ScrollViewerVerticalOffset,
                FontZoomFactor = FontZoomFactor > 0 ? FontZoomFactor : 1,
                WrapWord = WrapWord,
            };
        }
    }
}
