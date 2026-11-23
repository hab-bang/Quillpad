// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.Dialog
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.System;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
    using Notepads.Services;
    using Notepads.Utilities;

    /// <summary>One line in one file of the workspace folder that holds the search text.</summary>
    public sealed class FolderSearchResult
    {
        public FolderSearchResult(StorageFile file, string relativePath, TextMatch match)
        {
            File = file;
            RelativePath = relativePath;
            Match = match;
        }

        public StorageFile File { get; }

        public string RelativePath { get; }

        public TextMatch Match { get; }

        public override string ToString() => $"{RelativePath}:{Match.Line}    {Match.Snippet}";
    }

    /// <summary>
    /// Find in Folder: searches the text files of the workspace folder and lists the lines that match.
    /// Enter searches, and a double click or Open goes to the line.
    /// </summary>
    public sealed class FolderSearchDialog : NotepadsDialog
    {
        private readonly Func<string, Task<IReadOnlyList<FolderSearchResult>>> _search;
        private readonly TextBox _queryTextBox;
        private readonly ProgressRing _progressRing;
        private readonly ListView _resultListView;
        private readonly TextBlock _messageTextBlock;
        private bool _isSearching;

        public FolderSearchDialog(string folderName, Func<string, Task<IReadOnlyList<FolderSearchResult>>> search)
        {
            _search = search;

            _queryTextBox = new TextBox
            {
                Style = (Style)Application.Current.Resources["TransparentTextBoxStyle"],
                PlaceholderText = ResourceLoader.GetString("FolderSearchDialog_QueryPlaceholderText"),
                IsSpellCheckEnabled = false,
                AcceptsReturn = false,
                Height = 35,
            };
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(_queryTextBox, ResourceLoader.GetString("FolderSearchDialog_QueryPlaceholderText"));

            _progressRing = new ProgressRing
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 16, 0, 8),
                Visibility = Visibility.Collapsed,
            };

            _resultListView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Single,
                IsItemClickEnabled = false,
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

            var content = new StackPanel { MinWidth = 480 };
            content.Children.Add(_queryTextBox);
            content.Children.Add(_progressRing);
            content.Children.Add(_resultListView);
            content.Children.Add(_messageTextBlock);

            Title = string.Format(ResourceLoader.GetString("FolderSearchDialog_Title"), folderName);
            Content = content;
            PrimaryButtonText = ResourceLoader.GetString("FolderSearchDialog_PrimaryButtonText");
            CloseButtonText = ResourceLoader.GetString("AppCloseSaveReminderDialog_CloseButtonText");
            IsPrimaryButtonEnabled = false;

            _queryTextBox.KeyDown += OnQueryKeyDown;
            _resultListView.SelectionChanged += (sender, args) => IsPrimaryButtonEnabled = _resultListView.SelectedItem != null;
            _resultListView.DoubleTapped += (sender, args) => OpenSelected();
            _resultListView.KeyDown += (sender, args) =>
            {
                if (args.Key != VirtualKey.Enter) return;
                args.Handled = true;
                OpenSelected();
            };
            PrimaryButtonClick += (sender, args) => SelectedResult = _resultListView.SelectedItem as FolderSearchResult;
            Opened += (sender, args) => _queryTextBox.Focus(FocusState.Programmatic);
        }

        /// <summary>The line to open, null unless the user picked one.</summary>
        public FolderSearchResult SelectedResult { get; private set; }

        private void OpenSelected()
        {
            if (!(_resultListView.SelectedItem is FolderSearchResult result)) return;
            SelectedResult = result;
            Hide();
        }

        private async void OnQueryKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Down && _resultListView.Items?.Count > 0)
            {
                e.Handled = true;
                _resultListView.SelectedIndex = Math.Max(0, _resultListView.SelectedIndex);
                _resultListView.Focus(FocusState.Keyboard);
                return;
            }

            if (e.Key != VirtualKey.Enter) return;
            e.Handled = true;

            var query = _queryTextBox.Text.Trim();
            if (query.Length == 0 || _isSearching) return;

            _isSearching = true;
            _progressRing.IsActive = true;
            _progressRing.Visibility = Visibility.Visible;
            _resultListView.Visibility = Visibility.Collapsed;
            _messageTextBlock.Visibility = Visibility.Collapsed;

            IReadOnlyList<FolderSearchResult> results = Array.Empty<FolderSearchResult>();

            try
            {
                results = await _search(query);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(FolderSearchDialog)}] Failed to search the folder: {ex.Message}");
            }

            _isSearching = false;
            _progressRing.IsActive = false;
            _progressRing.Visibility = Visibility.Collapsed;
            _resultListView.ItemsSource = results;

            if (results.Count == 0)
            {
                ShowMessage(ResourceLoader.GetString("FolderSearchDialog_NoResultsText"));
                return;
            }

            _resultListView.Visibility = Visibility.Visible;
            if (results.Count >= MaxResults)
            {
                ShowMessage(string.Format(ResourceLoader.GetString("FolderSearchDialog_TooManyResultsText"), MaxResults));
            }
        }

        public const int MaxResults = 500;

        private void ShowMessage(string message)
        {
            _messageTextBlock.Text = message;
            _messageTextBlock.Visibility = Visibility.Visible;
        }
    }
}
