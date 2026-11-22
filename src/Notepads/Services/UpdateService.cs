// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Services
{
    using System;
    using System.Threading.Tasks;
    using Windows.ApplicationModel;
    using Windows.Data.Json;
    using Windows.Foundation;
    using Windows.Management.Deployment;
    using Windows.Web.Http;
    using Windows.Web.Http.Filters;

    internal enum UpdateCheckStatus
    {
        UpToDate,
        UpdateAvailable,
        Failed,
    }

    internal sealed class UpdateCheckResult
    {
        public UpdateCheckStatus Status { get; set; }

        public Version LatestVersion { get; set; }

        public Uri ReleasePage { get; set; }

        // The release's update feed. Releases up to v0.3.1 have none; those can only be installed by hand.
        public Uri AppInstallerFile { get; set; }

        public string Error { get; set; }
    }

    // Finds the newest Quillpad release on GitHub and installs it through its .appinstaller update feed.
    // Installing from the feed also tells Windows where to look for later updates: from then on it checks
    // the feed on launch and in the background (see the "Collect the release files" step of release.yml).
    internal static class UpdateService
    {
        private const string LatestReleaseApi = "https://api.github.com/repos/hab-bang/Quillpad/releases/latest";
        private const string AppInstallerFileName = "Quillpad.appinstaller";
        private const string ReleasePackageName = "Quillpad";

        // Dev builds install as Quillpad-Dev. The feed would install Quillpad next to them, not update them.
        public static bool CanInstallUpdates => Package.Current.Id.Name == ReleasePackageName;

        public static Version InstalledVersion
        {
            get
            {
                PackageVersion version = Package.Current.Id.Version;
                return new Version(version.Major, version.Minor, version.Build, version.Revision);
            }
        }

        public static async Task<UpdateCheckResult> CheckForUpdateAsync()
        {
            try
            {
                var filter = new HttpBaseProtocolFilter();
                filter.CacheControl.ReadBehavior = HttpCacheReadBehavior.MostRecent;

                using (var client = new HttpClient(filter))
                {
                    client.DefaultRequestHeaders.UserAgent.TryParseAdd("Quillpad-UpdateCheck");
                    client.DefaultRequestHeaders.Accept.TryParseAdd("application/vnd.github+json");

                    using (HttpResponseMessage response = await client.GetAsync(new Uri(LatestReleaseApi)))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            return Failed($"GitHub answered {(int)response.StatusCode} {response.ReasonPhrase}");
                        }

                        return ParseRelease(await response.Content.ReadAsStringAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException(ex);
                return Failed(ex.Message);
            }
        }

        // Set by the main window. The install ends the app without a close request, so this saves what
        // closing the window would. It returns false when unsaved text cannot be kept.
        public static Func<Task<bool>> SaveBeforeInstallAsync { get; set; }

        // Set by the main window. Runs when an install ends and the app is still running (it failed, or
        // finished without closing the app), so the window can undo what SaveBeforeInstallAsync set up.
        public static Action InstallStopped { get; set; }

        // Static, because the settings frame makes a new About page each time it is opened
        public static bool IsInstalling { get; private set; }

        // Closes every Quillpad window when the new version is ready to register. The returned task finishes
        // only if the install fails, with the reason.
        public static async Task<string> InstallUpdateAsync(Uri appInstallerFile, IProgress<double> progress)
        {
            IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> operation = null;
            IsInstalling = true;
            try
            {
                // The call downloads the feed before it returns, so keep it off the UI thread
                operation = await Task.Run(() =>
                {
                    var packageManager = new PackageManager();
                    return packageManager.AddPackageByAppInstallerFileAsync(
                        appInstallerFile,
                        AddPackageByAppInstallerOptions.ForceTargetAppShutdown,
                        packageManager.GetDefaultPackageVolume());
                });
                operation.Progress = (_, deploymentProgress) => progress?.Report(deploymentProgress.percentage);

                DeploymentResult result = await operation;
                return string.IsNullOrEmpty(result.ErrorText) ? null : result.ErrorText;
            }
            catch (Exception ex)
            {
                LoggingService.LogException(ex);

                // A failed deployment throws with only the short HRESULT text. The details (which dependency,
                // conflict or file) are in the operation's DeploymentResult.
                string details = null;
                try
                {
                    if (operation?.Status == AsyncStatus.Error)
                    {
                        details = operation.GetResults()?.ErrorText;
                    }
                }
                catch (Exception)
                {
                    // No result to read, so the exception message is all there is
                }

                return string.IsNullOrEmpty(details) ? ex.Message : details;
            }
            finally
            {
                IsInstalling = false;
                InstallStopped?.Invoke();
            }
        }

        private static UpdateCheckResult ParseRelease(string json)
        {
            JsonObject release = JsonObject.Parse(json);
            string tag = release.GetNamedString("tag_name", string.Empty);

            if (!Version.TryParse(tag.TrimStart('v'), out Version latest))
            {
                return Failed($"The latest release has an unexpected tag: {tag}");
            }

            // Tags are v1.2.3 and packages 1.2.3.0, so pad the tag to compare the two
            latest = new Version(latest.Major, latest.Minor, Math.Max(latest.Build, 0), Math.Max(latest.Revision, 0));

            var result = new UpdateCheckResult
            {
                Status = latest > InstalledVersion ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate,
                LatestVersion = latest,
                ReleasePage = new Uri(release.GetNamedString("html_url", "https://github.com/hab-bang/Quillpad/releases")),
            };

            foreach (IJsonValue value in release.GetNamedArray("assets", new JsonArray()))
            {
                JsonObject asset = value.GetObject();
                if (asset.GetNamedString("name", string.Empty) == AppInstallerFileName)
                {
                    result.AppInstallerFile = new Uri(asset.GetNamedString("browser_download_url"));
                }
            }

            return result;
        }

        private static UpdateCheckResult Failed(string error)
        {
            return new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Error = error };
        }
    }
}
