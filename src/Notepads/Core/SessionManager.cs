// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.Storage.AccessCache;
    using Notepads.Controls.TextEditor;
    using Notepads.Core.SessionDataModels;
    using Notepads.Models;
    using Notepads.Services;
    using Notepads.Utilities;
    using Windows.ApplicationModel.Resources;
    using Windows.System;
    using Windows.UI.Xaml;

    public sealed class SessionDataCorruptedException : Exception
    {
        public SessionDataCorruptedException(string message) : base(message)
        {
        }
    }

    internal sealed class SessionManager : ISessionManager, IDisposable
    {
        private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(7);
        private readonly INotepadsCore _notepadsCore;
        private readonly SemaphoreSlim _semaphoreSlim;
        private readonly ConcurrentDictionary<Guid, TextEditorSessionDataV1> _sessionDataCache;
        private readonly List<string> _tabsNotRestored = new List<string>();
        private string _lastSessionJsonStr;
        private bool _loaded;
        private Timer _timer;

        private readonly string _backupFolderName;
        private readonly string _sessionMetaDataFileName;

        /// <summary>
        /// Do not call this constructor directly. Use <see cref="SessionUtility.GetSessionManager(INotepadsCore)" instead./>
        /// </summary>
        public SessionManager(INotepadsCore notepadsCore, string backupFolderName, string sessionMetaDataFileName)
        {
            _notepadsCore = notepadsCore;
            _backupFolderName = backupFolderName;
            _sessionMetaDataFileName = sessionMetaDataFileName;
            _semaphoreSlim = new SemaphoreSlim(1, 1);
            _sessionDataCache = new ConcurrentDictionary<Guid, TextEditorSessionDataV1>();
            _loaded = false;

            foreach (var editor in _notepadsCore.GetAllTextEditors())
            {
                BindEditorContentStateChangeEvent(this, editor);
            }

            _notepadsCore.TextEditorLoaded += BindEditorContentStateChangeEvent;
            _notepadsCore.TextEditorUnloaded += UnbindEditorContentStateChangeEvent;
        }

        public bool IsBackupEnabled { get; set; }

        public IReadOnlyList<string> TabsNotRestored => _tabsNotRestored;

        public async Task<int> LoadLastSessionAsync()
        {
            if (_loaded)
            {
                return 0; // Already loaded
            }

            bool sessionDataFileExists = await SessionUtility.IsSessionMetaDataFileExists(_sessionMetaDataFileName);

            if (!sessionDataFileExists)
            {
                return 0; // No session data found
            }

            NotepadsSessionDataV1 sessionData;

            try
            {
                string sessionDataContent = await SessionUtility.GetSerializedSessionMetaDataAsync(_sessionMetaDataFileName);
                var json = JsonDocument.Parse(sessionDataContent);
                var version = json.RootElement.GetProperty("Version").GetInt32();

                if (version == 1)
                {
                    sessionData = JsonSerializer.Deserialize<NotepadsSessionDataV1>(sessionDataContent);
                }
                else
                {
                    throw new Exception($"Invalid version found in session metadata: {version}");
                }
            }
            catch (Exception ex)
            {
                // Failed to load session data, throw exception to indicate session data is corrupted
                throw new SessionDataCorruptedException(ex.Message);
            }

            try
            {
                await AddBackupsNewerThanSessionAsync(sessionData);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(SessionManager)}] Failed to look for backups newer than the session: {ex.Message}");
            }

            IList<ITextEditor> recoveredEditor = new List<ITextEditor>();

            foreach (var textEditorData in sessionData.TextEditors)
            {
                ITextEditor textEditor;

                try
                {
                    textEditor = await RecoverTextEditorAsync(textEditorData);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"[{nameof(SessionManager)}] Failed to recover TextEditor: {ex.Message}");

                    // The next save would delete this tab's backups as orphans. Keep them, and say which tab it was.
                    await KeepBackupFilesAsync(textEditorData);
                    _tabsNotRestored.Add(BackupFileUtility.GetTabName(
                        textEditorData.EditingFileName,
                        textEditorData.StateMetaData?.FileNamePlaceholder,
                        ResourceLoader.GetForViewIndependentUse().GetString("TextEditor_DefaultNewFileName")));
                    continue;
                }

                if (textEditor != null)
                {
                    recoveredEditor.Add(textEditor);
                    _sessionDataCache.TryAdd(textEditor.Id, textEditorData);
                }
            }

            _notepadsCore.OpenTextEditors(recoveredEditor.ToArray(), sessionData.SelectedTextEditor);
            _notepadsCore.SetTabScrollViewerHorizontalOffset(sessionData.TabScrollViewerHorizontalOffset);

            _loaded = true;

            LoggingService.LogInfo($"[{nameof(SessionManager)}] {_sessionDataCache.Count} tab(s) restored from last session.");

            return _sessionDataCache.Count;
        }

        // A save writes the backup files first and the session file last. If the session file could not be
        // written and the app then ended, backup files newer than the session file hold text the session
        // does not point to, and the first save of this run would delete them as orphans. Point the session
        // at them: a tab that is in the session gets the newer text, and any other tab comes back on its own.
        private async Task AddBackupsNewerThanSessionAsync(NotepadsSessionDataV1 sessionData)
        {
            DateTimeOffset? sessionSavedAt = await SessionUtility.GetSessionMetaDataFileDateModifiedAsync(_sessionMetaDataFileName);
            if (sessionSavedAt == null) return;

            HashSet<string> knownPaths = sessionData.TextEditors
                .SelectMany(editor => new[] { editor.LastSavedBackupFilePath, editor.PendingBackupFilePath })
                .Where(path => path != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Files older than the session file are left over from before the last save, not lost text.
            // File times are coarse, so a file with the same time as the session file is kept: bringing back
            // a leftover only shows old text again, but skipping a real backup would lose it.
            var newerFiles = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            foreach (StorageFile file in await SessionUtility.GetAllFilesInBackupFolderAsync(_backupFolderName))
            {
                if (file.Name.Contains(".") || knownPaths.Contains(file.Path)) continue;

                try
                {
                    if ((await file.GetBasicPropertiesAsync()).DateModified < sessionSavedAt.Value) continue;
                    newerFiles[file.Name] = file;
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"[{nameof(SessionManager)}] Failed to read the time of backup file {file.Name}: {ex.Message}");
                }
            }

            foreach (StorageFile pendingFile in newerFiles.Values)
            {
                if (!pendingFile.Name.EndsWith(PendingBackupSuffix, StringComparison.OrdinalIgnoreCase)) continue;

                string token = pendingFile.Name.Substring(0, pendingFile.Name.Length - PendingBackupSuffix.Length);
                if (!Guid.TryParseExact(token, "N", out Guid id)) continue;

                try
                {
                    newerFiles.TryGetValue(token + LastSavedBackupSuffix, out StorageFile lastSavedFile);

                    TextEditorSessionDataV1 editorData = sessionData.TextEditors.FirstOrDefault(editor => editor.Id == id);

                    if (editorData != null) // The tab had no unsaved changes when the session file was written
                    {
                        editorData.PendingBackupFilePath = pendingFile.Path;
                        editorData.LastSavedBackupFilePath = editorData.LastSavedBackupFilePath ?? lastSavedFile?.Path;
                        editorData.StateMetaData.IsModified = true;
                    }
                    else // The tab was opened after the session file was written
                    {
                        sessionData.TextEditors.Add(await BuildRecoveredTextEditorSessionDataAsync(id, token, pendingFile, lastSavedFile));
                    }

                    LoggingService.LogInfo($"[{nameof(SessionManager)}] Restored a backup that is newer than the session file: {pendingFile.Name}");
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"[{nameof(SessionManager)}] Failed to restore backup file {pendingFile.Name}: {ex.Message}");
                }
            }
        }

        private static async Task<TextEditorSessionDataV1> BuildRecoveredTextEditorSessionDataAsync(Guid id, string token, StorageFile pendingFile, StorageFile lastSavedFile)
        {
            // A save adds a tab's file to the FutureAccessList under the tab's token
            string fileToken = StorageApplicationPermissions.FutureAccessList.ContainsItem(token) ? token : null;

            // An untitled tab starts with the default encoding and line ending. A file tab's backups are written
            // in the file's own, so detect them from -LastSaved, the way the app does when it opens a file.
            string encodingName = EncodingUtility.GetEncodingName(AppSettingsService.EditorDefaultEncoding);
            string lineEndingName = LineEndingUtility.GetLineEndingName(AppSettingsService.EditorDefaultLineEnding);

            if (fileToken != null && lastSavedFile != null)
            {
                try
                {
                    TextFile lastSavedText = await FileSystemUtility.ReadFileAsync(lastSavedFile, ignoreFileSizeLimit: true);
                    encodingName = EncodingUtility.GetEncodingName(lastSavedText.Encoding);

                    // With no line break in the text, the detected line ending is only a default
                    if (lastSavedText.Content.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                    {
                        lineEndingName = LineEndingUtility.GetLineEndingName(lastSavedText.LineEnding);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"[{nameof(SessionManager)}] Failed to detect the encoding of {lastSavedFile.Name}: {ex.Message}");
                }
            }

            return new TextEditorSessionDataV1
            {
                Id = id,
                EditingFileFutureAccessToken = fileToken,
                LastSavedBackupFilePath = lastSavedFile?.Path,
                PendingBackupFilePath = pendingFile.Path,
                StateMetaData = new TextEditorStateMetaData
                {
                    FileNamePlaceholder = ResourceLoader.GetForViewIndependentUse().GetString("TextEditor_DefaultNewFileName"),
                    LastSavedEncoding = encodingName,
                    LastSavedLineEnding = lineEndingName,
                    DateModifiedFileTime = -1, // Unknown, so the tab shows the file as changed outside the app
                    HasEditingFile = fileToken != null,
                    IsModified = true,
                    WrapWord = AppSettingsService.EditorDefaultTextWrapping == TextWrapping.Wrap,
                    FontZoomFactor = 1,
                },
            };
        }

        private static async Task KeepBackupFilesAsync(TextEditorSessionDataV1 textEditorData)
        {
            foreach (string path in new[] { textEditorData.LastSavedBackupFilePath, textEditorData.PendingBackupFilePath })
            {
                if (path == null) continue;

                try
                {
                    StorageFile backupFile = await FileSystemUtility.GetFileAsync(path);
                    if (backupFile == null) continue; // A missing backup file is a common reason the restore failed

                    await backupFile.RenameAsync(BackupFileUtility.GetKeptFileName(backupFile.Name), NameCollisionOption.GenerateUniqueName);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"[{nameof(SessionManager)}] Failed to keep backup file {path}: {ex.Message}");
                }
            }
        }

        public async Task<int> RecoverBackupFilesAsync()
        {
            int numberOfRecoveredFiles = 0;

            // Rename all backup files to indicate they are corrupted with an extension of ".txt" to avoid being deleted
            foreach (StorageFile backupFile in await SessionUtility.GetAllFilesInBackupFolderAsync(_backupFolderName))
            {
                if (backupFile.Name.Contains(".")) continue; // Skip files with extension

                try
                {
                    await backupFile.RenameAsync(BackupFileUtility.GetKeptFileName(backupFile.Name));
                    numberOfRecoveredFiles++;
                }
                catch (Exception)
                {
                    // Best effort
                }
            }

            return numberOfRecoveredFiles;
        }

        public async Task OpenSessionBackupFolderAsync()
        {
            await Launcher.LaunchFolderAsync(await SessionUtility.GetBackupFolderAsync(_backupFolderName));
        }

        public async Task<bool> SaveSessionAsync(Action actionAfterSaving = null)
        {
            if (!IsBackupEnabled)
            {
                LoggingService.LogInfo($"[{nameof(SessionManager)}] Session backup is disabled.");
                return false;
            }

            // Serialize saves
            await _semaphoreSlim.WaitAsync();

            try
            {
                if (!IsBackupEnabled) return false; // Check again now that the semaphore is held

                Stopwatch stopwatch = Stopwatch.StartNew();

                ITextEditor[] textEditors = _notepadsCore.GetAllTextEditors();

                if (textEditors == null || textEditors.Length == 0)
                {
                    await ClearSessionDataAsync();
                    actionAfterSaving?.Invoke();
                    return true;
                }

                ITextEditor selectedTextEditor = _notepadsCore.GetSelectedTextEditor();

                NotepadsSessionDataV1 sessionData = new NotepadsSessionDataV1();
                bool everyTabBuilt = true;

                foreach (ITextEditor textEditor in textEditors)
                {
                    TextEditorSessionDataV1 textEditorSessionData = null;

                    try
                    {
                        textEditorSessionData = await GetTextEditorSessionDataAsync(textEditor);
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError($"[{nameof(SessionManager)}] Failed to build TextEditor session data: {ex}");
                    }

                    // A session without this tab would drop it, and its backup files with it. Keep the last
                    // complete session file, but still back up the other tabs: newer backups come back on launch.
                    if (textEditorSessionData == null)
                    {
                        everyTabBuilt = false;
                        continue;
                    }

                    sessionData.TextEditors.Add(textEditorSessionData);

                    if (textEditor == selectedTextEditor)
                    {
                        sessionData.SelectedTextEditor = textEditor.Id;
                    }
                }

                if (!everyTabBuilt) return false;

                sessionData.TabScrollViewerHorizontalOffset =
                    _notepadsCore.GetTabScrollViewerHorizontalOffset();

                bool sessionDataSaved = false;

                try
                {
                    string sessionJsonStr = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions { WriteIndented = true });

                    if (_lastSessionJsonStr == null || !string.Equals(_lastSessionJsonStr, sessionJsonStr, StringComparison.OrdinalIgnoreCase))
                    {
                        // write
                        await SessionUtility.SaveSerializedSessionMetaDataAsync(sessionJsonStr, _sessionMetaDataFileName);
                        _lastSessionJsonStr = sessionJsonStr;
                        sessionDataSaved = true;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"[{nameof(SessionManager)}] Failed to save session metadata: {ex.Message}");
                    return false; // Failed to save the session - do not proceed to delete backup files
                }

                if (sessionDataSaved)
                {
                    try
                    {
                        await DeleteOrphanedBackupFilesAsync(sessionData);
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException(ex);
                    }

                    try
                    {
                        DeleteOrphanedTokensInFutureAccessList(sessionData);
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException(ex);
                    }
                }

                stopwatch.Stop();

                if (sessionDataSaved)
                {
                    LoggingService.LogInfo($"[{nameof(SessionManager)}] Successfully saved the current session. Total time: {stopwatch.Elapsed.TotalMilliseconds} milliseconds.", consoleOnly: true);
                }

                actionAfterSaving?.Invoke();
                return true;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private async Task<TextEditorSessionDataV1> GetTextEditorSessionDataAsync(ITextEditor textEditor)
        {
            // A tab out of view gets no text events here (they are unbound on Unloaded), but its file can be deleted
            // outside, which makes it modified. An entry built while it was clean has no backup files, so build it again.
            if (_sessionDataCache.TryGetValue(textEditor.Id, out TextEditorSessionDataV1 textEditorSessionData) &&
                textEditorSessionData.StateMetaData?.IsModified == textEditor.IsModified)
            {
                // We will not create new backup files for this text editor unless it has content changes
                // But we should update latest TextEditor state meta data
                textEditorSessionData.StateMetaData = textEditor.GetTextEditorStateMetaData();
            }
            else // Text content has been changed or editor has not backed up yet
            {
                textEditorSessionData = await BuildTextEditorSessionDataAsync(textEditor);

                if (textEditorSessionData == null)
                {
                    return null;
                }

                _sessionDataCache[textEditor.Id] = textEditorSessionData;
            }

            return textEditorSessionData;
        }

        private async Task<TextEditorSessionDataV1> BuildTextEditorSessionDataAsync(ITextEditor textEditor)
        {
            TextEditorSessionDataV1 textEditorData = new TextEditorSessionDataV1
            {
                Id = textEditor.Id,
            };

            if (textEditor.EditingFile != null)
            {
                // Add the opened file to FutureAccessList so we can access it next launch
                var futureAccessToken = ToToken(textEditor.Id);
                await FutureAccessListUtility.TryAddOrReplaceTokenInFutureAccessListAsync(futureAccessToken, textEditor.EditingFile);
                textEditorData.EditingFileFutureAccessToken = futureAccessToken;
                textEditorData.EditingFileName = textEditor.EditingFileName;
                textEditorData.EditingFilePath = textEditor.EditingFilePath;
            }

            if (textEditor.IsModified)
            {
                if (textEditor.EditingFile != null)
                {
                    // Persist the last save known to the app, which might not be up-to-date (if the file was modified outside the app)
                    // OpenIfExists keeps the old backup until the write swaps the new one in. With ReplaceExisting
                    // it was emptied first, so a failed write left the session pointing at an empty file.
                    var lastSavedBackupFile = await SessionUtility.CreateNewFileInBackupFolderAsync(
                        ToToken(textEditor.Id) + LastSavedBackupSuffix,
                        CreationCollisionOption.OpenIfExists,
                        _backupFolderName);

                    if (!await BackupTextAsync(textEditor.LastSavedSnapshot.Content,
                        textEditor.LastSavedSnapshot.Encoding,
                        textEditor.LastSavedSnapshot.LineEnding,
                        lastSavedBackupFile))
                    {
                        return null; // Error: Failed to write backup text to file
                    }

                    textEditorData.LastSavedBackupFilePath = lastSavedBackupFile.Path;
                }

                if (textEditor.EditingFile == null ||
                    !string.Equals(textEditor.LastSavedSnapshot.Content, textEditor.GetText()))
                {
                    // Persist pending changes relative to the last save
                    var pendingBackupFile = await SessionUtility.CreateNewFileInBackupFolderAsync(
                        ToToken(textEditor.Id) + PendingBackupSuffix,
                        CreationCollisionOption.OpenIfExists,
                        _backupFolderName);

                    if (!await BackupTextAsync(textEditor.GetText(),
                        textEditor.LastSavedSnapshot.Encoding,
                        textEditor.LastSavedSnapshot.LineEnding,
                        pendingBackupFile))
                    {
                        return null; // Error: Failed to write backup text to file
                    }

                    textEditorData.PendingBackupFilePath = pendingBackupFile.Path;
                }
            }

            textEditorData.StateMetaData = textEditor.GetTextEditorStateMetaData();

            return textEditorData;
        }

        public void StartSessionBackup(bool startImmediately = false)
        {
            if (_timer == null)
            {
                LoggingService.LogInfo($"[{nameof(SessionManager)}] Session backup process started (StartImmediately = {startImmediately}).");

                Timer timer = new Timer(async (obj) => await SaveSessionAsync());

                if (Interlocked.CompareExchange(ref _timer, timer, null) == null)
                {
                    var delay = startImmediately ? TimeSpan.Zero : SaveInterval;
                    timer.Change(delay, SaveInterval);
                }
                else
                {
                    timer.Dispose();
                }
            }
        }

        public void StopSessionBackup()
        {
            Timer timer = _timer;

            try
            {
                timer?.Dispose();
            }
            catch
            {
                // Best effort
            }
            finally
            {
                Interlocked.CompareExchange(ref _timer, null, timer);
            }
        }

        public async Task ClearSessionDataAsync()
        {
            _lastSessionJsonStr = null;
            try
            {
                await SessionUtility.DeleteSerializedSessionMetaDataAsync(_sessionMetaDataFileName);
                LoggingService.LogInfo($"[{nameof(SessionManager)}] Successfully deleted session meta data.");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(SessionManager)}] Failed to delete session meta data: {ex.Message}");
            }
        }

        private void BindEditorContentStateChangeEvent(object sender, ITextEditor textEditor)
        {
            // All text or file related events
            textEditor.TextChanging += RemoveTextEditorSessionData;
            textEditor.ChangeReverted += RemoveTextEditorSessionData;
            textEditor.FileSaved += RemoveTextEditorSessionData;
            textEditor.FileReloaded += RemoveTextEditorSessionData;
        }

        private void UnbindEditorContentStateChangeEvent(object sender, ITextEditor textEditor)
        {
            // All text or file related events
            textEditor.TextChanging -= RemoveTextEditorSessionData;
            textEditor.ChangeReverted -= RemoveTextEditorSessionData;
            textEditor.FileSaved -= RemoveTextEditorSessionData;
            textEditor.FileReloaded -= RemoveTextEditorSessionData;
        }

        private async Task<ITextEditor> RecoverTextEditorAsync(TextEditorSessionDataV1 editorSessionData)
        {
            StorageFile editingFile = null;

            if (editorSessionData.EditingFileFutureAccessToken != null)
            {
                editingFile = await FutureAccessListUtility.GetFileFromFutureAccessListAsync(editorSessionData.EditingFileFutureAccessToken);
            }

            string lastSavedFile = editorSessionData.LastSavedBackupFilePath;
            string pendingFile = editorSessionData.PendingBackupFilePath;

            ITextEditor textEditor;

            if (editingFile == null && lastSavedFile == null && pendingFile == null)
            {
                textEditor = null;
            }
            else if (editingFile != null && lastSavedFile == null && pendingFile == null) // File without pending changes
            {
                var encoding = EncodingUtility.GetEncodingByName(editorSessionData.StateMetaData.LastSavedEncoding);
                textEditor = await _notepadsCore.CreateTextEditorAsync(editorSessionData.Id, editingFile, encoding: encoding, ignoreFileSizeLimit: true);
                textEditor.ResetEditorState(editorSessionData.StateMetaData);
            }
            else // File with pending changes
            {
                string lastSavedText = string.Empty;
                string pendingText = null;

                if (lastSavedFile != null)
                {
                    TextFile lastSavedTextFile = await FileSystemUtility.ReadFileAsync(lastSavedFile, ignoreFileSizeLimit: true,
                    EncodingUtility.GetEncodingByName(editorSessionData.StateMetaData.LastSavedEncoding));
                    lastSavedText = lastSavedTextFile.Content;
                }

                var textFile = new TextFile(lastSavedText,
                    EncodingUtility.GetEncodingByName(editorSessionData.StateMetaData.LastSavedEncoding),
                    LineEndingUtility.GetLineEndingByName(editorSessionData.StateMetaData.LastSavedLineEnding),
                    editorSessionData.StateMetaData.DateModifiedFileTime);

                textEditor = _notepadsCore.CreateTextEditor(
                    editorSessionData.Id,
                    textFile,
                    editingFile,
                    editorSessionData.StateMetaData.FileNamePlaceholder,
                    editorSessionData.StateMetaData.IsModified);

                if (pendingFile != null)
                {
                    TextFile pendingTextFile = await FileSystemUtility.ReadFileAsync(pendingFile,
                        ignoreFileSizeLimit: true,
                        EncodingUtility.GetEncodingByName(editorSessionData.StateMetaData.LastSavedEncoding));
                    pendingText = pendingTextFile.Content;
                }

                textEditor.ResetEditorState(editorSessionData.StateMetaData, pendingText);
            }

            return textEditor;
        }

        private static async Task<bool> BackupTextAsync(string text, Encoding encoding, LineEnding lineEnding, StorageFile file)
        {
            try
            {
                // In the tab's encoding when it holds the text, else UTF-8 with a byte order mark. The restore
                // reads with the tab's encoding but follows the mark, so no character turns into "?".
                encoding = EncodingFitUtility.GetBackupEncoding(text, encoding);
                await FileSystemUtility.WriteTextToFileAsync(file, LineEndingUtility.ApplyLineEnding(text, lineEnding), encoding);
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(SessionManager)}] Failed to save backup file: {ex.Message}");
                return false;
            }
        }

        // Cleanup orphaned/dangling backup files
        private async Task DeleteOrphanedBackupFilesAsync(NotepadsSessionDataV1 sessionData)
        {
            HashSet<string> backupPaths = sessionData.TextEditors
                .SelectMany(editor => new[] { editor.LastSavedBackupFilePath, editor.PendingBackupFilePath })
                .Where(path => path != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (StorageFile backupFile in await SessionUtility.GetAllFilesInBackupFolderAsync(_backupFolderName))
            {
                if (!BackupFileUtility.ShouldDeleteFromBackupFolder(backupFile.Name, backupPaths.Contains(backupFile.Path))) continue;

                try
                {
                    await backupFile.DeleteAsync();
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"[{nameof(SessionManager)}] Failed to delete orphaned backup file: {ex.Message}");
                }
            }
        }

        // Cleanup orphaned/dangling entries in FutureAccessList
        private void DeleteOrphanedTokensInFutureAccessList(NotepadsSessionDataV1 sessionData)
        {
            HashSet<string> tokens = sessionData.TextEditors
                .SelectMany(editor => new[] { editor.EditingFileFutureAccessToken })
                .Where(token => token != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var tokensToBeDeleted = new List<string>();

            foreach (var entry in StorageApplicationPermissions.FutureAccessList.Entries)
            {
                // Editor tokens are Guids (see ToToken). Leave other tokens, like the workspace folder, alone.
                if (!tokens.Contains(entry.Token) && Guid.TryParseExact(entry.Token, "N", out _))
                {
                    tokensToBeDeleted.Add(entry.Token);
                }
            }

            foreach (var tokenToBeDel in tokensToBeDeleted)
            {
                try
                {
                    StorageApplicationPermissions.FutureAccessList.Remove(tokenToBeDel);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"[{nameof(SessionManager)}] Failed to delete orphaned token in FutureAccessList: {ex.Message}");
                }
            }
        }

        private const string LastSavedBackupSuffix = "-LastSaved";
        private const string PendingBackupSuffix = "-Pending";

        private static string ToToken(Guid textEditorId)
        {
            return textEditorId.ToString("N");
        }

        private void RemoveTextEditorSessionData(object sender, EventArgs e)
        {
            if (sender is ITextEditor textEditor)
            {
                _sessionDataCache.TryRemove(textEditor.Id, out _);
            }
        }

        public void Dispose()
        {
            if (_notepadsCore != null)
            {
                _notepadsCore.TextEditorLoaded -= BindEditorContentStateChangeEvent;
                _notepadsCore.TextEditorUnloaded -= UnbindEditorContentStateChangeEvent;
            }

            _semaphoreSlim?.Dispose();
            _timer?.Dispose();
        }
    }
}