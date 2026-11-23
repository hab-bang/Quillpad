// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Decides whether this is the first run of a new install, so the prose defaults apply to new users only.
    /// A setting that was never changed is not stored, and reads as its old default. Writing a new default
    /// over it would change the editor under an existing user, so new defaults are written once, on a new install.
    /// </summary>
    public static class NewInstallUtility
    {
        /// <param name="storedKeys">The keys in the settings store at startup.</param>
        /// <param name="markerKey">Written after the first run with this code, new install or not.</param>
        /// <param name="keysWrittenBeforeCheck">Keys the app writes at every start, before the check runs.</param>
        public static bool IsNewInstall(IEnumerable<string> storedKeys, string markerKey, IEnumerable<string> keysWrittenBeforeCheck)
        {
            var ignored = new HashSet<string>(keysWrittenBeforeCheck ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (var key in storedKeys ?? Enumerable.Empty<string>())
            {
                if (string.Equals(key, markerKey, StringComparison.Ordinal)) return false;
                if (!ignored.Contains(key)) return false; // Anything else means an earlier run
            }

            return true;
        }
    }
}
