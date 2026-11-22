// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Marks a folder as open in this window. Each Quillpad window is its own process, and two windows on one
    /// folder would write over each other's .quillpad\workspace.json. A named mutex lives as long as a handle
    /// to it is open, also across processes of the app, and Windows closes the handle when a window ends or crashes.
    /// </summary>
    public sealed class WorkspaceFolderLock : IDisposable
    {
        private const string NamePrefix = "Quillpad-Folder-";

        private Mutex _mutex;

        private WorkspaceFolderLock(string folderPath, Mutex mutex)
        {
            FolderPath = folderPath;
            _mutex = mutex;
        }

        public string FolderPath { get; }

        /// <summary>
        /// Returns the lock, or null when another window holds this folder. A folder without a path
        /// (some virtual folders) can't be compared, so it gets a lock that holds nothing.
        /// </summary>
        public static WorkspaceFolderLock TryAcquire(string folderPath)
        {
            string name = GetName(folderPath);
            if (name == null) return new WorkspaceFolderLock(folderPath, null);

            // Not owned: only the mutex's existence matters, so no thread has to release it
            var mutex = new Mutex(false, name, out bool createdNew);

            if (!createdNew)
            {
                mutex.Dispose();
                return null;
            }

            return new WorkspaceFolderLock(folderPath, mutex);
        }

        /// <summary>
        /// The mutex name for a folder path, or null when there is no path. The same folder gets the
        /// same name whatever its case or trailing separator. A hash, because "\" is not allowed in a name.
        /// </summary>
        public static string GetName(string folderPath)
        {
            string normalizedPath = NormalizePath(folderPath);
            if (normalizedPath == null) return null;

            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedPath));
                var name = new StringBuilder(NamePrefix, NamePrefix.Length + hash.Length * 2);
                foreach (byte b in hash) name.Append(b.ToString("x2"));
                return name.ToString();
            }
        }

        public static bool IsSameFolder(string folderPath, string otherFolderPath)
        {
            string path = NormalizePath(folderPath);
            return path != null && string.Equals(path, NormalizePath(otherFolderPath), StringComparison.Ordinal);
        }

        private static string NormalizePath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return null;
            return folderPath.Trim().Replace('/', '\\').TrimEnd('\\').ToUpperInvariant();
        }

        public void Dispose()
        {
            _mutex?.Dispose();
            _mutex = null;
        }
    }
}
