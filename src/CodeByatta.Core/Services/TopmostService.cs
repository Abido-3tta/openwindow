using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CodeByatta.Core.Interop;

namespace CodeByatta.Core.Services
{
    public class TopmostService
    {
        private readonly ConcurrentDictionary<IntPtr, bool> _pinnedWindows = new();

        public event EventHandler<(IntPtr Hwnd, bool IsTopmost)>? TopmostStateChanged;

        public IReadOnlyCollection<IntPtr> PinnedWindows => _pinnedWindows.Keys as IReadOnlyCollection<IntPtr> ?? new List<IntPtr>(_pinnedWindows.Keys);

        public bool IsTopmost(IntPtr hwnd)
        {
            if (!User32.IsWindow(hwnd))
            {
                _pinnedWindows.TryRemove(hwnd, out _);
                return false;
            }

            IntPtr exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            return ((long)exStyle & User32.WS_EX_TOPMOST) != 0;
        }

        public bool SetTopmost(IntPtr hwnd, bool enable)
        {
            if (!User32.IsWindow(hwnd))
            {
                _pinnedWindows.TryRemove(hwnd, out _);
                return false;
            }

            if (enable && ShellSanitizer.IsShellOrTaskbarWindow(hwnd))
            {
                return false;
            }

            IntPtr insertAfter = enable ? User32.HWND_TOPMOST : User32.HWND_NOTOPMOST;
            uint flags = User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW;

            bool success = User32.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, flags);
            if (success)
            {
                if (enable)
                {
                    _pinnedWindows[hwnd] = true;
                    // Ensure the Windows Taskbar stays on top of user topmost windows
                    ShellSanitizer.EnsureTaskbarOnTop();
                }
                else
                {
                    _pinnedWindows.TryRemove(hwnd, out _);
                }

                TopmostStateChanged?.Invoke(this, (hwnd, enable));
            }

            return success;
        }

        public bool ToggleTopmost(IntPtr hwnd)
        {
            bool currentState = IsTopmost(hwnd);
            return SetTopmost(hwnd, !currentState);
        }

        public void EnforcePinnedWindows()
        {
            bool hasPinned = false;
            foreach (var hwnd in _pinnedWindows.Keys)
            {
                if (User32.IsWindow(hwnd))
                {
                    hasPinned = true;
                    if (!IsTopmost(hwnd))
                    {
                        User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0,
                            User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
                    }
                }
                else
                {
                    _pinnedWindows.TryRemove(hwnd, out _);
                }
            }

            if (hasPinned)
            {
                ShellSanitizer.EnsureTaskbarOnTop();
            }
        }
    }
}
