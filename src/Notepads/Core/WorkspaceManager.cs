// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
        private bool _rememberFolder;
        private WorkspaceFolderLock _folderLock;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        private string _lastStateJson;

        // Set when the folder's state file exists but cannot be read, so it is never overwritten
        private bool _isStateUnreadable;

        // Word goals by relative path. Null until read from the state file.
        private Dictionary<string, int> _wordGoals;

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
                var folder = await futureAccessList.GetFolderAsync(LastFolderToken);

                // Another window opened it after this folder's window closed. Leave it there, and keep the token.
                var folderLock = WorkspaceFolderLock.TryAcquire(folder.Path);
                if (folderLock == null)
                {
                    LoggingService.LogInfo($"[{nameof(WorkspaceManager)}] The last workspace folder is open in another window, so it is not resumed.");
                    return false;
                }

                ReplaceFolderLock(folderLock);
                SetFolder(folder);
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

        public bool IsOpen(StorageFolder folder)
        {
            return folder != null && WorkspaceFolderLock.IsSameFolder(Folder?.Path, folder.Path);
        }

        public void Open(StorageFolder folder, WorkspaceFolderLock folderLock)
        {
            if (folderLock != null || !IsOpen(folder))
            {
                ReplaceFolderLock(folderLock);
            }

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

            ReplaceFolderLock(null);
            SetFolder(null);
        }

        public void StartRememberingFolder()
        {
            if (_rememberFolder) return;
            _rememberFolder = true;

            // With no folder open here, the token still names the folder of the main window that closed.
            // Its tabs come back with the session, so its folder comes back on the next launch too.
            if (Folder != null)
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace(LastFolderToken, Folder);
            }
        }

        private void ReplaceFolderLock(WorkspaceFolderLock folderLock)
        {
            _folderLock?.Dispose();
            _folderLock = folderLock;
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
                if (_wordGoals == null) _wordGoals = CopyWordGoals(workspaceData);
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

                // The goals of files that are not open come from the state file, so read it before writing over it
                await LoadWordGoalsAsync(folder);
                if (_isStateUnreadable) return;

                var workspaceData = BuildWorkspaceData(folder);

                // Do not create .quillpad in a folder that never had tabs to remember
                if (workspaceData.Files.Count == 0 && workspaceData.WordGoals.Count == 0 && _lastStateJson == null &&
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

        public async Task<int?> GetWordGoalAsync(string filePath)
        {
            var folder = Folder;
            var relativePath = folder == null ? null : WorkspaceUtility.GetRelativePath(folder, filePath);
            if (relativePath == null) return null;

            await _semaphoreSlim.WaitAsync();

            try
            {
                await LoadWordGoalsAsync(folder);
                return _wordGoals.TryGetValue(relativePath, out var goal) ? goal : (int?)null;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public async Task<bool> TrySetWordGoalAsync(string filePath, int? goal)
        {
            var folder = Folder;
            var relativePath = folder == null ? null : WorkspaceUtility.GetRelativePath(folder, filePath);
            if (relativePath == null) return false;

            await _semaphoreSlim.WaitAsync();

            try
            {
                await LoadWordGoalsAsync(folder);
                if (goal.HasValue) _wordGoals[relativePath] = goal.Value;
                else _wordGoals.Remove(relativePath);
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            await SaveAsync();
            return true;
        }

        // Call with the semaphore held
        private async Task LoadWordGoalsAsync(StorageFolder folder)
        {
            if (_wordGoals != null) return;

            try
            {
                var stateJson = await WorkspaceUtility.ReadStateAsync(folder);
                _wordGoals = stateJson == null
                    ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    : CopyWordGoals(Deserialize(stateJson));
            }
            catch (Exception ex)
            {
                _isStateUnreadable = true; // Same rule as RestoreTabsAsync: never write over a file that cannot be read
                _wordGoals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                LoggingService.LogError($"[{nameof(WorkspaceManager)}] Failed to read workspace state, it will not be overwritten: {ex.Message}");
            }
        }

        private static Dictionary<string, int> CopyWordGoals(WorkspaceDataV1 workspaceData)
        {
            var goals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (workspaceData.WordGoals == null) return goals;

            foreach (var pair in workspaceData.WordGoals)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0) goals[pair.Key] = pair.Value;
            }

            return goals;
        }

        private WorkspaceDataV1 BuildWorkspaceData(StorageFolder folder)
        {
            var workspaceData = new WorkspaceDataV1();

            // Sorted, so the file only changes when a goal does
            foreach (var pair in _wordGoals.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                workspaceData.WordGoals[pair.Key] = pair.Value;
            }

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
            _wordGoals = null;
            FolderChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
