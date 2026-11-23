// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.System;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Notepads.Controls.Dialog;
    using Notepads.Controls.FilePicker;
    using Notepads.Controls.TextEditor;
    using Notepads.Core;
    using Notepads.Services;
    using Notepads.Utilities;

    public sealed partial class NotepadsMainPage
    {
        private IWorkspaceManager _workspace;

        private IWorkspaceManager Workspace
        {
            get
            {
                if (_workspace != null) return _workspace;

                // Only the main window reopens its folder on the next launch, like the session snapshot
                _workspace = new WorkspaceManager(NotepadsCore, rememberFolder: App.IsPrimaryInstance && !App.IsGameBarWidget);
                _workspace.FolderChanged += OnWorkspaceFolderChanged;
                return _workspace;
            }
        }

        private void InitializeWorkspace()
        {
            MenuOpenFolderButton.Click += async (sender, args) => await OpenFolderAsync();
            MenuCloseFolderButton.Click += async (sender, args) => await CloseFolderAsync();

            if (App.IsGameBarWidget)
            {
                MenuOpenFolderButton.Visibility = Visibility.Collapsed;
            }
        }

        // Called once on launch, after the app session is restored
        private async Task<int> ResumeWorkspaceAsync(bool restoreTabs)
        {
            try
            {
                if (!await Workspace.TryResumeLastFolderAsync() || !restoreTabs) return 0;
                return await Workspace.RestoreTabsAsync();
            }
            catch (Exception ex)
            {
                // Never let the workspace stop the rest of the launch
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to resume the workspace: {ex}");
                return 0;
            }
        }

        private async Task OpenFolderAsync()
        {
            StorageFolder folder = null;

            try
            {
                folder = await FilePickerFactory.GetFolderPicker().PickSingleFolderAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to pick a folder: {ex.Message}");
            }

            if (folder == null)
            {
                NotepadsCore.FocusOnSelectedTextEditor();
                return;
            }

            await OpenFolderAsync(folder);
        }

        private async Task OpenFolderAsync(StorageFolder folder)
        {
            // Two windows on one folder would write over each other's list of open tabs. Check before asking
            // about unsaved changes. Opening the folder that is already open here only reloads its tabs.
            WorkspaceFolderLock folderLock = null;
            if (!Workspace.IsOpen(folder))
            {
                folderLock = WorkspaceFolderLock.TryAcquire(folder.Path);
                if (folderLock == null)
                {
                    var dialog = new FolderOpenInAnotherWindowDialog(folder.Name);
                    await DialogManager.OpenDialogAsync(dialog, awaitPreviousDialog: false);
                    if (!dialog.IsAborted) NotepadsCore.FocusOnSelectedTextEditor();
                    return;
                }
            }

            bool lockHandedOver = false;

            try
            {
                var opened = await ReplaceAllTabsAsync(() =>
                {
                    Workspace.Open(folder, folderLock);
                    lockHandedOver = true;
                    return Workspace.RestoreTabsAsync();
                });

                if (opened && MRUService.TryAdd(folder))
                {
                    await BuildOpenRecentButtonSubItemsAsync();
                }
            }
            finally
            {
                if (!lockHandedOver) folderLock?.Dispose(); // Cancelled: the folder stays free for other windows
            }
        }

        private async Task CloseFolderAsync()
        {
            await ReplaceAllTabsAsync(() =>
            {
                Workspace.Close();
                return Task.FromResult(0);
            });
        }

        /// <summary>
        /// Saves the current workspace, then swaps every open tab for the tabs <paramref name="openTabsAsync"/> opens.
        /// Returns false when the user cancelled.
        /// </summary>
        private async Task<bool> ReplaceAllTabsAsync(Func<Task<int>> openTabsAsync)
        {
            // A file deleted outside Quillpad makes its tab modified, so the question below covers it
            await RefreshFileStatusOfUnmodifiedTabsAsync(NotepadsCore.GetAllTextEditors());

            // Untitled drafts belong to no folder, so they stay open. "Don't save" would delete them, even with session backup on.
            var closingEditors = NotepadsCore.GetAllTextEditors()
                .Where(editor => editor.EditingFile != null || !editor.IsModified)
                .ToArray();

            if (!await SaveOrDiscardUnsavedChangesAsync(closingEditors))
            {
                NotepadsCore.FocusOnSelectedTextEditor();
                return false;
            }

            // The closing tabs are locked from here on: text typed into them while the new tabs load would close with them
            try
            {
                await Workspace.SaveAsync();

                // Open the new tabs before closing the old ones, so the window never runs out of tabs
                int openedCount = await openTabsAsync();

                if (openedCount == 0 && closingEditors.Length == NotepadsCore.GetNumberOfOpenedTextEditors())
                {
                    NotepadsCore.OpenNewTextEditor(_defaultNewFileName);
                }

                foreach (var editor in closingEditors)
                {
                    NotepadsCore.DeleteTextEditor(editor);
                }
            }
            finally
            {
                SetEditingLocked(closingEditors, false); // Only the tabs that are still open, after a failure
            }

            NotepadsCore.FocusOnSelectedTextEditor();
            return true;
        }

        // Asks about the unsaved tabs among the closing ones. On "Save all" or "Don't save", the closing tabs
        // are locked before anything else runs, so no text typed during the saves or the switch gets lost.
        private async Task<bool> SaveOrDiscardUnsavedChangesAsync(IReadOnlyList<ITextEditor> closingEditors)
        {
            if (!closingEditors.Any(editor => editor.IsModified))
            {
                SetEditingLocked(closingEditors, true);
                return true;
            }

            HideAllOpenFlyouts();

            var result = await DialogManager.OpenDialogAsync(new WorkspaceSaveReminderDialog(), awaitPreviousDialog: false);

            if (result != ContentDialogResult.Primary && result != ContentDialogResult.Secondary) return false; // Cancel

            SetEditingLocked(closingEditors, true);
            if (result == ContentDialogResult.Secondary) return true; // Don't save

            var allSaved = true;

            foreach (var editor in closingEditors)
            {
                if (!await SaveAsync(editor, saveAs: false, ignoreUnmodifiedDocument: true, rebuildOpenRecentItems: false))
                {
                    allSaved = false; // The user cancelled a save picker, or the save failed
                    break;
                }
            }

            if (!allSaved)
            {
                SetEditingLocked(closingEditors, false);
            }

            // Files saved before a cancel are in the recent list too
            await BuildOpenRecentButtonSubItemsAsync();
            return allSaved;
        }

        private void SetEditingLocked(IEnumerable<ITextEditor> textEditors, bool locked)
        {
            var openEditors = NotepadsCore.GetAllTextEditors();

            foreach (var textEditor in textEditors)
            {
                if (!openEditors.Contains(textEditor)) continue;
                textEditor.SetEditingLocked(locked || _editingLockedForUpdate); // An update install keeps its own lock
            }
        }

        private void OnWorkspaceFolderChanged(object sender, EventArgs e)
        {
            UpdateWorkspaceIndicator();

            var selectedEditor = NotepadsCore.GetSelectedTextEditor();
            if (selectedEditor != null)
            {
                UpdateApplicationTitle(selectedEditor);
            }
        }

        private void UpdateWorkspaceIndicator()
        {
            if (StatusBar == null) return;

            var folder = Workspace.Folder;

            if (folder == null)
            {
                WorkspaceIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            WorkspaceIndicatorText.Text = folder.Name;
            ToolTipService.SetToolTip(WorkspaceIndicator, folder.Path);
            WorkspaceIndicator.Visibility = Visibility.Visible;
        }

        private async void WorkspaceIndicatorOpenFile_OnClick(object sender, RoutedEventArgs e)
        {
            await ShowWorkspaceFileListAsync();
        }

        // Also Ctrl+Shift+O
        private async Task ShowWorkspaceFileListAsync()
        {
            var folder = Workspace.Folder;
            if (folder == null) return;

            // The dialog shows at once and fills in when the listing ends
            var dialog = new WorkspaceFileOpenDialog(folder.Name, WorkspaceUtility.GetFilesAsync(folder));
            await DialogManager.OpenDialogAsync(dialog, awaitPreviousDialog: false);

            if (dialog.FilesToOpen.Count > 0)
            {
                await OpenFilesAsync(dialog.FilesToOpen);
            }
            else if (!dialog.IsAborted)
            {
                NotepadsCore.FocusOnSelectedTextEditor();
            }
        }

        private async void WorkspaceIndicatorFindInFolder_OnClick(object sender, RoutedEventArgs e)
        {
            await ShowFolderSearchAsync();
        }

        private async Task ShowFolderSearchAsync()
        {
            var folder = Workspace.Folder;
            if (folder == null)
            {
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("Workspace_NotificationMsg_NoFolderOpen"), 2500);
                return;
            }

            var dialog = new FolderSearchDialog(folder.Name, query => SearchFolderAsync(folder, query));
            await DialogManager.OpenDialogAsync(dialog, awaitPreviousDialog: false);

            var result = dialog.SelectedResult;
            if (result == null)
            {
                if (!dialog.IsAborted) NotepadsCore.FocusOnSelectedTextEditor();
                return;
            }

            var textEditor = NotepadsCore.GetTextEditor(result.File);
            if (textEditor != null)
            {
                NotepadsCore.SwitchTo(textEditor);
            }
            else if (await OpenFileAsync(result.File))
            {
                textEditor = NotepadsCore.GetTextEditor(result.File);
            }

            textEditor?.GoToLine(result.Match.Line);
        }

        // Open tabs are searched as they are now, with their unsaved changes. Other files are read from disk.
        private async Task<IReadOnlyList<FolderSearchResult>> SearchFolderAsync(StorageFolder folder, string query)
        {
            var results = new List<FolderSearchResult>();

            foreach (var file in await WorkspaceUtility.GetFilesAsync(folder))
            {
                var extension = Path.GetExtension(file.RelativePath).ToLowerInvariant();
                if (!FileExtensionProvider.AllSupportedFileExtensions.Contains(extension)) continue;

                var text = NotepadsCore.GetTextEditor(file.File)?.GetText();
                if (text == null)
                {
                    try
                    {
                        text = (await FileSystemUtility.ReadFileAsync(file.File, ignoreFileSizeLimit: false)).Content;
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] Find in Folder skipped a file: {ex.Message}", consoleOnly: true);
                        continue;
                    }
                }

                foreach (var match in FolderSearchUtility.FindInText(text, query, matchCase: false, FolderSearchDialog.MaxResults - results.Count))
                {
                    results.Add(new FolderSearchResult(file.File, file.RelativePath, match));
                }

                if (results.Count >= FolderSearchDialog.MaxResults) break;
            }

            return results;
        }

        private async void WorkspaceIndicatorOpenInFileExplorer_OnClick(object sender, RoutedEventArgs e)
        {
            var folder = Workspace.Folder;
            if (folder == null) return;

            try
            {
                await Launcher.LaunchFolderAsync(folder);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to open the workspace folder: {ex.Message}");
            }
        }

        private async void WorkspaceIndicatorCloseFolder_OnClick(object sender, RoutedEventArgs e)
        {
            await CloseFolderAsync();
        }
    }
}
