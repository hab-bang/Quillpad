// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;

    /// <summary>
    /// The button after the tabs lists every open tab, for when there are too many to read their names.
    /// </summary>
    public sealed partial class NotepadsMainPage
    {
        private void ShowTabList()
        {
            var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };
            var presenterStyle = new Style(typeof(MenuFlyoutPresenter));
            presenterStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, 0));
            presenterStyle.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty, 600));
            flyout.MenuFlyoutPresenterStyle = presenterStyle;

            var selectedEditor = NotepadsCore.GetSelectedTextEditor();

            foreach (var textEditor in NotepadsCore.GetAllTextEditors())
            {
                var name = textEditor.EditingFileName ?? textEditor.FileNamePlaceholder;
                var item = new ToggleMenuFlyoutItem
                {
                    Text = textEditor.IsModified ? string.Format(_resourceLoader.GetString("App_TabList_ModifiedTabText"), name) : name,
                    IsChecked = textEditor == selectedEditor,
                };
                ToolTipService.SetToolTip(item, textEditor.EditingFilePath ?? name);

                var editor = textEditor;
                item.Click += (sender, args) => NotepadsCore.SwitchTo(editor);
                flyout.Items?.Add(item);
            }

            flyout.Closed += (sender, args) => NotepadsCore.FocusOnSelectedTextEditor();
            flyout.ShowAt(TabListButton);
        }
    }
}
