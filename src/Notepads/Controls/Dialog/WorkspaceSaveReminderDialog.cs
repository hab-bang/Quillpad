// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.Dialog
{
    using Windows.UI;
    using Windows.UI.Xaml;

    /// <summary>
    /// Asked before the open tabs are replaced by another folder's tabs.
    /// Primary: save all, Secondary: don't save, Close: cancel.
    /// </summary>
    public sealed class WorkspaceSaveReminderDialog : NotepadsDialog
    {
        public WorkspaceSaveReminderDialog()
        {
            Title = ResourceLoader.GetString("AppCloseSaveReminderDialog_Title");
            HorizontalAlignment = HorizontalAlignment.Center;
            Content = ResourceLoader.GetString("AppCloseSaveReminderDialog_Content");
            PrimaryButtonText = ResourceLoader.GetString("MainMenu_Button_SaveAll/Text");
            SecondaryButtonText = ResourceLoader.GetString("SetCloseSaveReminderDialog_SecondaryButtonText");
            CloseButtonText = ResourceLoader.GetString("AppCloseSaveReminderDialog_CloseButtonText");
            PrimaryButtonStyle = GetButtonStyle(Color.FromArgb(255, 38, 114, 201));
        }
    }
}
