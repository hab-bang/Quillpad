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

    public enum SnapshotKind
    {
        Manual,
        BeforeReplaceAll,
        BeforeReload,
        BeforeRestore,
    }

    public sealed class SnapshotInfo
    {
        public SnapshotInfo(string fileName, DateTime timeUtc, SnapshotKind kind)
        {
            FileName = fileName;
            TimeUtc = timeUtc;
            Kind = kind;
        }

        public string FileName { get; }

        public DateTime TimeUtc { get; }

        public SnapshotKind Kind { get; }
    }

    /// <summary>
    /// Names and retention of file snapshots. Each file has a folder of snapshots, one text file each,
    /// named by the time it was taken and why, like "20261123-164512-123-Manual.txt".
    /// </summary>
    public static class SnapshotUtility
    {
        public const int MaxSnapshotsPerFile = 20;

        private const string TimeFormat = "yyyyMMdd-HHmmss-fff";
        private const string Extension = ".txt";

        /// <summary>
        /// The folder for a file's snapshots. A saved file is known by its path, an untitled tab by its id.
        /// </summary>
        public static string GetFolderName(string filePath, Guid editorId)
        {
            return string.IsNullOrEmpty(filePath)
                ? "Untitled-" + editorId.ToString("N")
                : "File-" + WordGoalUtility.GetStoreKey(filePath).Substring("Goal_".Length);
        }

        public static string GetFileName(DateTime timeUtc, SnapshotKind kind)
        {
            return timeUtc.ToString(TimeFormat, CultureInfo.InvariantCulture) + "-" + kind + Extension;
        }

        public static SnapshotInfo Parse(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) return null;
            if (fileName.Length < TimeFormat.Length + 1 + Extension.Length) return null;

            var time = fileName.Substring(0, TimeFormat.Length);
            var kind = fileName.Substring(TimeFormat.Length + 1, fileName.Length - TimeFormat.Length - 1 - Extension.Length);

            // Windows adds " (2)" to a name taken twice in one millisecond
            var space = kind.IndexOf(' ');
            if (space >= 0) kind = kind.Substring(0, space);

            if (fileName[TimeFormat.Length] != '-' ||
                !DateTime.TryParseExact(time, TimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var timeUtc) ||
                !Enum.TryParse(kind, out SnapshotKind snapshotKind) ||
                !Enum.IsDefined(typeof(SnapshotKind), snapshotKind))
            {
                return null;
            }

            return new SnapshotInfo(fileName, timeUtc, snapshotKind);
        }

        /// <summary>The snapshots among the file names, newest first. Other files are left out.</summary>
        public static List<SnapshotInfo> GetSnapshots(IEnumerable<string> fileNames)
        {
            return (fileNames ?? Enumerable.Empty<string>())
                .Select(Parse)
                .Where(snapshot => snapshot != null)
                .OrderByDescending(snapshot => snapshot.TimeUtc)
                .ThenByDescending(snapshot => snapshot.FileName, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>The snapshots to delete so that only the newest <paramref name="keep"/> stay.</summary>
        public static List<string> GetFilesToDelete(IEnumerable<string> fileNames, int keep)
        {
            return GetSnapshots(fileNames).Skip(Math.Max(0, keep)).Select(snapshot => snapshot.FileName).ToList();
        }
    }
}
