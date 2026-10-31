using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeByatta.Core.Models;

namespace CodeByatta.Core.Services
{
    public class CrashReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string AppVersion { get; set; } = "1.1.3";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string OsVersion { get; set; } = Environment.OSVersion.VersionString;
        public string ExceptionType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public string? TargetProcess { get; set; }
        public Dictionary<string, object>? ActiveFeatures { get; set; }
        public bool IsReviewed { get; set; } = false;
    }

    public static class CrashReporterService
    {
        private static AppConfig? _config;
        private static Func<Dictionary<string, object>>? _activeFeaturesProvider;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        private static readonly object _lock = new object();

        public static string CrashReportsDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeByatta",
            "OpenWindow",
            "crash_reports");

        public static void Initialize(AppConfig config, Func<Dictionary<string, object>>? activeFeaturesProvider = null)
        {
            _config = config;
            _activeFeaturesProvider = activeFeaturesProvider;

            try
            {
                if (!Directory.Exists(CrashReportsDir))
                {
                    Directory.CreateDirectory(CrashReportsDir);
                }
            }
            catch { }

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    RecordCrash(ex, "AppDomain.UnhandledException");
                }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                RecordCrash(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }

        public static CrashReport RecordCrash(Exception ex, string context = "", string? targetProcess = null)
        {
            var report = new CrashReport
            {
                Timestamp = DateTime.UtcNow,
                ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.StackTrace ?? ex.ToString(),
                Context = context,
                TargetProcess = targetProcess
            };

            try
            {
                if (_activeFeaturesProvider != null)
                {
                    report.ActiveFeatures = _activeFeaturesProvider();
                }
            }
            catch { }

            SaveReportToDisk(report);

            if (_config != null && !string.IsNullOrWhiteSpace(_config.TelemetryWebhookUrl))
            {
                _ = DispatchWebhookAsync(report, _config.TelemetryWebhookUrl);
            }

            return report;
        }

        private static void SaveReportToDisk(CrashReport report)
        {
            lock (_lock)
            {
                try
                {
                    if (!Directory.Exists(CrashReportsDir))
                    {
                        Directory.CreateDirectory(CrashReportsDir);
                    }

                    string fileName = $"crash_{report.Timestamp:yyyyMMdd_HHmmss}_{report.Id.Substring(0, 8)}.json";
                    string filePath = Path.Combine(CrashReportsDir, fileName);

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(report, options);
                    File.WriteAllText(filePath, json);
                }
                catch { }
            }
        }

        public static async Task<bool> DispatchWebhookAsync(CrashReport report, string webhookUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webhookUrl)) return false;

                StringContent content;
                if (webhookUrl.Contains("discord.com/api/webhooks", StringComparison.OrdinalIgnoreCase) ||
                    webhookUrl.Contains("discordapp.com/api/webhooks", StringComparison.OrdinalIgnoreCase))
                {
                    string safeStack = report.StackTrace;
                    if (safeStack.Length > 900) safeStack = safeStack.Substring(0, 900) + "...";

                    var discordPayload = new
                    {
                        content = "🚨 **OpenWindow v1.1.3 Crash Telemetry Report**",
                        embeds = new[]
                        {
                            new
                            {
                                title = $"Exception: {report.ExceptionType}",
                                description = report.Message,
                                color = 16724784, // #FF3B30
                                fields = new[]
                                {
                                    new { name = "Context", value = string.IsNullOrEmpty(report.Context) ? "Runtime" : report.Context, inline = true },
                                    new { name = "OS Version", value = report.OsVersion, inline = true },
                                    new { name = "Target App", value = string.IsNullOrEmpty(report.TargetProcess) ? "None" : report.TargetProcess, inline = true },
                                    new { name = "Stack Trace", value = $"```{safeStack}```", inline = false }
                                },
                                footer = new { text = $"Report ID: {report.Id} • CodeByatta Diagnostics" }
                            }
                        }
                    };
                    string json = JsonSerializer.Serialize(discordPayload);
                    content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else
                {
                    string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
                    content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                var response = await _httpClient.PostAsync(webhookUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<(bool success, string message)> SendTestPingAsync(string webhookUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webhookUrl))
                    return (false, "Webhook URL cannot be empty.");

                var testReport = new CrashReport
                {
                    ExceptionType = "TestPingNotification",
                    Message = "OpenWindow Webhook Integration Test: Telemetry Dispatch Verified Successfully!",
                    Context = "SettingsWindow.TestPing",
                    TargetProcess = "OpenWindow",
                    StackTrace = "No error. This is an intentional diagnostic ping to verify telemetry reachability."
                };

                bool ok = await DispatchWebhookAsync(testReport, webhookUrl);
                if (ok)
                {
                    return (true, "Telemetry ping sent successfully! Your webhook is fully operational.");
                }
                else
                {
                    return (false, "Failed to deliver telemetry ping. Check webhook URL and permissions.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error testing webhook: {ex.Message}");
            }
        }

        public static List<CrashReport> GetPendingReports()
        {
            var reports = new List<CrashReport>();
            try
            {
                if (!Directory.Exists(CrashReportsDir)) return reports;

                var files = Directory.GetFiles(CrashReportsDir, "crash_*.json");
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var rep = JsonSerializer.Deserialize<CrashReport>(json);
                        if (rep != null && !rep.IsReviewed)
                        {
                            reports.Add(rep);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return reports;
        }

        public static void MarkReportReviewed(string reportId)
        {
            try
            {
                if (!Directory.Exists(CrashReportsDir)) return;

                var files = Directory.GetFiles(CrashReportsDir, "crash_*.json");
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        if (json.Contains(reportId, StringComparison.OrdinalIgnoreCase))
                        {
                            var rep = JsonSerializer.Deserialize<CrashReport>(json);
                            if (rep != null)
                            {
                                rep.IsReviewed = true;
                                File.WriteAllText(file, JsonSerializer.Serialize(rep, new JsonSerializerOptions { WriteIndented = true }));
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
