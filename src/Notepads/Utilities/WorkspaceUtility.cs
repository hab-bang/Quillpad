// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Windows.Storage;

    /// <summary>
    /// Reads and writes the workspace state file (.quillpad/workspace.json) inside a workspace folder.
    /// </summary>
    internal static class WorkspaceUtility
    {
        private const string StateFolderName = ".quillpad";
        private const string StateFileName = "workspace.json";

        /// <summary>Returns the state file content, or null when the folder has no state file yet.</summary>
        public static async Task<string> ReadStateAsync(StorageFolder workspaceFolder)
        {
            try
            {
                var stateFolder = await workspaceFolder.GetFolderAsync(StateFolderName);
                var stateFile = await stateFolder.GetFileAsync(StateFileName);
                return await FileIO.ReadTextAsync(stateFile);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        public static async Task WriteStateAsync(StorageFolder workspaceFolder, string state)
        {
            var stateFolder = await workspaceFolder.CreateFolderAsync(StateFolderName, CreationCollisionOption.OpenIfExists);
            var stateFile = await FileSystemUtility.GetOrCreateFileAsync(stateFolder, StateFileName);
            await FileSystemUtility.WriteTextToFileAsync(stateFile, state);
        }

        /// <summary>
        /// Returns <paramref name="filePath"/> relative to the folder, with "/" separators,
        /// or null when the file is not inside the folder.
        /// </summary>
        public static string GetRelativePath(StorageFolder workspaceFolder, string filePath)
        {
            if (string.IsNullOrEmpty(workspaceFolder.Path) || string.IsNullOrEmpty(filePath)) return null;

            var folderPath = workspaceFolder.Path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!filePath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase)) return null;

            return filePath.Substring(folderPath.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        /// <summary>Returns the file at <paramref name="relativePath"/>, or null when it is gone.</summary>
        public static async Task<StorageFile> TryGetFileAsync(StorageFolder workspaceFolder, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            try
            {
                return await workspaceFolder.GetFileAsync(relativePath.Replace('/', Path.DirectorySeparatorChar));
            }
            catch (Exception)
            {
                return null; // Renamed, moved or deleted outside the app
            }
        }
    }
}
