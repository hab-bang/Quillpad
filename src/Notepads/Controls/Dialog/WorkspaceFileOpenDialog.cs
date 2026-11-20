// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.Dialog
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Windows.Storage;
    using Windows.System;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using Notepads.Models;

    /// <summary>
    /// Lists the files of the workspace folder, with a filter box, so the user can pick files to open.
    /// Unlike the Windows file picker, it always starts in the workspace folder.
    /// </summary>
    public sealed class WorkspaceFileOpenDialog : NotepadsDialog
    {
        private readonly IReadOnlyList<WorkspaceFile> _allFiles;
        private readonly HashSet<WorkspaceFile> _selectedFiles = new HashSet<WorkspaceFile>();
        private readonly TextBox _filterTextBox;
        private readonly ListView _fileListView;
        private bool _isRefreshing;

        public WorkspaceFileOpenDialog(string folderName, IReadOnlyList<WorkspaceFile> files)
        {
            _allFiles = files;

            _filterTextBox = new TextBox
            {
                Style = (Style)Application.Current.Resources["TransparentTextBoxStyle"],
                PlaceholderText = ResourceLoader.GetString("WorkspaceFileOpenDialog_FilterPlaceholderText"),
                IsSpellCheckEnabled = false,
                AcceptsReturn = false,
                Height = 35,
            };

            _fileListView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Multiple,
                MaxHeight = 360,
                Margin = new Thickness(0, 8, 0, 0),
            };

            var content = new StackPanel { MinWidth = 400 };
            content.Children.Add(_filterTextBox);

            if (files.Count == 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = ResourceLoader.GetString("WorkspaceFileOpenDialog_NoFilesText"),
                    Margin = new Thickness(4, 12, 4, 0),
                    Opacity = 0.7,
                });
            }
            else
            {
                content.Children.Add(_fileListView);
            }

            Title = string.Format(ResourceLoader.GetString("WorkspaceFileOpenDialog_Title"), folderName);
            Content = content;
            PrimaryButtonText = ResourceLoader.GetString("WorkspaceFileOpenDialog_PrimaryButtonText");
            CloseButtonText = ResourceLoader.GetString("AppCloseSaveReminderDialog_CloseButtonText");
            IsPrimaryButtonEnabled = false;

            _filterTextBox.TextChanged += (sender, args) => RefreshList();
            _filterTextBox.KeyDown += OnFilterKeyDown;
            _fileListView.SelectionChanged += OnSelectionChanged;
            PrimaryButtonClick += (sender, args) => Confirm();
            Opened += (sender, args) => _filterTextBox.Focus(FocusState.Programmatic);

            RefreshList();
        }

        /// <summary>The files to open, empty unless the user confirmed.</summary>
        public IReadOnlyList<StorageFile> FilesToOpen { get; private set; } = Array.Empty<StorageFile>();

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
