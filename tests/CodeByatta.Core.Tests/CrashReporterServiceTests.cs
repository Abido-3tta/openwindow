using System;
using System.IO;
using CodeByatta.Core.Models;
using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class CrashReporterServiceTests
    {
        [Fact]
        public void RecordCrash_WritesValidJsonReportToDisk()
        {
            var config = new AppConfig();
            CrashReporterService.Initialize(config, () => new System.Collections.Generic.Dictionary<string, object>
            {
                { "testMode", true }
            });

            var testEx = new InvalidOperationException("Test crash exception for unit testing");
            var report = CrashReporterService.RecordCrash(testEx, "UnitTestContext", "TestApp.exe");

            Assert.NotNull(report);
            Assert.Equal("System.InvalidOperationException", report.ExceptionType);
            Assert.Equal("Test crash exception for unit testing", report.Message);
            Assert.Equal("UnitTestContext", report.Context);
            Assert.Equal("TestApp.exe", report.TargetProcess);
            Assert.False(report.IsReviewed);

            // Verify file exists on disk
            Assert.True(Directory.Exists(CrashReporterService.CrashReportsDir));
            var pending = CrashReporterService.GetPendingReports();
            Assert.Contains(pending, r => r.Id == report.Id);

            // Test MarkReviewed
            CrashReporterService.MarkReportReviewed(report.Id);
            var updatedPending = CrashReporterService.GetPendingReports();
            Assert.DoesNotContain(updatedPending, r => r.Id == report.Id);
        }

        [Fact]
        public async System.Threading.Tasks.Task SendTestPingAsync_ReturnsFalseForEmptyUrl()
        {
            var (success, msg) = await CrashReporterService.SendTestPingAsync("");
            Assert.False(success);
            Assert.Contains("empty", msg, StringComparison.OrdinalIgnoreCase);
        }
    }
}
