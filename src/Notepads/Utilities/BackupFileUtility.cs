// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;

    /// <summary>
    /// File names in the session backup folder, and which of them the cleanup may delete.
    /// </summary>
    public static class BackupFileUtility
    {
        /// <summary>
        /// A save writes "name.quillpad-save.tmp" next to the file, then swaps it in. Backups are saved the same way.
        /// </summary>
        public const string SaveTempFileSuffix = ".quillpad-save.tmp";

        /// <summary>
        /// Added to a backup file that must stay: the cleanup deletes only files with no extension.
        /// </summary>
        public const string KeptFileSuffix = "-Corrupted.txt";

        public static bool ShouldDeleteFromBackupFolder(string fileName, bool usedBySession)
        {
            if (usedBySession || string.IsNullOrEmpty(fileName)) return false;

            // A backup write that stopped halfway leaves its temp file
            if (fileName.EndsWith(SaveTempFileSuffix, StringComparison.OrdinalIgnoreCase)) return true;

            // Backup files have no extension. A file with one (a kept backup) is the user's to delete.
            return !fileName.Contains(".");
        }

        public static string GetKeptFileName(string backupFileName)
        {
            return backupFileName + KeptFileSuffix;
        }

        /// <summary>
        /// The name the tab showed: its file's name, else its placeholder, else the given default.
        /// </summary>
        public static string GetTabName(string editingFileName, string fileNamePlaceholder, string defaultName)
        {
            if (!string.IsNullOrEmpty(editingFileName)) return editingFileName;
            if (!string.IsNullOrEmpty(fileNamePlaceholder)) return fileNamePlaceholder;
            return defaultName;
        }
    }
}
