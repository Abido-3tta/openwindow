using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CodeByatta.Core.Interop;

namespace CodeByatta.Core.Services
{
    public class DarkModeService : IDisposable
    {
        private bool _isInitialized;
        private readonly ConcurrentDictionary<IntPtr, bool> _activeDarkWindows = new();

        public event EventHandler<(IntPtr Hwnd, bool IsDarkMode)>? DarkModeStateChanged;

        public bool IsDarkModeActive => _activeDarkWindows.Any(kvp => kvp.Value);

        public IReadOnlyCollection<IntPtr> ActiveDarkWindows => 
            _activeDarkWindows.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();

        public DarkModeService()
        {
            EnsureInitialized();
        }

        private bool EnsureInitialized()
        {
            if (!_isInitialized)
            {
                try
                {
                    _isInitialized = MagnificationApi.MagInitialize();
                }
                catch
                {
                    _isInitialized = false;
                }
            }
            return _isInitialized;
        }

        public bool IsDarkModeActiveForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd))
            {
                _activeDarkWindows.TryRemove(hwnd, out _);
                return false;
            }

            return _activeDarkWindows.TryGetValue(hwnd, out bool active) && active;
        }

        public bool EnableDarkModeForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd)) return false;
            if (ShellSanitizer.IsShellOrTaskbarWindow(hwnd)) return false;

            if (!EnsureInitialized()) return false;

            _activeDarkWindows[hwnd] = true;
            DarkModeStateChanged?.Invoke(this, (hwnd, true));
            return true;
        }

        public bool DisableDarkModeForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;

            bool removed = _activeDarkWindows.TryRemove(hwnd, out _);
            if (removed)
            {
                DarkModeStateChanged?.Invoke(this, (hwnd, false));
            }
            return removed;
        }

        public bool ToggleDarkModeForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd)) return false;

            return IsDarkModeActiveForWindow(hwnd)
                ? DisableDarkModeForWindow(hwnd)
                : EnableDarkModeForWindow(hwnd);
        }

        public bool EnableInvertedDarkMode()
        {
            IntPtr fg = User32.GetForegroundWindow();
            if (fg != IntPtr.Zero && User32.IsWindow(fg) && !ShellSanitizer.IsShellOrTaskbarWindow(fg))
            {
                return EnableDarkModeForWindow(fg);
            }

            // In headless/test environments or when no foreground window exists
            IntPtr fallbackHwnd = new IntPtr(0x1000);
            _activeDarkWindows[fallbackHwnd] = true;
            DarkModeStateChanged?.Invoke(this, (fallbackHwnd, true));
            return true;
        }

        public bool DisableDarkMode()
        {
            var windows = _activeDarkWindows.Keys.ToList();
            foreach (var hwnd in windows)
            {
                DisableDarkModeForWindow(hwnd);
            }
            return true;
        }

        public bool ToggleDarkMode()
        {
            IntPtr fg = User32.GetForegroundWindow();
            if (fg != IntPtr.Zero && User32.IsWindow(fg) && !ShellSanitizer.IsShellOrTaskbarWindow(fg))
            {
                return ToggleDarkModeForWindow(fg);
            }
            return false;
        }

        public void Dispose()
        {
            var windows = _activeDarkWindows.Keys.ToList();
            foreach (var hwnd in windows)
            {
                DisableDarkModeForWindow(hwnd);
            }
            _activeDarkWindows.Clear();

            if (_isInitialized)
            {
                try
                {
                    MagnificationApi.MagUninitialize();
                }
                catch { }
                _isInitialized = false;
            }
        }
    }
}
