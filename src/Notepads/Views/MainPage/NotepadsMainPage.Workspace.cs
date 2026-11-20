// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.System;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Notepads.Controls.Dialog;
    using Notepads.Controls.FilePicker;
    using Notepads.Core;
    using Notepads.Models;
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
            if (!await Workspace.TryResumeLastFolderAsync() || !restoreTabs) return 0;
            return await Workspace.RestoreTabsAsync();
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
            var opened = await ReplaceAllTabsAsync(() =>
            {
                Workspace.Open(folder);
                return Workspace.RestoreTabsAsync();
            });

            if (opened && MRUService.TryAdd(folder))
            {
                await BuildOpenRecentButtonSubItemsAsync();
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
            if (!await SaveOrDiscardUnsavedChangesAsync())
            {
                NotepadsCore.FocusOnSelectedTextEditor();
                return false;
            }

            await Workspace.SaveAsync();

            var previousEditors = NotepadsCore.GetAllTextEditors();

            // Open the new tabs before closing the old ones, so the window never runs out of tabs
            if (await openTabsAsync() == 0)
            {
                NotepadsCore.OpenNewTextEditor(_defaultNewFileName);
            }

            foreach (var editor in previousEditors)
            {
                NotepadsCore.DeleteTextEditor(editor);
            }

            NotepadsCore.FocusOnSelectedTextEditor();
            return true;
        }

        private async Task<bool> SaveOrDiscardUnsavedChangesAsync()
        {
            if (!NotepadsCore.HaveUnsavedTextEditor()) return true;

            HideAllOpenFlyouts();

            var result = await DialogManager.OpenDialogAsync(new WorkspaceSaveReminderDialog(), awaitPreviousDialog: false);

            if (result == ContentDialogResult.Secondary) return true; // Don't save
            if (result != ContentDialogResult.Primary) return false; // Cancel

            foreach (var editor in NotepadsCore.GetAllTextEditors())
            {
                if (!await SaveAsync(editor, saveAs: false, ignoreUnmodifiedDocument: true, rebuildOpenRecentItems: false))
                {
                    return false; // The user cancelled a save picker
                }
            }

            await BuildOpenRecentButtonSubItemsAsync();
            return true;
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
            var folder = Workspace.Folder;
            if (folder == null) return;

            IReadOnlyList<WorkspaceFile> files;

            try
            {
                files = await WorkspaceUtility.GetFilesAsync(folder);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to list the workspace files: {ex.Message}");
                files = Array.Empty<WorkspaceFile>();
            }

            var dialog = new WorkspaceFileOpenDialog(folder.Name, files);
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
