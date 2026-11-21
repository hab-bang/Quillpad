// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.Settings
{
    using System;
    using Notepads.Extensions;
    using Notepads.Services;
    using Notepads.Utilities;
    using Windows.ApplicationModel;
    using Windows.ApplicationModel.Resources;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;

    public sealed partial class AboutPage : Page
    {
        public string AppName => App.ApplicationName;

        public string AppVersion => $"v{GetAppVersion()}";

        private readonly ResourceLoader _resourceLoader = ResourceLoader.GetForCurrentView();

        private UpdateCheckResult _update;

        public AboutPage()
        {
            InitializeComponent();
            SetAppIconBasedOnTheme(ThemeSettingsService.ThemeMode);

            Loaded += AboutPage_Loaded;
            Unloaded += AboutPage_Unloaded;
        }

        private void AboutPage_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeSettingsService.OnThemeChanged += ThemeSettingsService_OnThemeChanged;
        }

        private void AboutPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeSettingsService.OnThemeChanged -= ThemeSettingsService_OnThemeChanged;
        }

        private async void ThemeSettingsService_OnThemeChanged(object sender, ElementTheme theme)
        {
            await Dispatcher.CallOnUIThreadAsync(() =>
            {
                SetAppIconBasedOnTheme(theme);
            });
        }

        private void SetAppIconBasedOnTheme(ElementTheme theme)
        {
            if (theme == ElementTheme.Dark || theme == ElementTheme.Default)
            {
                AppIconImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/appicon_w.png"));
            }
            else
            {
                AppIconImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/appicon_b.png"));
            }
        }

        private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdatesButton.IsEnabled = false;
            InstallUpdateButton.Visibility = Visibility.Collapsed;
            ReleasePageLink.Visibility = Visibility.Collapsed;
            ShowUpdateStatus(_resourceLoader.GetString("AboutPage_Update_Checking"));

            _update = await UpdateService.CheckForUpdateAsync();
            CheckForUpdatesButton.IsEnabled = true;

            if (_update.Status == UpdateCheckStatus.Failed)
            {
                ShowUpdateStatus(string.Format(_resourceLoader.GetString("AboutPage_Update_CheckFailed"), _update.Error));
                return;
            }

            string latest = _update.LatestVersion.ToString(3);
            ReleasePageLink.NavigateUri = _update.ReleasePage;
            ReleasePageLink.Visibility = Visibility.Visible;

            if (!UpdateService.CanInstallUpdates)
            {
                ShowUpdateStatus(string.Format(_resourceLoader.GetString("AboutPage_Update_DevBuild"), latest));
            }
            else if (_update.Status == UpdateCheckStatus.UpToDate)
            {
                ShowUpdateStatus(string.Format(_resourceLoader.GetString("AboutPage_Update_UpToDate"), latest));
            }
            else if (_update.AppInstallerFile == null)
            {
                ShowUpdateStatus(string.Format(_resourceLoader.GetString("AboutPage_Update_AvailableManualOnly"), latest));
            }
            else
            {
                string message = AppSettingsService.IsSessionSnapshotEnabled
                    ? "AboutPage_Update_Available"
                    : "AboutPage_Update_AvailableNoSessionBackup";
                ShowUpdateStatus(string.Format(_resourceLoader.GetString(message), latest));
                InstallUpdateButton.IsEnabled = true;
                InstallUpdateButton.Visibility = Visibility.Visible;
            }
        }

        private async void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            string latest = _update.LatestVersion.ToString(3);
            CheckForUpdatesButton.IsEnabled = false;
            InstallUpdateButton.IsEnabled = false;
            ShowUpdateStatus(string.Format(_resourceLoader.GetString("AboutPage_Update_Installing"), latest));
            UpdateProgressBar.IsIndeterminate = true;
            UpdateProgressBar.Visibility = Visibility.Visible;

            // The install closes every Quillpad window, so keep the open tabs first
            await SessionUtility.SaveAllSessionsAsync();

            var progress = new Progress<double>(percentage =>
            {
                UpdateProgressBar.IsIndeterminate = false;
                UpdateProgressBar.Value = percentage;
            });
            string error = await UpdateService.InstallUpdateAsync(_update.AppInstallerFile, progress);

            // Still running: the install failed, or finished without having to close the app
            UpdateProgressBar.Visibility = Visibility.Collapsed;
            CheckForUpdatesButton.IsEnabled = true;
            if (error != null)
            {
                ShowUpdateStatus(string.Format(_resourceLoader.GetString("AboutPage_Update_InstallFailed"), error));
                InstallUpdateButton.IsEnabled = true;
            }
            else
            {
                ShowUpdateStatus(string.Format(_resourceLoader.GetString("AboutPage_Update_Installed"), latest));
                InstallUpdateButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowUpdateStatus(string message)
        {
            UpdateStatusTextBlock.Text = message;
            UpdateStatusTextBlock.Visibility = Visibility.Visible;
        }

        private static string GetAppVersion()
        {
            PackageVersion version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }
}