// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;

    public static class SaveStateUtility
    {
        /// <summary>
        /// A save takes the text first and then waits for the write. Text typed during the write is
        /// not in the file, so the tab is still modified when the editor text differs from what was written.
        /// </summary>
        public static bool IsModifiedAfterSave(string writtenText, string currentText)
        {
            return !string.Equals(writtenText, currentText, StringComparison.Ordinal);
        }
    }
}
