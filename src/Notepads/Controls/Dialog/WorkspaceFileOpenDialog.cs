// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.Dialog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.System;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using Notepads.Models;
    using Notepads.Services;
    using Notepads.Utilities;

    /// <summary>
    /// Lists the files of the workspace folder, with a filter box, so the user can pick files to open.
    /// Unlike the Windows file picker, it always starts in the workspace folder.
    /// It opens at once and shows a progress ring while a big folder is still being listed.
    /// </summary>
    public sealed class WorkspaceFileOpenDialog : NotepadsDialog
    {
        private readonly Task<IReadOnlyList<WorkspaceFile>> _filesTask;
        private readonly HashSet<WorkspaceFile> _selectedFiles = new HashSet<WorkspaceFile>();
        private readonly TextBox _filterTextBox;
        private readonly ProgressRing _progressRing;
        private readonly ListView _fileListView;
        private readonly TextBlock _messageTextBlock;
        private IReadOnlyList<WorkspaceFile> _allFiles = Array.Empty<WorkspaceFile>();
        private bool _isRefreshing;

        /// <param name="filesTask">The folder listing, already started, so the dialog can show while it runs.</param>
        public WorkspaceFileOpenDialog(string folderName, Task<IReadOnlyList<WorkspaceFile>> filesTask)
        {
            _filesTask = filesTask;

            _filterTextBox = new TextBox
            {
                Style = (Style)Application.Current.Resources["TransparentTextBoxStyle"],
                PlaceholderText = ResourceLoader.GetString("WorkspaceFileOpenDialog_FilterPlaceholderText"),
                IsSpellCheckEnabled = false,
                AcceptsReturn = false,
                Height = 35,
            };

            _progressRing = new ProgressRing
            {
                IsActive = true,
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 16, 0, 8),
            };

            _fileListView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Multiple,
                MaxHeight = 360,
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed,
            };

            _messageTextBlock = new TextBlock
            {
                Margin = new Thickness(4, 12, 4, 0),
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };

            var content = new StackPanel { MinWidth = 400 };
            content.Children.Add(_filterTextBox);
            content.Children.Add(_progressRing);
            content.Children.Add(_fileListView);
            content.Children.Add(_messageTextBlock);

            Title = string.Format(ResourceLoader.GetString("WorkspaceFileOpenDialog_Title"), folderName);
            Content = content;
            PrimaryButtonText = ResourceLoader.GetString("WorkspaceFileOpenDialog_PrimaryButtonText");
            CloseButtonText = ResourceLoader.GetString("AppCloseSaveReminderDialog_CloseButtonText");
            IsPrimaryButtonEnabled = false;

            _filterTextBox.TextChanged += (sender, args) => RefreshList();
            _filterTextBox.KeyDown += OnFilterKeyDown;
            _fileListView.SelectionChanged += OnSelectionChanged;
            PrimaryButtonClick += (sender, args) => Confirm();
            Opened += async (sender, args) =>
            {
                _filterTextBox.Focus(FocusState.Programmatic);
                await ShowFilesAsync();
            };
        }

        /// <summary>The files to open, empty unless the user confirmed.</summary>
        public IReadOnlyList<StorageFile> FilesToOpen { get; private set; } = Array.Empty<StorageFile>();

        private async Task ShowFilesAsync()
        {
            try
            {
                _allFiles = await _filesTask;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(WorkspaceFileOpenDialog)}] Failed to list the workspace files: {ex.Message}");
            }

            _progressRing.IsActive = false;
            _progressRing.Visibility = Visibility.Collapsed;

            if (_allFiles.Count == 0)
            {
                ShowMessage(ResourceLoader.GetString("WorkspaceFileOpenDialog_NoFilesText"));
                return;
            }

            if (_allFiles.Count >= WorkspaceUtility.MaxListedFiles)
            {
                ShowMessage(string.Format(ResourceLoader.GetString("WorkspaceFileOpenDialog_TooManyFilesText"), WorkspaceUtility.MaxListedFiles));
            }

            _fileListView.Visibility = Visibility.Visible;
            RefreshList();
        }

        private void ShowMessage(string message)
        {
            _messageTextBlock.Text = message;
            _messageTextBlock.Visibility = Visibility.Visible;
        }

        private void RefreshList()
        {
            var filter = _filterTextBox.Text.Trim();
            var visibleFiles = string.IsNullOrEmpty(filter)
                ? _allFiles
                : _allFiles.Where(file => file.RelativePath.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Rebuilding the list clears its selection, so put back what the user already ticked
            _isRefreshing = true;
            _fileListView.ItemsSource = visibleFiles;
            foreach (var file in visibleFiles.Where(_selectedFiles.Contains))
            {
                _fileListView.SelectedItems.Add(file);
            }
            _isRefreshing = false;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing) return;

            foreach (WorkspaceFile file in e.RemovedItems) _selectedFiles.Remove(file);
            foreach (WorkspaceFile file in e.AddedItems) _selectedFiles.Add(file);

            IsPrimaryButtonEnabled = _selectedFiles.Count > 0;
        }

        // Enter opens the ticked files, or the first match when nothing is ticked
        private void OnFilterKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter) return;

            if (_selectedFiles.Count == 0 && _fileListView.Items?.FirstOrDefault() is WorkspaceFile firstMatch)
            {
                _selectedFiles.Add(firstMatch);
            }

            if (_selectedFiles.Count == 0) return;

            e.Handled = true;
            Confirm();
            Hide();
        }

        private void Confirm()
        {
            FilesToOpen = _allFiles.Where(_selectedFiles.Contains).Select(file => file.File).ToList();
        }
    }
}
