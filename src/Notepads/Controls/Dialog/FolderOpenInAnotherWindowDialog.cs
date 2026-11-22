// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.Dialog
{
    /// <summary>
    /// Shown instead of opening a folder that another Quillpad window has open. Two windows on one
    /// folder would write over each other's list of open tabs.
    /// </summary>
    public sealed class FolderOpenInAnotherWindowDialog : NotepadsDialog
    {
        public FolderOpenInAnotherWindowDialog(string folderName)
        {
            Title = ResourceLoader.GetString("FolderOpenInAnotherWindowDialog_Title");
            Content = string.Format(ResourceLoader.GetString("FolderOpenInAnotherWindowDialog_Content"), folderName);
            PrimaryButtonText = ResourceLoader.GetString("FileOpenErrorDialog_PrimaryButtonText");
        }
    }
}
