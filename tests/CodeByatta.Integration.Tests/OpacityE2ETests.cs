using System;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Integration.Tests
{
    public class OpacityE2ETests
    {
        [Fact]
        public void Opacity_OnRealWindow_SetsAlphaAndClickThrough()
        {
            using var fixture = new TestWindowFixture("Opacity Test Window");
            Assert.NotEqual(IntPtr.Zero, fixture.Hwnd);

            var opacityService = new OpacityService();

            // Test 1: Set opacity to 180 (approx 70%)
            bool setAlphaSuccess = opacityService.SetOpacity(fixture.Hwnd, 180);
            Assert.True(setAlphaSuccess);

            // Verify WS_EX_LAYERED is set
            IntPtr exStyle = User32.GetWindowLongPtr(fixture.Hwnd, User32.GWL_EXSTYLE);
            Assert.True(((long)exStyle & User32.WS_EX_LAYERED) != 0);

            // Verify GetLayeredWindowAttributes reports alpha 180
            bool getSuccess = User32.GetLayeredWindowAttributes(fixture.Hwnd, out _, out byte alpha, out uint flags);
            Assert.True(getSuccess);
            Assert.Equal(180, alpha);
            Assert.True((flags & User32.LWA_ALPHA) != 0);

            // Test 2: Enable Click-Through
            bool clickThroughSuccess = opacityService.SetClickThrough(fixture.Hwnd, true);
            Assert.True(clickThroughSuccess);
            Assert.True(opacityService.IsClickThrough(fixture.Hwnd));
            Assert.Contains(fixture.Hwnd, opacityService.ActiveClickThroughWindows);

            // Test 2b: Header interactivity on click-through window
            opacityService.SetHeaderInteractive(fixture.Hwnd, true);
            exStyle = User32.GetWindowLongPtr(fixture.Hwnd, User32.GWL_EXSTYLE);
            Assert.True(((long)exStyle & User32.WS_EX_TRANSPARENT) == 0);

            opacityService.SetHeaderInteractive(fixture.Hwnd, false);
            exStyle = User32.GetWindowLongPtr(fixture.Hwnd, User32.GWL_EXSTYLE);
            Assert.True(((long)exStyle & User32.WS_EX_TRANSPARENT) != 0);

            // Test 3: Disable Click-Through
            clickThroughSuccess = opacityService.SetClickThrough(fixture.Hwnd, false);
            Assert.True(clickThroughSuccess);
            Assert.False(opacityService.IsClickThrough(fixture.Hwnd));
            Assert.DoesNotContain(fixture.Hwnd, opacityService.ActiveClickThroughWindows);

            exStyle = User32.GetWindowLongPtr(fixture.Hwnd, User32.GWL_EXSTYLE);
            Assert.True(((long)exStyle & User32.WS_EX_TRANSPARENT) == 0);

            // Test 4: Reset Window Opacity to 100%
            bool resetSuccess = opacityService.ResetWindow(fixture.Hwnd);
            Assert.True(resetSuccess);

            exStyle = User32.GetWindowLongPtr(fixture.Hwnd, User32.GWL_EXSTYLE);
            Assert.True(((long)exStyle & User32.WS_EX_LAYERED) == 0);
        }
    }
}
