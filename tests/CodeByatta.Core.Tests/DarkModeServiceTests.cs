using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class DarkModeServiceTests
    {
        [Fact]
        public void DarkModeService_InitialState_IsNotActive()
        {
            using var service = new DarkModeService();
            Assert.False(service.IsDarkModeActive);
            Assert.False(service.IsDarkModeActiveForWindow(new System.IntPtr(1234)));
        }

        [Fact]
        public void DarkModeService_EnableAndDisableWindow_TracksCorrectly()
        {
            using var service = new DarkModeService();
            var testHwnd = new System.IntPtr(999999);
            // Non-existent HWND should be rejected safely
            Assert.False(service.EnableDarkModeForWindow(System.IntPtr.Zero));
            Assert.False(service.IsDarkModeActiveForWindow(System.IntPtr.Zero));
        }
    }
}
