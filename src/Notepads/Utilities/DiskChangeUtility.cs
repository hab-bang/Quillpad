// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
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
    }
}
