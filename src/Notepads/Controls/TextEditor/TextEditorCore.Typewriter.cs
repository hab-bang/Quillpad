// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.TextEditor
{
    using System;
    using Notepads.Utilities;
    using Windows.UI.Core;
    using Windows.UI.Text;

    /// <summary>
    /// Typewriter mode: the line with the caret stays in the middle of the editor while typing.
    /// Only the caret's own rectangle is read, so the cost does not grow with the file.
    /// A selection is never moved under the mouse; the view follows the caret again on the next key.
    /// </summary>
    public partial class TextEditorCore
    {
        private bool _isTypewriterModeEnabled;
        private bool _isCaretCenteringScheduled;

        public bool IsTypewriterModeEnabled
        {
            get => _isTypewriterModeEnabled;
            set
            {
                if (_isTypewriterModeEnabled == value) return;
                _isTypewriterModeEnabled = value;
                ScheduleCaretCentering();
            }
        }

        private void ScheduleCaretCentering()
        {
            if (!_isTypewriterModeEnabled || _isCaretCenteringScheduled || !_loaded) return;
            _isCaretCenteringScheduled = true;

            // After layout, so the caret rectangle is the new one
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                _isCaretCenteringScheduled = false;
                CenterCaret();
            });
        }

        private void CenterCaret()
        {
            if (!_isTypewriterModeEnabled || _contentScrollViewer == null) return;
            if (Document.Selection.Length != 0) return;

            Document.Selection.GetRect(PointOptions.ClientCoordinates, out var rect, out _);
            var lineHeight = rect.Height > 0 ? rect.Height : GetSingleLineHeight();

            var offset = TypewriterUtility.GetCenteredVerticalOffset(rect.Y + Padding.Top, lineHeight, _contentScrollViewer.ViewportHeight);
            if (Math.Abs(offset - _contentScrollViewer.VerticalOffset) < 1) return;

            _contentScrollViewer.ChangeView(null, offset, null, disableAnimation: true);
        }
    }
}
