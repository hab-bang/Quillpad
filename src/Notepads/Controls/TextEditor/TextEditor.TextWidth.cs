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
        private void HookTextWidthEvents()
        {
            ApplyTextWidth(AppSettingsService.EditorTextWidth);
            AppSettingsService.OnTextWidthChanged += AppSettingsService_OnTextWidthChanged;
        }

        private void UnhookTextWidthEvents()
        {
            AppSettingsService.OnTextWidthChanged -= AppSettingsService_OnTextWidthChanged;
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
