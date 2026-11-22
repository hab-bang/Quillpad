// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    public static class LogFileUtility
    {
        public const int FilesToKeep = 10;

        /// <summary>
        /// A log file stops taking lines at this size, so an error that repeats on every keystroke
        /// cannot fill the disk.
        /// </summary>
        public const long MaxFileBytes = 1024 * 1024;

        // A log file is named after the UTC time of its first line and the window's process id, for example
        // 20261122T061500_4242.log. Files from before the process id was added have no "_4242" part.
        private static readonly Regex LogFileNamePattern = new Regex(@"^\d{8}T\d{6}(_\d+)?\.log$", RegexOptions.CultureInvariant);

        public static string GetFileName(DateTime utcTime, uint processId)
        {
            return utcTime.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture) + "_" + processId.ToString(CultureInfo.InvariantCulture) + ".log";
        }

        /// <summary>
        /// Release builds write only warnings and errors to the log file, so the file stays small.
        /// </summary>
        public static bool ShouldWriteToFile(string level, bool includeInfo)
        {
            return includeInfo || !string.Equals(level, "Info", StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns the log files to delete so that only the newest <paramref name="filesToKeep"/> stay.
        /// Names start with the time, so name order is time order. Files with other names are not touched.
        /// </summary>
        public static IReadOnlyList<string> GetFileNamesToDelete(IEnumerable<string> fileNames, int filesToKeep)
        {
            return fileNames
                .Where(name => name != null && LogFileNamePattern.IsMatch(name))
                .OrderByDescending(name => name, StringComparer.Ordinal)
                .Skip(Math.Max(filesToKeep, 0))
                .ToList();
        }

        /// <summary>
        /// How many of <paramref name="lines"/>, from the first, still fit in a file that holds
        /// <paramref name="bytesInFile"/> bytes, without going over <paramref name="maxBytes"/>.
        /// </summary>
        public static int CountLinesThatFit(IReadOnlyList<string> lines, long bytesInFile, long maxBytes)
        {
            int count = 0;
            long bytes = bytesInFile;
            foreach (string line in lines)
            {
                bytes += GetByteCount(line);
                if (bytes > maxBytes) break;
                count++;
            }
            return count;
        }

        /// <summary>
        /// The bytes a line takes in the file: UTF-8 text plus CR LF.
        /// </summary>
        public static long GetByteCount(string line)
        {
            return Encoding.UTF8.GetByteCount(line ?? string.Empty) + 2;
        }
    }
}
