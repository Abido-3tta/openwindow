using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodeByatta.Core.Services
{
    public class UpdateInfo
    {
        public Version CurrentVersion { get; set; } = new Version(1, 1, 0);
        public Version RemoteVersion { get; set; } = new Version(1, 1, 0);
        public string TagName { get; set; } = string.Empty;
        public string ReleaseTitle { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public long AssetSizeBytes { get; set; }
        public bool IsNewer => RemoteVersion > CurrentVersion;
    }

    public class UpdateService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/Abido-3tta/openwindow/releases/latest";
        private readonly HttpClient _httpClient;

        public UpdateService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "OpenWindow-App");
            }
            _httpClient.Timeout = TimeSpan.FromSeconds(8);
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync(Version? currentVersion = null, CancellationToken ct = default)
        {
            currentVersion ??= Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 1, 0);

            try
            {
                using var response = await _httpClient.GetAsync(GitHubApiUrl, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
                string releaseTitle = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
                string releaseNotes = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var htmlElem) ? htmlElem.GetString() ?? "" : "";

                // Parse version from tag_name (e.g. "v1.2.0" or "1.2.0")
                string cleanVer = tagName.TrimStart('v', 'V');
                if (!Version.TryParse(cleanVer, out var remoteVer))
                {
                    return null;
                }

                // Match architecture (x64 vs x86)
                string archNeedle = Environment.Is64BitOperatingSystem ? "win-x64" : "win-x86";
                string downloadUrl = "";
                long assetSize = 0;

                if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsElem.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                        if (name.Contains(archNeedle, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.TryGetProperty("browser_download_url", out var du) ? du.GetString() ?? "" : "";
                            assetSize = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                            break;
                        }
                    }

                    // Fallback to first zip if architecture match was not found
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        foreach (var asset in assetsElem.EnumerateArray())
                        {
                            string name = asset.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.TryGetProperty("browser_download_url", out var du) ? du.GetString() ?? "" : "";
                                assetSize = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                                break;
                            }
                        }
                    }
                }

                return new UpdateInfo
                {
                    CurrentVersion = currentVersion,
                    RemoteVersion = remoteVer,
                    TagName = tagName,
                    ReleaseTitle = string.IsNullOrWhiteSpace(releaseTitle) ? tagName : releaseTitle,
                    ReleaseNotes = releaseNotes,
                    HtmlUrl = htmlUrl,
                    DownloadUrl = downloadUrl,
                    AssetSizeBytes = assetSize
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> DownloadUpdatePackageAsync(
            string downloadUrl,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new ArgumentException("Download URL cannot be empty.", nameof(downloadUrl));
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "CodeByatta_Updates");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            string targetZip = Path.Combine(tempDir, $"update_{DateTime.UtcNow.Ticks}.zip");

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var sourceStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var fileStream = new FileStream(targetZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;

            while ((read = await sourceStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                totalRead += read;

                if (totalBytes > 0 && progress != null)
                {
                    progress.Report((double)totalRead / totalBytes);
                }
            }

            return targetZip;
        }

        public void ApplyUpdateAndRestart(string zipFilePath, int currentPid)
        {
            if (!File.Exists(zipFilePath))
            {
                throw new FileNotFoundException("Update package not found.", zipFilePath);
            }

            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string exePath = Path.Combine(appDir, "CodeByatta.UI.exe");
            string updaterScript = Path.Combine(Path.GetTempPath(), $"codebyatta_updater_{DateTime.UtcNow.Ticks}.ps1");

            // PowerShell updater script:
            // 1. Wait for current app process to terminate
            // 2. Extract update zip directly over installation directory
            // 3. Launch updated CodeByatta.UI.exe
            // 4. Delete temp zip and script
            string scriptContent = $@"
$ErrorActionPreference = 'SilentlyContinue'
Start-Sleep -Milliseconds 800
$proc = Get-Process -Id {currentPid} -ErrorAction SilentlyContinue
if ($proc) {{
    $proc | Wait-Process -Timeout 6 -ErrorAction SilentlyContinue
}}
Start-Sleep -Milliseconds 500

Expand-Archive -Path '{zipFilePath}' -DestinationPath '{appDir}' -Force

Start-Process -FilePath '{exePath}'
Start-Sleep -Seconds 2
Remove-Item -Path '{zipFilePath}' -Force
Remove-Item -Path $MyInvocation.MyCommand.Path -Force
";
            File.WriteAllText(updaterScript, scriptContent);

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{updaterScript}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(startInfo);
        }
    }
}
