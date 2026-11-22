// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;

    public static class DiskChangeUtility
    {
        /// <summary>
        /// True when another app wrote the file after the tab last loaded or saved it.
        /// <paramref name="lastKnownFileTime"/> is the file time from that load or save (-1 when unknown,
        /// which counts as changed). <paramref name="currentFileTime"/> is null when it could not be read:
        /// then nothing is known, and the write itself reports the error.
        /// </summary>
        public static bool HasChangedOnDisk(long lastKnownFileTime, long? currentFileTime)
        {
            return currentFileTime.HasValue && currentFileTime.Value != lastKnownFileTime;
        }

        /// <summary>
        /// True when two looks at the file, a moment apart, found the same time and size. Sync tools and
        /// other apps can write a file in parts, and a reload in between would take the first part only.
        /// </summary>
        public static bool HasStoppedChanging(long fileTimeBefore, ulong sizeBefore, long fileTimeAfter, ulong sizeAfter)
        {
            return fileTimeBefore == fileTimeAfter && sizeBefore == sizeAfter;
        }

        /// <summary>
        /// True when the file on disk holds the same text as the editor, so a save over it loses nothing.
        /// Git and sync tools often write a file back with the same text and a new time. The editor keeps
        /// each line break as a single CR, so the file's line breaks are compared in that form.
        /// </summary>
        public static bool HasSameText(string fileOnDiskText, string editorText)
        {
            if (fileOnDiskText == null || editorText == null) return false;
            return string.Equals(LineEndingUtility.ApplyLineEnding(fileOnDiskText, LineEnding.Cr), editorText, StringComparison.Ordinal);
        }
    }
}
