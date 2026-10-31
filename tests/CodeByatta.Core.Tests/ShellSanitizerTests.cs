using System;
using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class ShellSanitizerTests
    {
        [Fact]
        public void IsShellOrTaskbarWindow_ZeroHandle_ReturnsTrue()
        {
            Assert.True(ShellSanitizer.IsShellOrTaskbarWindow(IntPtr.Zero));
        }

        [Fact]
        public void SanitizeShellWindows_DoesNotThrow()
        {
            var exception = Record.Exception(() => ShellSanitizer.SanitizeShellWindows());
            Assert.Null(exception);
        }

        [Fact]
        public void EnsureTaskbarOnTop_DoesNotThrow()
        {
            var exception = Record.Exception(() => ShellSanitizer.EnsureTaskbarOnTop());
            Assert.Null(exception);
        }
    }
}
