// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.TextEditor
{
    using Notepads.Extensions;
    using Notepads.Services;
    using Notepads.Utilities;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;

    /// <summary>
    /// Narrows and centers the editor column according to <see cref="AppSettingsService.EditorTextWidth"/>.
    /// The editor control itself is resized (not padded) so that tools reading its on-screen bounds,
    /// such as the Grammarly for Windows tab, see the narrower column.
    /// </summary>
    public sealed partial class TextEditor
    {
        // Width of the vertical scroll bar when it expands on hover. It overlays the text instead of taking
        // its own space, so a narrowed column reserves room for it to keep it from covering the line ends.
        private const double ExpandedScrollBarWidth = 16;

        private Thickness _fullWidthEditorPadding;

        private void HookTextWidthEvents()
        {
            _fullWidthEditorPadding = TextEditorCore.Padding;
            ApplyTextWidth(AppSettingsService.EditorTextWidth);
            AppSettingsService.OnTextWidthChanged += AppSettingsService_OnTextWidthChanged;
            TextEditorCore.SizeChanged += TextEditorCore_OnSizeChangedForTypewriter;
        }

        public void SetTypewriterMode(bool enabled)
        {
            if (TextEditorCore.IsTypewriterModeEnabled == enabled) return;
            TextEditorCore.IsTypewriterModeEnabled = enabled;
            ApplyTextWidth(AppSettingsService.EditorTextWidth);
        }

        // The room below the text is half the editor's height
        private void TextEditorCore_OnSizeChangedForTypewriter(object sender, SizeChangedEventArgs e)
        {
            if (TextEditorCore.IsTypewriterModeEnabled && e.PreviousSize.Height != e.NewSize.Height)
            {
                ApplyTextWidth(AppSettingsService.EditorTextWidth);
            }
        }

        private void UnhookTextWidthEvents()
        {
            AppSettingsService.OnTextWidthChanged -= AppSettingsService_OnTextWidthChanged;
            TextEditorCore.SizeChanged -= TextEditorCore_OnSizeChangedForTypewriter;
        }

        private async void AppSettingsService_OnTextWidthChanged(object sender, TextWidth textWidth)
        {
            await Dispatcher.CallOnUIThreadAsync(() => ApplyTextWidth(textWidth));
        }

        private void ApplyTextWidth(TextWidth textWidth)
        {
            var editorFraction = TextWidthUtility.GetWidthFraction(textWidth);
            var marginFraction = (1.0 - editorFraction) / 2;

            TextWidthLeftMarginColumnDefinition.Width = new GridLength(marginFraction, GridUnitType.Star);
            TextWidthEditorColumnDefinition.Width = new GridLength(editorFraction, GridUnitType.Star);
            TextWidthRightMarginColumnDefinition.Width = new GridLength(marginFraction, GridUnitType.Star);

            var padding = textWidth == TextWidth.Full
                ? _fullWidthEditorPadding
                : new Thickness(
                    _fullWidthEditorPadding.Left,
                    _fullWidthEditorPadding.Top,
                    _fullWidthEditorPadding.Right + ExpandedScrollBarWidth,
                    _fullWidthEditorPadding.Bottom);

            // Typewriter mode: room below the text, so the last line can scroll up to the middle too
            if (TextEditorCore.IsTypewriterModeEnabled)
            {
                padding.Bottom += TypewriterUtility.GetBottomPadding(TextEditorCore.ActualHeight, TextEditorCore.GetSingleLineHeight());
            }

            TextEditorCore.Padding = padding;
        }

        // The side margins are not part of the editor control, so forward mouse wheel scrolling to it
        private void TextWidthContainer_OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource != TextWidthContainer) return;

            var pointerProperties = e.GetCurrentPoint(TextWidthContainer).Properties;
            if (pointerProperties.IsHorizontalMouseWheel) return;

            TextEditorCore.ScrollVerticallyBy(-pointerProperties.MouseWheelDelta);
            e.Handled = true;
        }

        // Clicking a side margin should behave like clicking empty editor space
        private void TextWidthContainer_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            if (e.OriginalSource != TextWidthContainer) return;

            Focus();
            e.Handled = true;
        }
    }
}
