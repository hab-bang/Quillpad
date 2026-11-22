// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    public static class LogFileUtility
    {
        public const int FilesToKeep = 10;

        // A log file is named after the UTC time its window started, for example 20261122T061500.log
        private static readonly Regex LogFileNamePattern = new Regex(@"^\d{8}T\d{6}\.log$", RegexOptions.CultureInvariant);

        /// <summary>
        /// Release builds write only warnings and errors to the log file, so the file stays small.
        /// </summary>
        public static bool ShouldWriteToFile(string level, bool includeInfo)
        {
            return includeInfo || !string.Equals(level, "Info", StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns the log files to delete so that only the newest <paramref name="filesToKeep"/> stay.
        /// Names are times, so name order is time order. Files with other names are not touched.
        /// </summary>
        public static IReadOnlyList<string> GetFileNamesToDelete(IEnumerable<string> fileNames, int filesToKeep)
        {
            return fileNames
                .Where(name => name != null && LogFileNamePattern.IsMatch(name))
                .OrderByDescending(name => name, StringComparer.Ordinal)
                .Skip(Math.Max(filesToKeep, 0))
                .ToList();
        }
    }
}
