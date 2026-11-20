// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.Storage.AccessCache;
    using Notepads.Controls.TextEditor;
    using Notepads.Core.WorkspaceDataModels;
    using Notepads.Services;
    using Notepads.Utilities;

    internal sealed class WorkspaceManager : IWorkspaceManager
    {
        // Not a Guid, so SessionManager never deletes it as an orphaned editor token
        private const string LastFolderToken = "WorkspaceFolder";

        private readonly INotepadsCore _notepadsCore;
        private readonly bool _rememberFolder;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        private string _lastStateJson;

        // Set when the folder's state file exists but cannot be read, so it is never overwritten
        private bool _isStateUnreadable;

        /// <param name="rememberFolder">
        /// True to reopen the folder on the next launch. Only one window should remember its folder.
        /// </param>
        public WorkspaceManager(INotepadsCore notepadsCore, bool rememberFolder)
        {
            _notepadsCore = notepadsCore;
            _rememberFolder = rememberFolder;
        }

        public event EventHandler FolderChanged;

        public StorageFolder Folder { get; private set; }

        public async Task<bool> TryResumeLastFolderAsync()
        {
            if (!_rememberFolder) return false;

            var futureAccessList = StorageApplicationPermissions.FutureAccessList;

            try
            {
                if (!futureAccessList.ContainsItem(LastFolderToken)) return false;
                SetFolder(await futureAccessList.GetFolderAsync(LastFolderToken));
                return true;
            }
            catch (Exception ex)
            {
                // The folder was renamed, moved or deleted, or its drive or share is offline.
                // Forget it, so later launches do not wait on it; it stays in Open Recent.
                LoggingService.LogError($"[{nameof(WorkspaceManager)}] Failed to resume the last workspace folder: {ex.Message}");
                futureAccessList.Remove(LastFolderToken);
                return false;
            }
        }

        public void Open(StorageFolder folder)
        {
            if (_rememberFolder)
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace(LastFolderToken, folder);
            }

            SetFolder(folder);
        }

        public void Close()
        {
            if (_rememberFolder && StorageApplicationPermissions.FutureAccessList.ContainsItem(LastFolderToken))
            {
                StorageApplicationPermissions.FutureAccessList.Remove(LastFolderToken);
            }

            SetFolder(null);
        }

        public async Task<int> RestoreTabsAsync()
        {
            // A save while the tabs are still opening would write a half list over the state file
            await _semaphoreSlim.WaitAsync();

            try
            {
                return await RestoreTabsInternalAsync();
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private async Task<int> RestoreTabsInternalAsync()
        {
            var folder = Folder;
            if (folder == null) return 0;

            WorkspaceDataV1 workspaceData;

            try
            {
                var stateJson = await WorkspaceUtility.ReadStateAsync(folder);
                if (stateJson == null) return 0;

                workspaceData = Deserialize(stateJson);
                _lastStateJson = stateJson;
            }
            catch (Exception ex)
            {
                // Damaged, hand-edited badly, or written by a newer Quillpad: keep the user's file as it is
                _isStateUnreadable = true;
                LoggingService.LogError($"[{nameof(WorkspaceManager)}] Failed to read workspace state, it will not be overwritten: {ex.Message}");
                return 0;
            }

            var editors = new List<ITextEditor>();
            Guid? selectedEditorId = null;

            foreach (var fileData in workspaceData.Files)
            {
                var file = await WorkspaceUtility.TryGetFileAsync(folder, fileData.Path);
                if (file == null) continue;

                try
                {
                    var editor = await _notepadsCore.CreateTextEditorAsync(Guid.NewGuid(), file);
                    editor.ResetEditorState(fileData.ToEditorState());
                    editors.Add(editor);

                    if (string.Equals(fileData.Path, workspaceData.SelectedFile, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedEditorId = editor.Id;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"[{nameof(WorkspaceManager)}] Failed to reopen workspace file: {ex.Message}");
                }
            }

            if (editors.Count > 0)
            {
                _notepadsCore.OpenTextEditors(editors.ToArray(), selectedEditorId);
            }

            LoggingService.LogInfo($"[{nameof(WorkspaceManager)}] {editors.Count} tab(s) restored from workspace.");

            return editors.Count;
        }

        public async Task SaveAsync()
        {
            await _semaphoreSlim.WaitAsync();

            try
            {
                var folder = Folder;
                if (folder == null || _isStateUnreadable) return;

                var workspaceData = BuildWorkspaceData(folder);

                // Do not create .quillpad in a folder that never had tabs to remember
                if (workspaceData.Files.Count == 0 && _lastStateJson == null &&
                    await WorkspaceUtility.ReadStateAsync(folder) == null) return;

                var stateJson = JsonSerializer.Serialize(workspaceData, new JsonSerializerOptions { WriteIndented = true });
                if (string.Equals(stateJson, _lastStateJson, StringComparison.Ordinal)) return;

                await WorkspaceUtility.WriteStateAsync(folder, stateJson);
                _lastStateJson = stateJson;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(WorkspaceManager)}] Failed to save workspace state: {ex.Message}");
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private WorkspaceDataV1 BuildWorkspaceData(StorageFolder folder)
        {
            var workspaceData = new WorkspaceDataV1();
            var selectedEditor = _notepadsCore.GetSelectedTextEditor();

            foreach (var editor in _notepadsCore.GetAllTextEditors())
            {
                // Untitled tabs and files outside the folder stay in the app session only
                var relativePath = WorkspaceUtility.GetRelativePath(folder, editor.EditingFilePath);
                if (relativePath == null) continue;

                workspaceData.Files.Add(WorkspaceFileDataV1.From(relativePath, editor.GetTextEditorStateMetaData()));

                if (editor == selectedEditor)
                {
                    workspaceData.SelectedFile = relativePath;
                }
            }

            return workspaceData;
        }

        private static WorkspaceDataV1 Deserialize(string stateJson)
        {
            var version = JsonDocument.Parse(stateJson).RootElement.GetProperty("Version").GetInt32();

            if (version != 1)
            {
                throw new NotSupportedException($"Unknown workspace state version: {version}");
            }

            var workspaceData = JsonSerializer.Deserialize<WorkspaceDataV1>(stateJson);

            if (workspaceData?.Files == null || workspaceData.Files.Exists(file => string.IsNullOrEmpty(file?.Path)))
            {
                throw new InvalidDataException("The workspace state has no file list, or a file without a path.");
            }

            return workspaceData;
        }

        private void SetFolder(StorageFolder folder)
        {
            Folder = folder;
            _lastStateJson = null;
            _isStateUnreadable = false;
            FolderChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
