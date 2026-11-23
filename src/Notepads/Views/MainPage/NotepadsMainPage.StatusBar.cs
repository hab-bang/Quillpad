// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using Windows.ApplicationModel.DataTransfer;
    using Windows.System;
    using Windows.UI.ViewManagement;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
    using Windows.UI.Xaml.Input;
    using Notepads.Controls.Dialog;
    using Notepads.Controls.TextEditor;
    using Notepads.Extensions;
    using Notepads.Services;
    using Notepads.Utilities;

    public sealed partial class NotepadsMainPage
    {
        private void InitializeStatusBar()
        {
            ShowHideStatusBar(AppSettingsService.ShowStatusBar);
            AppSettingsService.OnStatusBarVisibilityChanged += OnStatusBarVisibilityChanged;
            AppSettingsService.OnTextWidthChanged += OnTextWidthChanged;
        }

        private void SetupStatusBar(ITextEditor textEditor)
        {
            if (textEditor == null) return;
            UpdateApplicationTitle(textEditor);
            UpdateWorkspaceIndicator();
            UpdateFileModificationStateIndicator(textEditor);
            UpdatePathIndicator(textEditor);
            UpdateEditorModificationIndicator(textEditor);
            UpdateLineColumnIndicator(textEditor);
            UpdateWordCountIndicator(textEditor);
            UpdateFontZoomIndicator(textEditor);
            UpdateTextWidthIndicator(AppSettingsService.EditorTextWidth);
            UpdateLineEndingIndicator(textEditor.GetLineEnding());
            UpdateEncodingIndicator(textEditor.GetEncoding());
            UpdateShadowWindowIndicator();
        }

        public void ShowHideStatusBar(bool showStatusBar)
        {
            if (showStatusBar)
            {
                if (StatusBar == null)
                {
                    FindName("StatusBar");
                    BuildEncodingIndicatorFlyout();
                } // Lazy loading

                SetupStatusBar(NotepadsCore.GetSelectedTextEditor());
            }
            else
            {
                if (StatusBar != null)
                {
                    // If VS cannot find UnloadObject, ignore it. Reference: https://github.com/MicrosoftDocs/windows-uwp/issues/734
                    UnloadObject(StatusBar);
                }
            }
        }

        private async void OnStatusBarVisibilityChanged(object sender, bool visible)
        {
            await Dispatcher.CallOnUIThreadAsync(() =>
            {
                if (ApplicationView.GetForCurrentView().ViewMode != ApplicationViewMode.CompactOverlay) ShowHideStatusBar(visible);
            });
        }

        private async void OnTextWidthChanged(object sender, TextWidth textWidth)
        {
            await Dispatcher.CallOnUIThreadAsync(() => UpdateTextWidthIndicator(textWidth));
        }

        private void UpdateFileModificationStateIndicator(ITextEditor textEditor)
        {
            if (StatusBar == null) return;
            if (textEditor.FileModificationState == FileModificationState.Untouched)
            {
                FileModificationStateIndicatorIcon.Glyph = "";
                FileModificationStateIndicator.Visibility = Visibility.Collapsed;
            }
            else if (textEditor.FileModificationState == FileModificationState.Modified)
            {
                FileModificationStateIndicatorIcon.Glyph = "\uE7BA"; // Warning Icon
                ToolTipService.SetToolTip(FileModificationStateIndicator, _resourceLoader.GetString("TextEditor_FileModifiedOutsideIndicator_ToolTip"));
                FileModificationStateIndicator.Visibility = Visibility.Visible;
            }
            else if (textEditor.FileModificationState == FileModificationState.RenamedMovedOrDeleted)
            {
                FileModificationStateIndicatorIcon.Glyph = "\uE9CE"; // Unknown Icon
                ToolTipService.SetToolTip(FileModificationStateIndicator, _resourceLoader.GetString("TextEditor_FileRenamedMovedOrDeletedIndicator_ToolTip"));
                FileModificationStateIndicator.Visibility = Visibility.Visible;
            }
        }

        private void UpdatePathIndicator(ITextEditor textEditor)
        {
            if (StatusBar == null) return;
            PathIndicator.Text = textEditor.EditingFilePath ?? textEditor.FileNamePlaceholder;

            switch (textEditor.FileModificationState)
            {
                case FileModificationState.Untouched:
                    ToolTipService.SetToolTip(PathIndicator, PathIndicator.Text);
                    break;
                case FileModificationState.Modified:
                    ToolTipService.SetToolTip(PathIndicator, _resourceLoader.GetString("TextEditor_FileModifiedOutsideIndicator_ToolTip"));
                    break;
                case FileModificationState.RenamedMovedOrDeleted:
                    ToolTipService.SetToolTip(PathIndicator, _resourceLoader.GetString("TextEditor_FileRenamedMovedOrDeletedIndicator_ToolTip"));
                    break;
            }
        }

        private void UpdateEditorModificationIndicator(ITextEditor textEditor)
        {
            if (StatusBar == null) return;
            if (textEditor.IsModified)
            {
                ModificationIndicator.Text = _resourceLoader.GetString("TextEditor_ModificationIndicator_Text");
                ModificationIndicator.Visibility = Visibility.Visible;
                ModificationIndicator.IsTapEnabled = true;
            }
            else
            {
                ModificationIndicator.Text = string.Empty;
                ModificationIndicator.Visibility = Visibility.Collapsed;
                ModificationIndicator.IsTapEnabled = false;
            }
        }

        private void UpdateEncodingIndicator(Encoding encoding)
        {
            if (StatusBar == null) return;
            EncodingIndicator.Text = EncodingUtility.GetEncodingName(encoding);
        }

        private void UpdateLineEndingIndicator(LineEnding lineEnding)
        {
            if (StatusBar == null) return;
            LineEndingIndicator.Text = LineEndingUtility.GetLineEndingDisplayText(lineEnding);
        }

        private void UpdateLineColumnIndicator(ITextEditor textEditor)
        {
            if (StatusBar == null) return;
            textEditor.GetLineColumnSelection(out var startLineIndex, out _, out var startColumn, out _, out var selectedCount, out _);

            var wordSelected = selectedCount > 1
                ? _resourceLoader.GetString("TextEditor_LineColumnIndicator_FullText_PluralSelectedWord")
                : _resourceLoader.GetString("TextEditor_LineColumnIndicator_FullText_SingularSelectedWord");

            LineColumnIndicator.Text = selectedCount == 0
                ? string.Format(_resourceLoader.GetString("TextEditor_LineColumnIndicator_ShortText"), startLineIndex, startColumn)
                : string.Format(_resourceLoader.GetString("TextEditor_LineColumnIndicator_FullText"), startLineIndex, startColumn,
                    selectedCount, wordSelected);
        }

        private DispatcherTimer _wordCountUpdateTimer;

        // Typing fires a text change and a selection change for every key, so the count waits
        // until typing pauses. This keeps long drafts responsive.
        private void ScheduleWordCountIndicatorUpdate()
        {
            if (StatusBar == null) return;

            if (_wordCountUpdateTimer == null)
            {
                _wordCountUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                _wordCountUpdateTimer.Tick += (sender, args) =>
                {
                    _wordCountUpdateTimer.Stop();
                    var selectedEditor = NotepadsCore.GetSelectedTextEditor();
                    if (selectedEditor != null) UpdateWordCountIndicator(selectedEditor);
                };
            }

            _wordCountUpdateTimer.Stop();
            _wordCountUpdateTimer.Start();
        }

        private void GetWordCountStatistics(ITextEditor textEditor, out TextStatistics document, out TextStatistics? selection)
        {
            var text = textEditor.GetText();
            document = WordCountUtility.Count(text);
            selection = null;

            textEditor.GetTextSelectionPosition(out var start, out var end);
            if (end > start) selection = WordCountUtility.Count(text, start, end - start);
        }

        private void UpdateWordCountIndicator(ITextEditor textEditor)
        {
            if (StatusBar == null) return;

            GetWordCountStatistics(textEditor, out var document, out var selection);

            var total = WordCountUtility.FormatNumber(document.Words);

            if (selection.HasValue)
            {
                WordCountIndicator.Text = string.Format(_resourceLoader.GetString("TextEditor_WordCountIndicator_SelectionText"),
                    WordCountUtility.FormatNumber(selection.Value.Words), total);
            }
            else if (textEditor.WordGoal.HasValue)
            {
                WordCountIndicator.Text = string.Format(_resourceLoader.GetString("TextEditor_WordCountIndicator_GoalText"),
                    total, WordCountUtility.FormatNumber(textEditor.WordGoal.Value));
            }
            else
            {
                WordCountIndicator.Text = string.Format(document.Words == 1
                    ? _resourceLoader.GetString("TextEditor_WordCountIndicator_SingularText")
                    : _resourceLoader.GetString("TextEditor_WordCountIndicator_PluralText"), total);
            }
        }

        private void WordCountIndicatorClicked(ITextEditor selectedEditor)
        {
            GetWordCountStatistics(selectedEditor, out var document, out var selection);

            var builder = new StringBuilder();
            if (selection.HasValue)
            {
                AppendWordCountDetails(builder, _resourceLoader.GetString("TextEditor_WordCountFlyout_SelectionTitle"), selection.Value);
                builder.Append('\n');
            }
            AppendWordCountDetails(builder, _resourceLoader.GetString("TextEditor_WordCountFlyout_DocumentTitle"), document);

            builder.Append('\n');
            if (selectedEditor.WordGoal.HasValue)
            {
                builder.Append(string.Format(_resourceLoader.GetString("TextEditor_WordCountFlyout_Goal"),
                    WordCountUtility.FormatNumber(selectedEditor.WordGoal.Value),
                    WordGoalUtility.ProgressPercent(document.Words, selectedEditor.WordGoal.Value))).Append('\n');
            }

            var added = WordGoalUtility.WordsAddedThisSession(document.Words, selectedEditor.GetWordCountAtFirstOpen());
            builder.Append(string.Format(_resourceLoader.GetString("TextEditor_WordCountFlyout_SessionWords"),
                WordGoalUtility.FormatSigned(added, CultureInfo.CurrentCulture))).Append('\n');

            WordCountFlyoutText.Text = builder.ToString().TrimEnd('\n');
            WordGoalTextBox.Text = selectedEditor.WordGoal.HasValue ? WordCountUtility.FormatNumber(selectedEditor.WordGoal.Value) : string.Empty;
            FlyoutBase.ShowAttachedFlyout(WordCountIndicator);
        }

        private void WordGoalTextBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter) return;
            e.Handled = true;
            SetWordGoalButton_OnClick(sender, null);
        }

        private async void SetWordGoalButton_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedEditor = NotepadsCore.GetSelectedTextEditor();
            if (selectedEditor == null) return;

            int? goal = null;
            if (!string.IsNullOrWhiteSpace(WordGoalTextBox.Text))
            {
                if (!WordGoalUtility.TryParseGoal(WordGoalTextBox.Text, out var parsedGoal))
                {
                    NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_InvalidWordGoal"), 2500);
                    return;
                }

                goal = parsedGoal;
            }

            WordCountFlyout.Hide();
            await SetWordGoalAsync(selectedEditor, goal);
        }

        /// <summary>
        /// Files in the workspace folder keep their goal in the folder, other files in the app settings.
        /// An untitled tab keeps it in the session only.
        /// </summary>
        private async Task SetWordGoalAsync(ITextEditor textEditor, int? goal)
        {
            textEditor.WordGoal = goal;
            _wordGoalsLoaded.GetValue(textEditor, _ => new object());
            if (NotepadsCore.GetSelectedTextEditor() == textEditor) UpdateWordCountIndicator(textEditor);

            var filePath = textEditor.EditingFilePath;
            if (filePath == null) return;

            if (!await Workspace.TrySetWordGoalAsync(filePath, goal))
            {
                WordGoalStore.Set(filePath, goal);
            }
        }

        // Editors whose goal was looked up already, so a goal the user removed does not come back
        private readonly ConditionalWeakTable<ITextEditor, object> _wordGoalsLoaded = new ConditionalWeakTable<ITextEditor, object>();

        private async Task LoadWordGoalAsync(ITextEditor textEditor)
        {
            if (_wordGoalsLoaded.TryGetValue(textEditor, out _)) return;
            _wordGoalsLoaded.GetValue(textEditor, _ => new object());

            var filePath = textEditor.EditingFilePath;
            if (textEditor.WordGoal.HasValue || filePath == null) return;

            try
            {
                var goal = await Workspace.GetWordGoalAsync(filePath) ?? WordGoalStore.Get(filePath);
                if (goal == null || textEditor.WordGoal.HasValue || !NotepadsCore.GetAllTextEditors().Contains(textEditor)) return;

                textEditor.WordGoal = goal;
                if (NotepadsCore.GetSelectedTextEditor() == textEditor) UpdateWordCountIndicator(textEditor);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to load a word goal: {ex.Message}");
            }
        }

        private void AppendWordCountDetails(StringBuilder builder, string title, TextStatistics stats)
        {
            builder.Append(title).Append('\n');
            builder.Append(string.Format(_resourceLoader.GetString("TextEditor_WordCountFlyout_Words"), WordCountUtility.FormatNumber(stats.Words))).Append('\n');
            builder.Append(string.Format(_resourceLoader.GetString("TextEditor_WordCountFlyout_Characters"), WordCountUtility.FormatNumber(stats.Characters))).Append('\n');
            builder.Append(string.Format(_resourceLoader.GetString("TextEditor_WordCountFlyout_CharactersWithoutSpaces"), WordCountUtility.FormatNumber(stats.CharactersWithoutSpaces))).Append('\n');
        }

        private void UpdateFontZoomIndicator(ITextEditor textEditor)
        {
            if (StatusBar == null) return;
            var fontZoomFactor = Math.Round(textEditor.GetFontZoomFactor());
            FontZoomIndicator.Text = fontZoomFactor.ToString(CultureInfo.InvariantCulture) + "%";
            FontZoomSlider.Value = fontZoomFactor;
        }

        private void UpdateTextWidthIndicator(TextWidth textWidth)
        {
            if (StatusBar == null) return;
            // Reuses the flyout item labels so the indicator and its menu always read the same
            TextWidthIndicator.Text = _resourceLoader.GetString($"TextEditor_TextWidthIndicator_FlyoutItem_{textWidth}/Text");
            ToolTipService.SetToolTip(TextWidthIndicator, _resourceLoader.GetString("TextEditor_TextWidthIndicator_ToolTip"));
        }

        private void UpdateShadowWindowIndicator()
        {
            if (StatusBar == null) return;
            ShadowWindowIndicator.Visibility = !App.IsPrimaryInstance ? Visibility.Visible : Visibility.Collapsed;
            if (ShadowWindowIndicator.Visibility == Visibility.Visible)
            {
                ToolTipService.SetToolTip(ShadowWindowIndicator, _resourceLoader.GetString("App_ShadowWindowIndicator_Description"));
            }
        }

        private async void ModificationFlyoutSelection_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuFlyoutItem item)) return;

            var selectedTextEditor = NotepadsCore.GetSelectedTextEditor();
            if (selectedTextEditor == null) return;

            switch ((string)item.Tag)
            {
                case "PreviewTextChanges":
                    NotepadsCore.GetSelectedTextEditor().OpenSideBySideDiffViewer();
                    break;
                case "RevertAllChanges":
                    var fileName = selectedTextEditor.EditingFileName ?? selectedTextEditor.FileNamePlaceholder;
                    var revertAllChangesConfirmationDialog = new RevertAllChangesConfirmationDialog(
                        fileName,
                        confirmedAction: () =>
                         {
                             selectedTextEditor.CloseSideBySideDiffViewer();
                             NotepadsCore.GetSelectedTextEditor().RevertAllChanges();
                         });
                    await DialogManager.OpenDialogAsync(revertAllChangesConfirmationDialog, awaitPreviousDialog: true);
                    break;
            }
        }

        private async void ReloadFileFromDiskAsync(object sender, RoutedEventArgs e)
        {
            var selectedEditor = NotepadsCore.GetSelectedTextEditor();

            if (selectedEditor?.EditingFile != null &&
                selectedEditor.FileModificationState != FileModificationState.RenamedMovedOrDeleted &&
                await ConfirmReloadAsync(selectedEditor))
            {
                try
                {
                    if (await selectedEditor.ReloadFromEditingFileAsync())
                    {
                        NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_FileReloaded"), 1500);
                    }
                }
                catch (Exception ex)
                {
                    var fileOpenErrorDialog = new FileOpenErrorDialog(selectedEditor.EditingFilePath, ex.Message);
                    await DialogManager.OpenDialogAsync(fileOpenErrorDialog, awaitPreviousDialog: false);
                    if (!fileOpenErrorDialog.IsAborted)
                    {
                        NotepadsCore.FocusOnSelectedTextEditor();
                    }
                }
            }
        }

        // A reload replaces the tab's text with the file on disk. Ask first when unsaved edits would be lost.
        private async Task<bool> ConfirmReloadAsync(ITextEditor textEditor)
        {
            if (textEditor.NoChangesSinceLastSaved(compareTextOnly: true)) return true;

            var reloadFileConfirmationDialog = new ReloadFileConfirmationDialog(textEditor.EditingFilePath ?? textEditor.FileNamePlaceholder);
            var result = await DialogManager.OpenDialogAsync(reloadFileConfirmationDialog, awaitPreviousDialog: true);

            if (result == ContentDialogResult.Primary) return true;

            if (!reloadFileConfirmationDialog.IsAborted)
            {
                NotepadsCore.FocusOnSelectedTextEditor();
            }
            return false;
        }

        private void CopyFullPath(object sender, RoutedEventArgs e)
        {
            var selectedEditor = NotepadsCore.GetSelectedTextEditor();
            if (selectedEditor?.EditingFile == null) return;

            try
            {
                DataPackage dataPackage = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dataPackage.SetText(selectedEditor.EditingFile.Path);
                Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true, IsRoamable = true });
                Clipboard.Flush(); // This method allows the content to remain available after the application shuts down.
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_FileNameOrPathCopied"), 1500);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to copy full path: {ex.Message}");
            }
        }

        private async void OpenContainingFolderAsync(object sender, RoutedEventArgs e)
        {
            var selectedEditor = NotepadsCore.GetSelectedTextEditor();
            if (selectedEditor?.EditingFile == null) return;

            try
            {
                await Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(selectedEditor?.EditingFile.Path));
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to open containing folder: {ex.Message}");
            }
        }

        private async void RenameFileAsync(object sender, RoutedEventArgs e)
        {
            var selectedEditor = NotepadsCore.GetSelectedTextEditor();
            if (selectedEditor?.EditingFile == null) return;
            await RenameFileAsync(selectedEditor);
        }

        private void FontZoomIndicatorFlyoutSelection_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is AppBarButton button)) return;

            var selectedTextEditor = NotepadsCore.GetSelectedTextEditor();
            if (selectedTextEditor == null) return;

            switch (button.Name)
            {
                case "ZoomIn":
                    selectedTextEditor.SetFontZoomFactor(FontZoomSlider.Value % 10 > 0
                        ? Math.Ceiling(FontZoomSlider.Value / 10) * 10
                        : FontZoomSlider.Value + 10);
                    break;
                case "ZoomOut":
                    selectedTextEditor.SetFontZoomFactor(FontZoomSlider.Value % 10 > 0
                        ? Math.Floor(FontZoomSlider.Value / 10) * 10
                        : FontZoomSlider.Value - 10);
                    break;
                case "RestoreDefaultZoom":
                    selectedTextEditor.SetFontZoomFactor(100);
                    FontZoomIndicatorFlyout.Hide();
                    break;
            }
        }

        private void FontZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!(sender is Slider)) return;

            var selectedTextEditor = NotepadsCore.GetSelectedTextEditor();
            if (selectedTextEditor == null) return;

            if (Math.Abs(e.NewValue - e.OldValue) > 0.1)
            {
                selectedTextEditor.SetFontZoomFactor(e.NewValue);
            }
        }

        private void LineEndingSelection_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuFlyoutItem item)) return;

            var lineEnding = LineEndingUtility.GetLineEndingByName((string)item.Tag);
            var textEditor = NotepadsCore.GetSelectedTextEditor();
            if (textEditor != null)
            {
                NotepadsCore.ChangeLineEnding(textEditor, lineEnding);
            }
        }

        private void TextWidthSelection_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuFlyoutItem item)) return;

            if (Enum.TryParse((string)item.Tag, out TextWidth textWidth))
            {
                AppSettingsService.EditorTextWidth = textWidth;
            }
        }

        private void StatusBarComponent_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            var selectedEditor = NotepadsCore.GetSelectedTextEditor();
            if (selectedEditor == null) return;

            if (sender == FileModificationStateIndicator)
            {
                FileModificationStateIndicatorClicked(selectedEditor);
            }
            else if (sender == WorkspaceIndicator)
            {
                WorkspaceIndicator.ContextFlyout.ShowAt(WorkspaceIndicator);
            }
            else if (sender == PathIndicator && !string.IsNullOrEmpty(PathIndicator.Text))
            {
                PathIndicatorClicked(selectedEditor);
            }
            else if (sender == ModificationIndicator)
            {
                ModificationIndicatorClicked(selectedEditor);
            }
            else if (sender == LineColumnIndicator)
            {
                LineColumnIndicatorClicked(selectedEditor);
            }
            else if (sender == WordCountIndicator)
            {
                WordCountIndicatorClicked(selectedEditor);
            }
            else if (sender == FontZoomIndicator)
            {
                FontZoomIndicatorClicked(selectedEditor);
            }
            else if (sender == TextWidthIndicator)
            {
                TextWidthIndicator?.ContextFlyout.ShowAt(TextWidthIndicator);
            }
            else if (sender == LineEndingIndicator)
            {
                LineEndingIndicatorClicked(selectedEditor);
            }
            else if (sender == EncodingIndicator)
            {
                EncodingIndicatorClicked(selectedEditor);
            }
            else if (sender == ShadowWindowIndicator)
            {
                ShadowWindowIndicatorClicked();
            }
        }

        /// <summary>
        /// F6: the status bar items cannot take keyboard focus, so this menu lists them. Picking one opens
        /// the same menu as a click on it.
        /// </summary>
        private void ShowStatusBarMenu()
        {
            if (StatusBar == null || NotepadsCore.GetSelectedTextEditor() == null) return;

            var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.TopEdgeAlignedLeft };
            var presenterStyle = new Style(typeof(MenuFlyoutPresenter));
            presenterStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, 0));
            flyout.MenuFlyoutPresenterStyle = presenterStyle;
            var picked = false;

            void Add(FrameworkElement indicator, string text)
            {
                if (indicator.Visibility != Visibility.Visible || string.IsNullOrWhiteSpace(text)) return;

                var item = new MenuFlyoutItem { Text = text };
                item.Click += (sender, args) =>
                {
                    picked = true;
                    // After this menu has closed, or the two menus fight over the focus
                    _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () => StatusBarComponent_OnTapped(indicator, null));
                };
                flyout.Items?.Add(item);
            }

            Add(FileModificationStateIndicator, ToolTipService.GetToolTip(FileModificationStateIndicator) as string);
            Add(WorkspaceIndicator, WorkspaceIndicatorText.Text);
            Add(PathIndicator, PathIndicator.Text);
            Add(ModificationIndicator, ModificationIndicator.Text);
            Add(WordCountIndicator, WordCountIndicator.Text);
            Add(LineColumnIndicator, LineColumnIndicator.Text);
            Add(FontZoomIndicator, FontZoomIndicator.Text);
            Add(TextWidthIndicator, TextWidthIndicator.Text);
            Add(LineEndingIndicator, LineEndingIndicator.Text);
            Add(EncodingIndicator, EncodingIndicator.Text);
            Add(ShadowWindowIndicator, ToolTipService.GetToolTip(ShadowWindowIndicator) as string);

            // A picked item opens its own menu, which gives the focus back when it closes
            flyout.Closed += (sender, args) =>
            {
                if (!picked) NotepadsCore.FocusOnSelectedTextEditor();
            };
            flyout.ShowAt(StatusBar);
        }

        private void FileModificationStateIndicatorClicked(ITextEditor selectedEditor)
        {
            if (selectedEditor.FileModificationState == FileModificationState.Modified)
            {
                FileModificationStateIndicator.ContextFlyout.ShowAt(FileModificationStateIndicator);
            }
            else if (selectedEditor.FileModificationState == FileModificationState.RenamedMovedOrDeleted)
            {
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_FileRenamedMovedOrDeletedIndicator_ToolTip"), 2500);
            }
        }

        private async void PathIndicatorClicked(ITextEditor selectedEditor)
        {
            NotepadsCore.FocusOnSelectedTextEditor();

            PathIndicatorFlyoutCopyFullPathFlyoutItem.Text = _resourceLoader.GetString("Tab_ContextFlyout_CopyFullPathButtonDisplayText");
            PathIndicatorFlyoutOpenContainingFolderFlyoutItem.Text = _resourceLoader.GetString("Tab_ContextFlyout_OpenContainingFolderButtonDisplayText");
            PathIndicatorFlyoutFileRenameFlyoutItem.Text = _resourceLoader.GetString("Tab_ContextFlyout_RenameButtonDisplayText");

            if (selectedEditor.FileModificationState == FileModificationState.RenamedMovedOrDeleted ||
                (selectedEditor.EditingFile != null && FileSystemUtility.IsFileReadOnly(selectedEditor.EditingFile)))
            {
                PathIndicatorFlyoutFileRenameFlyoutItem.IsEnabled = false;
            }
            else
            {
                PathIndicatorFlyoutFileRenameFlyoutItem.IsEnabled = true;
            }

            if (App.IsGameBarWidget)
            {
                PathIndicatorFlyoutOpenContainingFolderFlyoutItem.Visibility = Visibility.Collapsed;
            }

            if (selectedEditor.FileModificationState == FileModificationState.Untouched)
            {
                if (selectedEditor.EditingFile != null)
                {
                    PathIndicator.ContextFlyout.ShowAt(FileModificationStateIndicator);
                }
                else
                {
                    await RenameFileAsync(selectedEditor);
                }
            }
            else if (selectedEditor.FileModificationState == FileModificationState.Modified)
            {
                PathIndicator.ContextFlyout.ShowAt(FileModificationStateIndicator);
            }
            else if (selectedEditor.FileModificationState == FileModificationState.RenamedMovedOrDeleted)
            {
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_FileRenamedMovedOrDeletedIndicator_ToolTip"), 2500);
            }
        }

        private void ModificationIndicatorClicked(ITextEditor selectedEditor)
        {
            PreviewTextChangesFlyoutItem.IsEnabled =
                !selectedEditor.NoChangesSinceLastSaved(compareTextOnly: true) &&
                selectedEditor.Mode != TextEditorMode.DiffPreview;
            ModificationIndicator?.ContextFlyout.ShowAt(ModificationIndicator);
        }

        private static void LineColumnIndicatorClicked(ITextEditor selectedEditor)
        {
            selectedEditor.ShowGoToControl();
        }

        private void FontZoomIndicatorClicked(ITextEditor _)
        {
            FontZoomIndicator?.ContextFlyout.ShowAt(FontZoomIndicator);
            FontZoomIndicatorFlyout.Opened += (sflyout, eflyout) => ToolTipService.SetToolTip(RestoreDefaultZoom, null);
        }

        private void LineEndingIndicatorClicked(ITextEditor _)
        {
            LineEndingIndicator?.ContextFlyout.ShowAt(LineEndingIndicator);
        }

        private void EncodingIndicatorClicked(ITextEditor selectedEditor)
        {
            var reopenWithEncoding = EncodingSelectionFlyout?.Items?.FirstOrDefault(i => i.Name.Equals("ReopenWithEncoding"));
            if (reopenWithEncoding != null)
            {
                reopenWithEncoding.IsEnabled = selectedEditor.EditingFile != null &&
                                               selectedEditor.FileModificationState !=
                                               FileModificationState.RenamedMovedOrDeleted;
            }

            EncodingIndicator?.ContextFlyout.ShowAt(EncodingIndicator);
        }

        private void ShadowWindowIndicatorClicked()
        {
            NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("App_ShadowWindowIndicator_Description"), 4000);
        }

        private void StatusBarFlyout_OnClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            NotepadsCore.FocusOnSelectedTextEditor();
        }

        private void BuildEncodingIndicatorFlyout()
        {
            if (StatusBar == null) return;

            if (EncodingSelectionFlyout.Items?.Count > 0)
            {
                return;
            }

            var reopenWithEncoding = new MenuFlyoutSubItem()
            {
                Text = _resourceLoader.GetString("TextEditor_EncodingIndicator_FlyoutItem_ReopenWithEncoding"),
                FlowDirection = FlowDirection.RightToLeft,
                Name = "ReopenWithEncoding"
            };

            var saveWithEncoding = new MenuFlyoutSubItem()
            {
                Text = _resourceLoader.GetString("TextEditor_EncodingIndicator_FlyoutItem_SaveWithEncoding"),
                FlowDirection = FlowDirection.RightToLeft,
                Name = "SaveWithEncoding"
            };

            // Add auto guess Encoding option in ReopenWithEncoding menu
            reopenWithEncoding.Items?.Add(CreateAutoGuessEncodingItem());
            reopenWithEncoding.Items?.Add(new MenuFlyoutSeparator());

            // Add suggested ANSI encodings
            var appAndSystemANSIEncodings = new HashSet<Encoding>();

            if (EncodingUtility.TryGetSystemDefaultANSIEncoding(out var systemDefaultANSIEncoding))
            {
                appAndSystemANSIEncodings.Add(systemDefaultANSIEncoding);
            }
            if (EncodingUtility.TryGetCurrentCultureANSIEncoding(out var currentCultureANSIEncoding))
            {
                appAndSystemANSIEncodings.Add(currentCultureANSIEncoding);
            }

            if (appAndSystemANSIEncodings.Count > 0)
            {
                foreach (var encoding in appAndSystemANSIEncodings)
                {
                    AddEncodingItem(encoding, reopenWithEncoding, saveWithEncoding);
                }
                reopenWithEncoding.Items?.Add(new MenuFlyoutSeparator());
                saveWithEncoding.Items?.Add(new MenuFlyoutSeparator());
            }

            // Add Unicode encodings
            var unicodeEncodings = new List<Encoding>
            {
                new UTF8Encoding(false), // "UTF-8"
                new UTF8Encoding(true), // "UTF-8-BOM"
                new UnicodeEncoding(false, true), // "UTF-16 LE BOM"
                new UnicodeEncoding(true, true), // "UTF-16 BE BOM"
            };

            foreach (var encoding in unicodeEncodings)
            {
                AddEncodingItem(encoding, reopenWithEncoding, saveWithEncoding);
            }

            // Add legacy ANSI encodings
            var ANSIEncodings = EncodingUtility.GetAllSupportedANSIEncodings();
            if (ANSIEncodings.Length > 0)
            {
                reopenWithEncoding.Items?.Add(new MenuFlyoutSeparator());
                saveWithEncoding.Items?.Add(new MenuFlyoutSeparator());

                var reopenWithEncodingOthers = new MenuFlyoutSubItem()
                {
                    Text = _resourceLoader.GetString("TextEditor_EncodingIndicator_FlyoutItem_MoreEncodings"),
                    FlowDirection = FlowDirection.RightToLeft,
                };

                var saveWithEncodingOthers = new MenuFlyoutSubItem()
                {
                    Text = _resourceLoader.GetString("TextEditor_EncodingIndicator_FlyoutItem_MoreEncodings"),
                    FlowDirection = FlowDirection.RightToLeft,
                };

                foreach (var encoding in ANSIEncodings)
                {
                    AddEncodingItem(encoding, reopenWithEncodingOthers, saveWithEncodingOthers);
                }

                reopenWithEncoding.Items?.Add(reopenWithEncodingOthers);
                saveWithEncoding.Items?.Add(saveWithEncodingOthers);
            }

            EncodingSelectionFlyout.Items?.Add(reopenWithEncoding);
            EncodingSelectionFlyout.Items?.Add(saveWithEncoding);
        }

        private MenuFlyoutItem CreateAutoGuessEncodingItem()
        {
            var autoGuessEncodingItem = new MenuFlyoutItem()
            {
                Text = _resourceLoader.GetString("TextEditor_EncodingIndicator_FlyoutItem_AutoGuessEncoding"),
                FlowDirection = FlowDirection.LeftToRight,
            };
            autoGuessEncodingItem.Click += async (sender, args) =>
            {
                var selectedTextEditor = NotepadsCore.GetSelectedTextEditor();
                var file = selectedTextEditor?.EditingFile;
                if (file == null || selectedTextEditor.FileModificationState == FileModificationState.RenamedMovedOrDeleted) return;

                Encoding encoding = null;

                try
                {
                    using (var inputStream = await file.OpenReadAsync())
                    using (var stream = inputStream.AsStreamForRead())
                    {
                        if (FileSystemUtility.TryGuessEncoding(stream, out var detectedEncoding))
                        {
                            encoding = detectedEncoding;
                        }
                    }
                }
                catch (Exception)
                {
                    encoding = null;
                }

                if (encoding == null)
                {
                    NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_EncodingCannotBeDetermined"), 2500);
                    return;
                }

                if (!await ConfirmReloadAsync(selectedTextEditor)) return;

                try
                {
                    if (await selectedTextEditor.ReloadFromEditingFileAsync(encoding))
                    {
                        NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_FileReloaded"), 1500);
                    }
                }
                catch (Exception ex)
                {
                    var fileOpenErrorDialog = new FileOpenErrorDialog(selectedTextEditor.EditingFilePath, ex.Message);
                    await DialogManager.OpenDialogAsync(fileOpenErrorDialog, awaitPreviousDialog: false);
                    if (!fileOpenErrorDialog.IsAborted)
                    {
                        NotepadsCore.FocusOnSelectedTextEditor();
                    }
                }
            };
            return autoGuessEncodingItem;
        }

        private void AddEncodingItem(Encoding encoding, MenuFlyoutSubItem reopenWithEncoding, MenuFlyoutSubItem saveWithEncoding)
        {
            const int EncodingMenuFlyoutItemHeight = 30;
            const int EncodingMenuFlyoutItemFontSize = 14;

            var reopenWithEncodingItem = new MenuFlyoutItem()
            {
                Text = EncodingUtility.GetEncodingName(encoding),
                FlowDirection = FlowDirection.LeftToRight,
                Height = EncodingMenuFlyoutItemHeight,
                FontSize = EncodingMenuFlyoutItemFontSize
            };
            reopenWithEncodingItem.Click += async (sender, args) =>
            {
                var selectedTextEditor = NotepadsCore.GetSelectedTextEditor();
                if (selectedTextEditor != null && await ConfirmReloadAsync(selectedTextEditor))
                {
                    try
                    {
                        if (await selectedTextEditor.ReloadFromEditingFileAsync(encoding))
                        {
                            NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_FileReloaded"), 1500);
                        }
                    }
                    catch (Exception ex)
                    {
                        var fileOpenErrorDialog = new FileOpenErrorDialog(selectedTextEditor.EditingFilePath, ex.Message);
                        await DialogManager.OpenDialogAsync(fileOpenErrorDialog, awaitPreviousDialog: false);
                        if (!fileOpenErrorDialog.IsAborted)
                        {
                            NotepadsCore.FocusOnSelectedTextEditor();
                        }
                    }
                }
            };
            reopenWithEncoding.Items?.Add(reopenWithEncodingItem);

            var saveWithEncodingItem = new MenuFlyoutItem()
            {
                Text = EncodingUtility.GetEncodingName(encoding),
                FlowDirection = FlowDirection.LeftToRight,
                Height = EncodingMenuFlyoutItemHeight,
                FontSize = EncodingMenuFlyoutItemFontSize
            };
            saveWithEncodingItem.Click += (sender, args) =>
            {
                NotepadsCore.GetSelectedTextEditor()?.TryChangeEncoding(encoding);
            };
            saveWithEncoding.Items?.Add(saveWithEncodingItem);
        }
    }
}