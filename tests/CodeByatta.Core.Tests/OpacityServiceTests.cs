using System;
using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class OpacityServiceTests
    {
        [Fact]
        public void OpacityService_InvalidHwnd_ReturnsFalse()
        {
            var service = new OpacityService();
            IntPtr fakeHwnd = new IntPtr(99999999);

            bool result = service.SetOpacity(fakeHwnd, 180);
            Assert.False(result);

            bool toggleResult = service.ToggleTransparency(fakeHwnd);
            Assert.False(toggleResult);

            bool clickThrough = service.IsClickThrough(fakeHwnd);
            Assert.False(clickThrough);

            bool headerInteractive = service.SetHeaderInteractive(fakeHwnd, true);
            Assert.False(headerInteractive);
        }

        [Fact]
        public void OpacityService_ActiveClickThroughWindows_InitiallyEmpty()
        {
            var service = new OpacityService();
            Assert.Empty(service.ActiveClickThroughWindows);
        }

        [Fact]
        public void OpacityService_ActiveTransparentWindows_InitiallyEmpty()
        {
            var service = new OpacityService();
            Assert.Empty(service.ActiveTransparentWindows);
            Assert.Equal(255, service.GetConfiguredOpacity(new IntPtr(12345)));
            Assert.False(service.IsTemporarilyBoosted(new IntPtr(12345)));
            Assert.False(service.SetTemporaryHoverReveal(new IntPtr(12345), true));
        }

        [Fact]
        public void OpacityService_PruneDeadWindows_CleansNonExistentHandles()
        {
            var service = new OpacityService();
            service.PruneDeadWindows();
            Assert.Empty(service.ActiveClickThroughWindows);
            Assert.Empty(service.ActiveTransparentWindows);
        }
    }
}
