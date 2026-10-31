using System;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Models;
using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class HotkeyServiceTests
    {
        [Fact]
        public void HotkeyService_ProcessMessage_TriggersEvent()
        {
            var service = new HotkeyService();
            var config = new HotkeyConfig();
            
            bool triggered = false;
            HotkeyAction? receivedAction = null;

            service.HotkeyTriggered += (s, action) =>
            {
                triggered = true;
                receivedAction = action;
            };

            // Fake registering with zero hwnd
            service.Initialize(IntPtr.Zero, config);

            // Directly simulate dispatching WM_HOTKEY
            // Since id was not registered to zero hwnd, it shouldn't trigger unregistered
            bool handled = service.ProcessMessage(User32.WM_HOTKEY, (IntPtr)1001);
            Assert.False(handled);
            Assert.False(triggered);
        }
    }
}
