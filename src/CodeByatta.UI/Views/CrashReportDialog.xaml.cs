using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using CodeByatta.Core.Services;

namespace CodeByatta.UI.Views
{
    public partial class CrashReportDialog : Window
    {
        private readonly CrashReport _report;

        public CrashReportDialog(CrashReport report)
        {
            InitializeComponent();
            _report = report;

            Loaded += (s, e) =>
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    TxtDiagnostics.Text = JsonSerializer.Serialize(_report, options);
                }
                catch
                {
                    TxtDiagnostics.Text = $"Timestamp: {_report.Timestamp:u}\nException: {_report.ExceptionType}\nMessage: {_report.Message}\nStack:\n{_report.StackTrace}";
                }
            };
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(TxtDiagnostics.Text);
                BtnCopy.Content = "✓ Copied!";
            }
            catch
            {
                BtnCopy.Content = "Failed to copy";
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = CrashReporterService.CrashReportsDir;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CrashReporterService.MarkReportReviewed(_report.Id);
            Close();
        }
    }
}
