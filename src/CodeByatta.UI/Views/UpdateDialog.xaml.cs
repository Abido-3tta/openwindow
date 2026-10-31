using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CodeByatta.Core.Models;
using CodeByatta.Core.Services;

namespace CodeByatta.UI.Views
{
    public partial class UpdateDialog : Window
    {
        private readonly UpdateInfo _updateInfo;
        private readonly UpdateService _updateService;
        private readonly AppConfig _config;
        private CancellationTokenSource? _downloadCts;

        public UpdateDialog(UpdateInfo updateInfo, UpdateService updateService, AppConfig config)
        {
            InitializeComponent();

            _updateInfo = updateInfo ?? throw new ArgumentNullException(nameof(updateInfo));
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            PopulateInfo();
        }

        private void PopulateInfo()
        {
            TxtVersionSummary.Text = $"OpenWindow {(!string.IsNullOrEmpty(_updateInfo.TagName) ? _updateInfo.TagName : "v" + _updateInfo.RemoteVersion)} is now available!";
            TxtCurrentVer.Text = $"v{_updateInfo.CurrentVersion}";
            TxtNewVer.Text = !string.IsNullOrEmpty(_updateInfo.TagName) ? _updateInfo.TagName : $"v{_updateInfo.RemoteVersion}";

            TxtReleaseTitle.Text = string.IsNullOrWhiteSpace(_updateInfo.ReleaseTitle)
                ? $"Release {_updateInfo.TagName}"
                : _updateInfo.ReleaseTitle;

            TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes)
                ? "Performance improvements and bug fixes."
                : _updateInfo.ReleaseNotes.Trim();

            if (_updateInfo.AssetSizeBytes > 0)
            {
                double mb = _updateInfo.AssetSizeBytes / (1024.0 * 1024.0);
                TxtAssetSize.Text = $"Size: {mb:F1} MB";
            }
            else
            {
                TxtAssetSize.Text = string.Empty;
            }

            if (string.IsNullOrEmpty(_updateInfo.DownloadUrl))
            {
                BtnUpdateNow.Content = "Download from GitHub";
            }
        }

        private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_updateInfo.DownloadUrl))
            {
                OpenGitHubRelease();
                Close();
                return;
            }

            BtnUpdateNow.IsEnabled = false;
            BtnLater.IsEnabled = false;
            BtnViewGitHub.IsEnabled = false;
            PnlProgress.Visibility = Visibility.Visible;
            TxtProgressStatus.Text = "Downloading update package...";

            _downloadCts = new CancellationTokenSource();

            var progress = new Progress<double>(percent =>
            {
                Dispatcher.Invoke(() =>
                {
                    int pctVal = (int)Math.Clamp(percent * 100, 0, 100);
                    PrgDownload.Value = pctVal;
                    TxtProgressPercent.Text = $"{pctVal}%";
                });
            });

            try
            {
                string zipPath = await _updateService.DownloadUpdatePackageAsync(
                    _updateInfo.DownloadUrl,
                    progress,
                    _downloadCts.Token);

                TxtProgressStatus.Text = "Applying update and restarting application...";
                PrgDownload.IsIndeterminate = true;

                await Task.Delay(600);

                int pid = Environment.ProcessId;
                _updateService.ApplyUpdateAndRestart(zipPath, pid);

                // Exit current process so the updater script can extract files cleanly
                System.Windows.Application.Current.Shutdown();
            }
            catch (OperationCanceledException)
            {
                TxtProgressStatus.Text = "Update canceled.";
                ResetButtonStates();
            }
            catch (Exception ex)
            {
                TxtProgressStatus.Text = $"Update failed: {ex.Message}";
                PrgDownload.IsIndeterminate = false;
                ResetButtonStates();

                var result = System.Windows.MessageBox.Show(
                    $"Automatic update encountered an error:\n{ex.Message}\n\nWould you like to open the GitHub Releases page to download manually?",
                    "Update Error",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    OpenGitHubRelease();
                    Close();
                }
            }
        }

        private void ResetButtonStates()
        {
            BtnUpdateNow.IsEnabled = true;
            BtnLater.IsEnabled = true;
            BtnViewGitHub.IsEnabled = true;
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            Close();
        }

        private void BtnViewGitHub_Click(object sender, RoutedEventArgs e)
        {
            OpenGitHubRelease();
        }

        private void OpenGitHubRelease()
        {
            try
            {
                string url = !string.IsNullOrEmpty(_updateInfo.HtmlUrl)
                    ? _updateInfo.HtmlUrl
                    : "https://github.com/codebyatta/openwindow/releases/latest";

                Process.Start(new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            _downloadCts?.Cancel();
            base.OnClosed(e);
        }
    }
}
