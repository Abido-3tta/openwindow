using System;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Integration.Tests
{
    public class TopmostE2ETests
    {
        [Fact]
        public void Topmost_OnRealWindow_TogglesAndPersistsState()
        {
            using var fixture = new TestWindowFixture("Topmost Test Window");
            Assert.NotEqual(IntPtr.Zero, fixture.Hwnd);

            var topmostService = new TopmostService();

            // Test 1: Set Topmost True
            bool setTrueSuccess = topmostService.SetTopmost(fixture.Hwnd, true);
            Assert.True(setTrueSuccess);
            Assert.True(topmostService.IsTopmost(fixture.Hwnd));
            Assert.Contains(fixture.Hwnd, topmostService.PinnedWindows);

            // Verify actual Win32 Extended Style bit
            IntPtr exStyle = User32.GetWindowLongPtr(fixture.Hwnd, User32.GWL_EXSTYLE);
            Assert.True(((long)exStyle & User32.WS_EX_TOPMOST) != 0);

            // Test 2: Set Topmost False
            bool setFalseSuccess = topmostService.SetTopmost(fixture.Hwnd, false);
            Assert.True(setFalseSuccess);
            Assert.False(topmostService.IsTopmost(fixture.Hwnd));
            Assert.DoesNotContain(fixture.Hwnd, topmostService.PinnedWindows);

            exStyle = User32.GetWindowLongPtr(fixture.Hwnd, User32.GWL_EXSTYLE);
            Assert.True(((long)exStyle & User32.WS_EX_TOPMOST) == 0);

            // Test 3: Toggle Topmost
            bool toggleResult = topmostService.ToggleTopmost(fixture.Hwnd);
            Assert.True(toggleResult);
            Assert.True(topmostService.IsTopmost(fixture.Hwnd));

            toggleResult = topmostService.ToggleTopmost(fixture.Hwnd);
            Assert.True(toggleResult);
            Assert.False(topmostService.IsTopmost(fixture.Hwnd));
        }
    }
}
