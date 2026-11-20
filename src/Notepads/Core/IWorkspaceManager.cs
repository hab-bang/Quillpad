// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Core
{
    using System;
    using System.Threading.Tasks;
    using Windows.Storage;

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

        /// <summary>Makes the folder from the last run the workspace again. Does not open any tab.</summary>
        Task<bool> TryResumeLastFolderAsync();

        /// <summary>Makes <paramref name="folder"/> the workspace. Does not open or close any tab.</summary>
        void Open(StorageFolder folder);

        /// <summary>Leaves the workspace. Does not close any tab.</summary>
        void Close();

        /// <summary>Opens the tabs saved in the workspace folder and returns how many were opened.</summary>
        Task<int> RestoreTabsAsync();

        /// <summary>Saves the open tabs that belong to the workspace folder into that folder.</summary>
        Task SaveAsync();
    }
}
