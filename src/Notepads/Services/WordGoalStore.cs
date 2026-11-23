// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Services
{
    using System;
    using Notepads.Utilities;
    using Windows.Storage;

    /// <summary>
    /// Word goals of files outside a workspace folder, kept in the app's local settings by file path.
    /// Goals of files in a workspace folder live in that folder (.quillpad/workspace.json) instead.
    /// </summary>
    public static class WordGoalStore
    {
        private const string ContainerName = "WordGoals";

        public static int? Get(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            try
            {
                var container = ApplicationData.Current.LocalSettings.CreateContainer(ContainerName, ApplicationDataCreateDisposition.Always);
                return container.Values.TryGetValue(WordGoalUtility.GetStoreKey(filePath), out var value) && value is int goal ? goal : (int?)null;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(WordGoalStore)}] Failed to read a word goal: {ex.Message}");
                return null;
            }
        }

        public static void Set(string filePath, int? goal)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                var container = ApplicationData.Current.LocalSettings.CreateContainer(ContainerName, ApplicationDataCreateDisposition.Always);
                var key = WordGoalUtility.GetStoreKey(filePath);
                if (goal.HasValue) container.Values[key] = goal.Value;
                else container.Values.Remove(key);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(WordGoalStore)}] Failed to save a word goal: {ex.Message}");
            }
        }
    }
}
