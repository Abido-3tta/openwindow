using System;
using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class TopmostServiceTests
    {
        [Fact]
        public void TopmostService_InvalidHwnd_ReturnsFalse()
        {
            var service = new TopmostService();
            IntPtr fakeHwnd = new IntPtr(99999999);

            bool result = service.SetTopmost(fakeHwnd, true);
            Assert.False(result);
            Assert.False(service.IsTopmost(fakeHwnd));
        }

        [Fact]
        public void TopmostService_PinnedList_TracksItems()
        {
            var service = new TopmostService();
            Assert.Empty(service.PinnedWindows);
        }

        [Fact]
        public void TopmostService_TopmostStateChanged_EventDefined()
        {
            var service = new TopmostService();
            bool eventInvoked = false;
            service.TopmostStateChanged += (s, e) => eventInvoked = true;

            // Invoking SetTopmost with invalid HWND shouldn't fire event
            service.SetTopmost(new IntPtr(9999999), true);
            Assert.False(eventInvoked);
        }
    }
}
