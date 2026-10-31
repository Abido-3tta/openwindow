using System;
using System.Collections.Concurrent;
using CodeByatta.Core.Interop;

namespace CodeByatta.Core.Services
{
    public class OpacityService
    {
        private readonly ConcurrentDictionary<IntPtr, byte> _previousOpacities = new();
        private readonly ConcurrentDictionary<IntPtr, byte> _configuredOpacities = new();
        private readonly ConcurrentDictionary<IntPtr, bool> _temporarilyBoostedWindows = new();
        private readonly ConcurrentDictionary<IntPtr, bool> _activeClickThroughWindows = new();

        public event EventHandler<(IntPtr Hwnd, byte Opacity)>? OpacityChanged;

        public IReadOnlyCollection<IntPtr> ActiveClickThroughWindows
        {
            get
            {
                PruneDeadWindows();
                return _activeClickThroughWindows.Keys as IReadOnlyCollection<IntPtr> ?? new List<IntPtr>(_activeClickThroughWindows.Keys);
            }
        }

        public IReadOnlyCollection<IntPtr> ActiveTransparentWindows
        {
            get
            {
                PruneDeadWindows();
                return _configuredOpacities.Keys as IReadOnlyCollection<IntPtr> ?? new List<IntPtr>(_configuredOpacities.Keys);
            }
        }

        public void PruneDeadWindows()
        {
            foreach (var hwnd in _activeClickThroughWindows.Keys)
            {
                if (!User32.IsWindow(hwnd))
                {
                    _activeClickThroughWindows.TryRemove(hwnd, out _);
                }
            }

            foreach (var hwnd in _configuredOpacities.Keys)
            {
                if (!User32.IsWindow(hwnd))
                {
                    _configuredOpacities.TryRemove(hwnd, out _);
                    _temporarilyBoostedWindows.TryRemove(hwnd, out _);
                    _previousOpacities.TryRemove(hwnd, out _);
                }
            }
        }

        public byte GetConfiguredOpacity(IntPtr hwnd)
        {
            if (_configuredOpacities.TryGetValue(hwnd, out byte alpha))
            {
                return alpha;
            }
            return 255;
        }

        public bool IsTemporarilyBoosted(IntPtr hwnd)
        {
            return _temporarilyBoostedWindows.ContainsKey(hwnd);
        }

        public bool SetTemporaryHoverReveal(IntPtr hwnd, bool reveal)
        {
            if (!User32.IsWindow(hwnd)) return false;
            if (!_configuredOpacities.TryGetValue(hwnd, out byte baseAlpha) || baseAlpha >= 255) return false;

            if (reveal)
            {
                if (_temporarilyBoostedWindows.ContainsKey(hwnd)) return true;

                // Ensure WS_EX_LAYERED is present
                IntPtr style = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
                long newStyle = (long)style;
                if ((newStyle & User32.WS_EX_LAYERED) == 0)
                {
                    newStyle |= User32.WS_EX_LAYERED;
                    User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)newStyle);
                }

                bool ok = User32.SetLayeredWindowAttributes(hwnd, 0, 255, User32.LWA_ALPHA);
                if (ok)
                {
                    _temporarilyBoostedWindows[hwnd] = true;
                    OpacityChanged?.Invoke(this, (hwnd, 255));
                }
                return ok;
            }
            else
            {
                if (!_temporarilyBoostedWindows.ContainsKey(hwnd)) return true;

                bool ok = User32.SetLayeredWindowAttributes(hwnd, 0, baseAlpha, User32.LWA_ALPHA);
                _temporarilyBoostedWindows.TryRemove(hwnd, out _);
                if (ok)
                {
                    OpacityChanged?.Invoke(this, (hwnd, baseAlpha));
                }
                return ok;
            }
        }

        public bool SetOpacity(IntPtr hwnd, byte alpha)
        {
            if (!User32.IsWindow(hwnd)) return false;
            if (ShellSanitizer.IsShellOrTaskbarOrSelf(hwnd)) return false;

            IntPtr style = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            long newStyle = (long)style;

            if (alpha == 255)
            {
                _configuredOpacities.TryRemove(hwnd, out _);
                _temporarilyBoostedWindows.TryRemove(hwnd, out _);

                // If opacity is 100% and not click-through, we can clear WS_EX_LAYERED
                bool isClickThrough = (newStyle & User32.WS_EX_TRANSPARENT) != 0;
                if (!isClickThrough)
                {
                    newStyle &= ~User32.WS_EX_LAYERED;
                    User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)newStyle);
                    User32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                        User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_FRAMECHANGED);
                    _previousOpacities.TryRemove(hwnd, out _);
                    OpacityChanged?.Invoke(this, (hwnd, 255));
                    return true;
                }
            }
            else
            {
                _configuredOpacities[hwnd] = alpha;
                _temporarilyBoostedWindows.TryRemove(hwnd, out _);
            }

            if ((newStyle & User32.WS_EX_LAYERED) == 0)
            {
                newStyle |= User32.WS_EX_LAYERED;
                User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)newStyle);
            }

            bool success = User32.SetLayeredWindowAttributes(hwnd, 0, alpha, User32.LWA_ALPHA);
            if (success)
            {
                _previousOpacities[hwnd] = alpha;
            }
            OpacityChanged?.Invoke(this, (hwnd, alpha));
            return success;
        }

        public bool ToggleTransparency(IntPtr hwnd, byte targetAlpha = 200)
        {
            if (!User32.IsWindow(hwnd)) return false;

            if (_previousOpacities.TryGetValue(hwnd, out byte current) && current < 255)
            {
                return SetOpacity(hwnd, 255);
            }
            return SetOpacity(hwnd, targetAlpha);
        }

        public bool IsClickThrough(IntPtr hwnd)
        {
            if (!User32.IsWindow(hwnd)) return false;
            IntPtr style = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            return ((long)style & User32.WS_EX_TRANSPARENT) != 0;
        }

        public bool SetClickThrough(IntPtr hwnd, bool enable)
        {
            if (!User32.IsWindow(hwnd)) return false;
            if (ShellSanitizer.IsShellOrTaskbarOrSelf(hwnd)) return false;

            IntPtr style = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            long newStyle = (long)style;

            if (enable)
            {
                newStyle |= User32.WS_EX_LAYERED | User32.WS_EX_TRANSPARENT;
                _activeClickThroughWindows[hwnd] = true;
            }
            else
            {
                newStyle &= ~User32.WS_EX_TRANSPARENT;
                _activeClickThroughWindows.TryRemove(hwnd, out _);
            }

            User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)newStyle);
            User32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_FRAMECHANGED);

            return true;
        }

        public bool SetHeaderInteractive(IntPtr hwnd, bool isInteractive)
        {
            if (!User32.IsWindow(hwnd)) return false;

            IntPtr style = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            long current = (long)style;
            long target = isInteractive ? (current & ~User32.WS_EX_TRANSPARENT) : (current | User32.WS_EX_TRANSPARENT);

            if (current != target)
            {
                User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)target);
                return true;
            }
            return false;
        }

        public bool ToggleClickThrough(IntPtr hwnd)
        {
            bool current = IsClickThrough(hwnd);
            return SetClickThrough(hwnd, !current);
        }

        public bool ResetWindow(IntPtr hwnd)
        {
            if (!User32.IsWindow(hwnd)) return false;

            IntPtr style = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            long newStyle = (long)style & ~(User32.WS_EX_LAYERED | User32.WS_EX_TRANSPARENT);

            User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)newStyle);
            User32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_FRAMECHANGED);

            _previousOpacities.TryRemove(hwnd, out _);
            _configuredOpacities.TryRemove(hwnd, out _);
            _temporarilyBoostedWindows.TryRemove(hwnd, out _);
            _activeClickThroughWindows.TryRemove(hwnd, out _);
            return true;
        }

        public void RestoreAllWindows()
        {
            var ctHwnds = new List<IntPtr>(_activeClickThroughWindows.Keys);
            foreach (var hwnd in ctHwnds)
            {
                SetClickThrough(hwnd, false);
            }

            var opHwnds = new List<IntPtr>(_configuredOpacities.Keys);
            foreach (var hwnd in opHwnds)
            {
                SetOpacity(hwnd, 255);
            }

            var boostHwnds = new List<IntPtr>(_temporarilyBoostedWindows.Keys);
            foreach (var hwnd in boostHwnds)
            {
                SetTemporaryHoverReveal(hwnd, false);
            }

            _activeClickThroughWindows.Clear();
            _configuredOpacities.Clear();
            _temporarilyBoostedWindows.Clear();
        }
    }
}
