using System;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Integration.Tests
{
    public class DwmThumbnailE2ETests
    {
        [Fact]
        public void DwmThumbnail_OnRealWindows_RegistersAndUpdates()
        {
            using var srcFixture = new TestWindowFixture("Source Window");
            using var destFixture = new TestWindowFixture("Destination Viewport");

            Assert.NotEqual(IntPtr.Zero, srcFixture.Hwnd);
            Assert.NotEqual(IntPtr.Zero, destFixture.Hwnd);

            var pipManager = new PipManagerService();

            // Register thumbnail
            RECT destRect = new RECT(0, 0, 200, 150);
            RECT cropRect = new RECT(10, 10, 100, 100);

            IntPtr hThumbnail = pipManager.RegisterPipThumbnail(destFixture.Hwnd, srcFixture.Hwnd, destRect, cropRect);
            Assert.NotEqual(IntPtr.Zero, hThumbnail);

            // Update bounds
            RECT newDest = new RECT(10, 10, 300, 200);
            bool updateSuccess = pipManager.UpdatePipBounds(hThumbnail, newDest, null, 200);
            Assert.True(updateSuccess);

            // Unregister
            pipManager.UnregisterPipThumbnail(hThumbnail);
        }
    }
}
