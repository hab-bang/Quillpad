// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Core.WorkspaceDataModels
{
    using System.Collections.Generic;

    /// <summary>
    /// The state Quillpad keeps in a workspace folder (.quillpad/workspace.json).
    /// Paths are relative to the folder and use "/" so the file can be synced between machines.
    /// </summary>
    internal sealed class WorkspaceDataV1
    {
        public int Version { get; set; } = 1;

        public string SelectedFile { get; set; }

        public List<WorkspaceFileDataV1> Files { get; set; } = new List<WorkspaceFileDataV1>();

        /// <summary>Word count goals by relative path, also for files that are not open.</summary>
        public Dictionary<string, int> WordGoals { get; set; } = new Dictionary<string, int>();
    }
}
