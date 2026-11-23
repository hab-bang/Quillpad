// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Core
{
    using System;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Notepads.Utilities;

    /// <summary>
    /// A workspace is a folder the user opened. Its open tabs are kept in the folder itself,
    /// so opening the folder again brings the same files back.
    /// </summary>
    internal interface IWorkspaceManager
    {
        /// <summary>Raised after <see cref="Folder"/> changed, including to null.</summary>
        event EventHandler FolderChanged;

        /// <summary>The open workspace folder, or null when no folder is open.</summary>
        StorageFolder Folder { get; }

        /// <summary>
        /// Makes the folder from the last run the workspace again. Does not open any tab.
        /// Returns false when there is none, or when another window has it open.
        /// </summary>
        Task<bool> TryResumeLastFolderAsync();

        /// <summary>True when <paramref name="folder"/> is the open workspace folder.</summary>
        bool IsOpen(StorageFolder folder);

        /// <summary>
        /// Makes <paramref name="folder"/> the workspace and keeps <paramref name="folderLock"/> until the folder closes.
        /// The lock is null only when the folder is already open. Does not open or close any tab.
        /// </summary>
        void Open(StorageFolder folder, WorkspaceFolderLock folderLock);

        /// <summary>
        /// From now on, reopen the workspace folder on the next launch. Used when this window becomes the main window.
        /// </summary>
        void StartRememberingFolder();

        /// <summary>Leaves the workspace. Does not close any tab.</summary>
        void Close();

        /// <summary>Opens the tabs saved in the workspace folder and returns how many were opened.</summary>
        Task<int> RestoreTabsAsync();

        /// <summary>Saves the open tabs that belong to the workspace folder into that folder.</summary>
        Task SaveAsync();

        /// <summary>The word goal of a file in the workspace folder, or null when it has none or is outside the folder.</summary>
        Task<int?> GetWordGoalAsync(string filePath);

        /// <summary>
        /// Keeps the word goal of a file in the workspace folder, null to remove it, and saves the workspace.
        /// Returns false when the file is not in the folder.
        /// </summary>
        Task<bool> TrySetWordGoalAsync(string filePath, int? goal);
    }
}
