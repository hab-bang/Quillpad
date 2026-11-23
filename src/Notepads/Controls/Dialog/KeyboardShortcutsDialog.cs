// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.Dialog
{
    using Windows.UI.Text;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;

    /// <summary>
    /// The list of keyboard shortcuts (Ctrl+/ or the main menu). The keys are the same in every language,
    /// the descriptions come from the string resources.
    /// </summary>
    public sealed class KeyboardShortcutsDialog : NotepadsDialog
    {
        private static readonly (string Keys, string Description)[] Shortcuts =
        {
            ("Ctrl+N, Ctrl+T", "Shortcut_NewTab"),
            ("Ctrl+Shift+N", "Shortcut_NewWindow"),
            ("Ctrl+O", "Shortcut_OpenFile"),
            ("Ctrl+Shift+O", "Shortcut_OpenFileFromFolder"),
            ("Ctrl+S", "Shortcut_Save"),
            ("Ctrl+Shift+S", "Shortcut_SaveAs"),
            ("Ctrl+W", "Shortcut_CloseTab"),
            ("Ctrl+Shift+T", "Shortcut_ReopenClosedTab"),
            ("Ctrl+Tab, Ctrl+Shift+Tab", "Shortcut_SwitchTab"),
            ("Ctrl+1 … Ctrl+9", "Shortcut_GoToTab"),
            ("Ctrl+F", "Shortcut_Find"),
            ("Ctrl+H, Ctrl+Shift+F", "Shortcut_Replace"),
            ("F3, Shift+F3", "Shortcut_FindNext"),
            ("Ctrl+G", "Shortcut_GoToLine"),
            ("Alt+O", "Shortcut_Outline"),
            ("Ctrl+B, Ctrl+I", "Shortcut_BoldItalic"),
            ("Ctrl+Z, Ctrl+Y", "Shortcut_UndoRedo"),
            ("Ctrl+D", "Shortcut_DuplicateLine"),
            ("Ctrl+J", "Shortcut_JoinLines"),
            ("Alt+Up, Alt+Down", "Shortcut_MoveLine"),
            ("F5", "Shortcut_InsertDateTime"),
            ("Ctrl+Plus, Ctrl+Minus, Ctrl+0", "Shortcut_Zoom"),
            ("Alt+Z", "Shortcut_WordWrap"),
            ("Alt+W", "Shortcut_TextWidth"),
            ("Alt+P", "Shortcut_Preview"),
            ("Alt+D", "Shortcut_ShowChanges"),
            ("Ctrl+Shift+R", "Shortcut_Reload"),
            ("F2", "Shortcut_Rename"),
            ("Ctrl+P, Ctrl+Shift+P", "Shortcut_Print"),
            ("F6", "Shortcut_StatusBar"),
            ("Alt+F", "Shortcut_Menu"),
            ("F1", "Shortcut_Settings"),
            ("F11", "Shortcut_FullScreen"),
            ("F12", "Shortcut_CompactOverlay"),
            ("Ctrl+/", "Shortcut_ShortcutList"),
        };

        public KeyboardShortcutsDialog()
        {
            var grid = new Grid { ColumnSpacing = 24, RowSpacing = 6 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (var row = 0; row < Shortcuts.Length; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var keys = new TextBlock { Text = Shortcuts[row].Keys, FontWeight = FontWeights.SemiBold, IsTextSelectionEnabled = true };
                var description = new TextBlock
                {
                    Text = ResourceLoader.GetString(Shortcuts[row].Description),
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                };

                Grid.SetRow(keys, row);
                Grid.SetRow(description, row);
                Grid.SetColumn(description, 1);
                grid.Children.Add(keys);
                grid.Children.Add(description);
            }

            Title = ResourceLoader.GetString("KeyboardShortcutsDialog_Title");
            Content = new ScrollViewer
            {
                Content = grid,
                MaxHeight = 480,
                Padding = new Thickness(0, 0, 16, 0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            CloseButtonText = ResourceLoader.GetString("KeyboardShortcutsDialog_CloseButtonText");
        }
    }
}
