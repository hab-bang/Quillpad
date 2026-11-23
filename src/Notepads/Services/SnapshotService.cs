// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Notepads.Utilities;
    using Windows.Storage;

    /// <summary>
    /// Copies of a file's text, kept in the app's local folder (Snapshots\&lt;file&gt;\&lt;time&gt;-&lt;kind&gt;.txt),
    /// the newest <see cref="SnapshotUtility.MaxSnapshotsPerFile"/> per file. The text is kept as the editor holds it.
    /// </summary>
    public static class SnapshotService
    {
        private const string RootFolderName = "Snapshots";

        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Keeps a copy of <paramref name="text"/>. Returns false when it is empty, the same as the newest
        /// snapshot, or could not be written.
        /// </summary>
        public static async Task<bool> TakeSnapshotAsync(string filePath, Guid editorId, string text, SnapshotKind kind)
        {
            if (string.IsNullOrEmpty(text)) return false;

            await Semaphore.WaitAsync();

            try
            {
                var folder = await GetFolderAsync(filePath, editorId, create: true);
                var files = await folder.GetFilesAsync();
                var snapshots = SnapshotUtility.GetSnapshots(files.Select(file => file.Name));

                if (snapshots.Count > 0)
                {
                    var newest = files.First(file => file.Name == snapshots[0].FileName);
                    if (string.Equals(await FileIO.ReadTextAsync(newest), text, StringComparison.Ordinal)) return false;
                }

                var snapshotFile = await folder.CreateFileAsync(SnapshotUtility.GetFileName(DateTime.UtcNow, kind), CreationCollisionOption.GenerateUniqueName);
                await FileIO.WriteTextAsync(snapshotFile, text, Windows.Storage.Streams.UnicodeEncoding.Utf8);

                var names = files.Select(file => file.Name).Concat(new[] { snapshotFile.Name });
                foreach (var name in SnapshotUtility.GetFilesToDelete(names, SnapshotUtility.MaxSnapshotsPerFile))
                {
                    var old = files.FirstOrDefault(file => file.Name == name);
                    if (old != null) await old.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(SnapshotService)}] Failed to take a snapshot: {ex.Message}");
                return false;
            }
            finally
            {
                Semaphore.Release();
            }
        }

        /// <summary>The snapshots of a file, newest first.</summary>
        public static async Task<IReadOnlyList<SnapshotInfo>> GetSnapshotsAsync(string filePath, Guid editorId)
        {
            try
            {
                var folder = await GetFolderAsync(filePath, editorId, create: false);
                if (folder == null) return Array.Empty<SnapshotInfo>();

                var files = await folder.GetFilesAsync();
                return SnapshotUtility.GetSnapshots(files.Select(file => file.Name));
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(SnapshotService)}] Failed to list snapshots: {ex.Message}");
                return Array.Empty<SnapshotInfo>();
            }
        }

        /// <summary>The text of a snapshot, or null when it cannot be read.</summary>
        public static async Task<string> ReadSnapshotAsync(string filePath, Guid editorId, SnapshotInfo snapshot)
        {
            try
            {
                var folder = await GetFolderAsync(filePath, editorId, create: false);
                if (folder == null) return null;

                var file = await folder.GetFileAsync(snapshot.FileName);
                return await FileIO.ReadTextAsync(file);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(SnapshotService)}] Failed to read a snapshot: {ex.Message}");
                return null;
            }
        }

        private static async Task<StorageFolder> GetFolderAsync(string filePath, Guid editorId, bool create)
        {
            var name = SnapshotUtility.GetFolderName(filePath, editorId);

            if (create)
            {
                var root = await ApplicationData.Current.LocalFolder.CreateFolderAsync(RootFolderName, CreationCollisionOption.OpenIfExists);
                return await root.CreateFolderAsync(name, CreationCollisionOption.OpenIfExists);
            }

            var existingRoot = await ApplicationData.Current.LocalFolder.TryGetItemAsync(RootFolderName) as StorageFolder;
            return existingRoot == null ? null : await existingRoot.TryGetItemAsync(name) as StorageFolder;
        }
    }
}
