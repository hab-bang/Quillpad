// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.Dialog
{
    /// <summary>
    /// Asked before a save writes over a file that another app changed since the tab loaded or saved it.
    /// Primary: overwrite, Secondary: compare with the file on disk, Close: cancel.
    /// </summary>
    public sealed class FileChangedOnDiskDialog : NotepadsDialog
    {
        public FileChangedOnDiskDialog(string fileNameOrPath)
        {
            Title = ResourceLoader.GetString("FileChangedOnDiskDialog_Title");
            Content = string.Format(ResourceLoader.GetString("FileChangedOnDiskDialog_Content"), fileNameOrPath);
            PrimaryButtonText = ResourceLoader.GetString("FileChangedOnDiskDialog_PrimaryButtonText");
            SecondaryButtonText = ResourceLoader.GetString("FileChangedOnDiskDialog_SecondaryButtonText");
            CloseButtonText = ResourceLoader.GetString("RevertAllChangesConfirmationDialog_CloseButtonText");
        }
    }
}
