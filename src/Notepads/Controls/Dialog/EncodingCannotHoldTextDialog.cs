// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.Dialog
{
    using Windows.UI.Xaml.Controls;

    /// <summary>
    /// Asked before a save when the tab's encoding can't hold a character of the text. Without it,
    /// each such character is saved as "?". Primary: save as UTF-8, Close: cancel the save.
    /// </summary>
    public sealed class EncodingCannotHoldTextDialog : NotepadsDialog
    {
        public EncodingCannotHoldTextDialog(string fileName, string encodingName, string character, int lineNumber)
        {
            Title = ResourceLoader.GetString("EncodingCannotHoldTextDialog_Title");
            Content = string.Format(ResourceLoader.GetString("EncodingCannotHoldTextDialog_Content"), fileName, encodingName, character, lineNumber);
            PrimaryButtonText = ResourceLoader.GetString("EncodingCannotHoldTextDialog_PrimaryButtonText");
            CloseButtonText = ResourceLoader.GetString("RevertAllChangesConfirmationDialog_CloseButtonText");
            DefaultButton = ContentDialogButton.Close; // Someone who saved mid-sentence keeps typing
        }
    }
}
