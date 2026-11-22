// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.Dialog
{
    using Windows.UI.Xaml.Controls;

    /// <summary>
    /// Asked before a reload from disk replaces a tab that has unsaved edits.
    /// Primary: reload, Close: cancel.
    /// </summary>
    public sealed class ReloadFileConfirmationDialog : NotepadsDialog
    {
        public ReloadFileConfirmationDialog(string fileNameOrPath)
        {
            Title = ResourceLoader.GetString("ReloadFileConfirmationDialog_Title");
            Content = string.Format(ResourceLoader.GetString("ReloadFileConfirmationDialog_Content"), fileNameOrPath);
            PrimaryButtonText = ResourceLoader.GetString("ReloadFileConfirmationDialog_PrimaryButtonText");
            CloseButtonText = ResourceLoader.GetString("RevertAllChangesConfirmationDialog_CloseButtonText");

            // Reload throws away the edits, so a Space or Enter typed by habit must not pick it
            DefaultButton = ContentDialogButton.Close;
        }
    }
}
