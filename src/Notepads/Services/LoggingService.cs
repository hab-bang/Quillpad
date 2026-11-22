// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Notepads.Utilities;
    using Windows.ApplicationModel.Core;
    using Windows.Storage;

    public static class LoggingService
    {
        private const string MessageFormatString = "{0} [{1}] {2}"; // {timestamp} [{level}] {message}

        private static readonly ConcurrentQueue<string> MessageQueue = new ConcurrentQueue<string>();
        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);
        private static readonly TimeSpan LoggingInterval = TimeSpan.FromSeconds(10);
        private static readonly List<string> Messages = new List<string>();
        private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(2);

#if DEBUG
        private const bool WriteInfoToFile = true;
#else
        private const bool WriteInfoToFile = false; // Release builds keep only warnings and errors
#endif

        private static StorageFile _logFile;
        private static Task _backgroundTask;
        private static bool _initialized;

        public static async Task InitializeFileSystemLoggingAsync()
        {
            if (_initialized)
            {
                return;
            }

            CoreApplication.Suspending += async (sender, args) => { await TryFlushMessageQueueAsync(); };
            CoreApplication.Resuming += async (sender, args) => { await InitializeLogFileWriterBackgroundTaskAsync(); };

            await InitializeLogFileWriterBackgroundTaskAsync();
        }

        /// <summary>
        /// Writes the queued messages now, waiting at most a short time. For crash handlers: the
        /// background writer runs every few seconds, and the app may not live that long.
        /// </summary>
        public static void Flush()
        {
            try
            {
                // Task.Run keeps the write off the caller's context, so waiting on the UI thread cannot deadlock
                Task.Run(TryFlushMessageQueueAsync).Wait(FlushTimeout);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public static StorageFile GetLogFile()
        {
            return _logFile;
        }

        public static void LogInfo(string message, bool consoleOnly = false)
        {
            LogMessage("Info", message, consoleOnly);
        }

        public static void LogWarning(string message, bool consoleOnly = false)
        {
            LogMessage("Warning", message, consoleOnly);
        }

        public static void LogError(string message, bool consoleOnly = false)
        {
            LogMessage("Error", message, consoleOnly);
        }

        public static void LogException(Exception ex, bool consoleOnly = false)
        {
            if (ex == null)
            {
                return;
            }

            LogError(ex.ToString(), consoleOnly);
        }

        private static void LogMessage(string level, string message, bool consoleOnly)
        {
            string timeStamp = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);
            string formattedMessage = string.Format(MessageFormatString, timeStamp, level, message);

            // Print to console
            Debug.WriteLine(formattedMessage);

            if (!_initialized)
            {
                return;
            }

            if (!consoleOnly && LogFileUtility.ShouldWriteToFile(level, WriteInfoToFile))
            {
                // Add to message queue
                MessageQueue.Enqueue(formattedMessage);
            }
        }

        private static async Task<bool> InitializeLogFileWriterBackgroundTaskAsync()
        {
            await SemaphoreSlim.WaitAsync();

            if (_backgroundTask != null && !_backgroundTask.IsCompleted)
            {
                SemaphoreSlim.Release();
                return false;
            }

            try
            {
                if (_logFile == null)
                {
                    StorageFolder logsFolder = await FileSystemUtility.GetOrCreateAppFolderAsync("Logs");
                    _logFile = await FileSystemUtility.CreateFileAsync(logsFolder,
                        DateTime.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture) + ".log");
                    await DeleteOldLogFilesAsync(logsFolder);
                }

                _backgroundTask = Task.Run(WriteLogMessagesAsync);

                _initialized = true;
                LogInfo($"Log file location: {_logFile.Path}", true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                SemaphoreSlim.Release();
            }

            return false;
        }

        private static async Task DeleteOldLogFilesAsync(StorageFolder logsFolder)
        {
            try
            {
                var files = (await logsFolder.GetFilesAsync()).ToDictionary(file => file.Name, StringComparer.OrdinalIgnoreCase);

                foreach (string name in LogFileUtility.GetFileNamesToDelete(files.Keys, LogFileUtility.FilesToKeep))
                {
                    try
                    {
                        await files[name].DeleteAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex); // Another window may still write to it
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static async Task WriteLogMessagesAsync()
        {
            while (true)
            {
                Thread.Sleep(LoggingInterval);

                // We will try to write all pending messages in our next attempt, if the current attempt failed
                // However, if the size of messages has become abnormally big, we know something is wrong and should abort at this point
                if (!await TryFlushMessageQueueAsync() && Messages.Count > 1000)
                {
                    break;
                }
            }
        }

        private static async Task<bool> TryFlushMessageQueueAsync()
        {
            if (!_initialized)
            {
                return false;
            }

            await SemaphoreSlim.WaitAsync();

            try
            {
                if (MessageQueue.Count == 0)
                {
                    return true;
                }

                while (MessageQueue.TryDequeue(out string message))
                {
                    Messages.Add(message);
                }

                await FileIO.AppendLinesAsync(_logFile, Messages);
                Messages.Clear();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                SemaphoreSlim.Release();
            }

            return false;
        }
    }
}