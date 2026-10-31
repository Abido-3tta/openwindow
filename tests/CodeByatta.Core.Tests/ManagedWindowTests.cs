using System;
using CodeByatta.Core.Models;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class ManagedWindowTests
    {
        [Fact]
        public void ManagedWindow_ZeroHwnd_HandlesGracefully()
        {
            var window = new ManagedWindow(IntPtr.Zero);

            Assert.Equal(IntPtr.Zero, window.Hwnd);
            Assert.Empty(window.Title);
            Assert.Empty(window.ProcessName);
            Assert.False(window.IsTopmost);
            Assert.False(window.IsClickThrough);
            Assert.Equal(255, window.Opacity);
        }

        [Fact]
        public void ManagedWindow_ToString_FormatsCorrectly()
        {
            var window = new ManagedWindow(IntPtr.Zero);
            string str = window.ToString();
            Assert.Contains("[0]", str);
        }
    }
}
