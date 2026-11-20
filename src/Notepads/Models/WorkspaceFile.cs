// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Models
{
    using Windows.Storage;

    /// <summary>A file inside a workspace folder, with its path relative to that folder.</summary>
    public sealed class WorkspaceFile
    {
        public WorkspaceFile(string relativePath, StorageFile file)
        {
            RelativePath = relativePath;
            File = file;
        }

        public string RelativePath { get; }

        public StorageFile File { get; }

        public override string ToString() => RelativePath;
    }
}
