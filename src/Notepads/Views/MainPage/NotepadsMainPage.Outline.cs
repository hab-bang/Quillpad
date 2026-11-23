// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using System.Linq;
    using Windows.UI.Core;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
    using Notepads.Services;
    using Notepads.Utilities;

    /// <summary>
    /// The outline: a menu of the headings and scene breaks of the current file. Picking one jumps to it.
    /// It is built when opened, so it costs nothing while typing.
    /// </summary>
    public sealed partial class NotepadsMainPage
    {
        private const int MaxOutlineItems = 300;

        private void ShowOutline()
        {
            var textEditor = NotepadsCore.GetSelectedTextEditor();
            if (textEditor == null || !textEditor.IsEditorEnabled()) return;

            var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
            var presenterStyle = new Style(typeof(MenuFlyoutPresenter));
            presenterStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, 0));
            presenterStyle.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty, 600));
            flyout.MenuFlyoutPresenterStyle = presenterStyle;

            var outline = OutlineUtility.GetOutline(textEditor.GetText());
            var picked = false;

            if (outline.Count == 0)
            {
                flyout.Items?.Add(new MenuFlyoutItem { Text = _resourceLoader.GetString("Outline_EmptyText"), IsEnabled = false });
            }

            foreach (var item in outline.Take(MaxOutlineItems))
            {
                var text = item.Kind == OutlineItemKind.Heading
                    ? item.Title
                    : string.IsNullOrEmpty(item.Title)
                        ? _resourceLoader.GetString("Outline_SceneBreakText")
                        : string.Format(_resourceLoader.GetString("Outline_SceneBreakWithTextText"), item.Title);

                var menuItem = new MenuFlyoutItem
                {
                    Text = text,
                    Padding = new Thickness(12 + (item.Level - 1) * 16, 8, 12, 9),
                    Opacity = item.Kind == OutlineItemKind.SceneBreak ? 0.75 : 1.0,
                };
                var line = item.Line + 1;
                menuItem.Click += (sender, args) =>
                {
                    picked = true;
                    textEditor.GoToLine(line);
                };
                flyout.Items?.Add(menuItem);
            }

            flyout.Closed += (sender, args) =>
            {
                // A jump focuses the editor itself. Focusing again here would scroll back to where it was.
                if (!picked) NotepadsCore.FocusOnSelectedTextEditor();
            };

            // From the main menu, wait until it has closed
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => flyout.ShowAt(MainMenuButton));
        }

        private async void ShowWorkspaceFileList()
        {
            if (Workspace.Folder == null)
            {
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("Workspace_NotificationMsg_NoFolderOpen"), 2500);
                return;
            }

            await ShowWorkspaceFileListAsync();
        }
    }
}
