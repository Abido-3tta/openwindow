using System;
using CodeByatta.Core.Interop;

namespace CodeByatta.UI.Views
{
    public class DarkModeOverlayWindow : IDisposable
    {
        public IntPtr TargetHwnd { get; }
        public IntPtr HwndHost { get; private set; }
        public IntPtr HwndMag { get; private set; }
        public bool IsVisible { get; private set; }
        
        private bool _disposed;
        private RECT _lastBounds;
        private long _lastUpdateTick;

        public DarkModeOverlayWindow(IntPtr targetHwnd, IntPtr? excludeHwnd = null)
        {
            TargetHwnd = targetHwnd;
            Initialize(excludeHwnd);
        }

        private void Initialize(IntPtr? excludeHwnd)
        {
            if (!User32.IsWindow(TargetHwnd)) return;

            // Get target window bounds
            if (!DwmApi.TryGetExtendedFrameBounds(TargetHwnd, out RECT bounds) &&
                !User32.GetWindowRect(TargetHwnd, out bounds))
            {
                bounds = new RECT(0, 0, 800, 600);
            }

            int w = Math.Max(10, bounds.Width);
            int h = Math.Max(10, bounds.Height);
            _lastBounds = bounds;

            // Host window: owned by TargetHwnd (tracks Z-order and minimize/restore automatically)
            uint exStyle = User32.WS_EX_LAYERED | User32.WS_EX_TRANSPARENT | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE;
            uint style = 0x80000000 | 0x10000000; // WS_POPUP | WS_VISIBLE

            HwndHost = User32.CreateWindowEx(
                exStyle,
                "Static",
                "OpenWindow_DarkModeOverlay",
                style,
                bounds.Left,
                bounds.Top,
                w,
                h,
                TargetHwnd,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (HwndHost == IntPtr.Zero) return;

            // Child magnifier window
            uint magStyle = 0x40000000 | 0x10000000; // WS_CHILD | WS_VISIBLE
            HwndMag = User32.CreateWindowEx(
                0,
                MagnificationApi.WC_MAGNIFIER,
                "MagChild",
                magStyle,
                0,
                0,
                w,
                h,
                HwndHost,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (HwndMag == IntPtr.Zero)
            {
                User32.DestroyWindow(HwndHost);
                HwndHost = IntPtr.Zero;
                return;
            }

            // Also set WS_EX_TRANSPARENT on child so all clicks penetrate
            IntPtr magEx = User32.GetWindowLongPtr(HwndMag, User32.GWL_EXSTYLE);
            long newMagEx = (long)magEx | User32.WS_EX_TRANSPARENT | User32.WS_EX_LAYERED;
            User32.SetWindowLongPtr(HwndMag, User32.GWL_EXSTYLE, (IntPtr)newMagEx);

            // Configure magnification: 1.0x transform (no zoom)
            var transform = MAGTRANSFORM.Identity;
            MagnificationApi.MagSetWindowTransform(HwndMag, ref transform);

            // Configure color inversion
            var effect = MagnificationApi.InvertedEffect;
            MagnificationApi.MagSetColorEffect(HwndMag, ref effect);

            // Exclude host overlay itself (and toolbar if provided) so they aren't captured recursively
            var excludeList = excludeHwnd.HasValue && excludeHwnd.Value != IntPtr.Zero
                ? new[] { HwndHost, excludeHwnd.Value }
                : new[] { HwndHost };
            MagnificationApi.MagSetWindowFilterList(HwndMag, MagnificationApi.MW_FILTERMODE_EXCLUDE, excludeList.Length, excludeList);

            // Set initial source rectangle
            MagnificationApi.MagSetWindowSource(HwndMag, bounds);
            IsVisible = true;
        }

        public void SetVisible(bool visible)
        {
            if (_disposed || HwndHost == IntPtr.Zero) return;

            if (visible)
            {
                UpdateBounds(force: true);
                User32.ShowWindow(HwndHost, User32.SW_SHOWNOACTIVATE);
                IsVisible = true;
            }
            else
            {
                User32.ShowWindow(HwndHost, User32.SW_HIDE);
                IsVisible = false;
            }
        }

        public void UpdateBounds(bool force = false)
        {
            if (_disposed || HwndHost == IntPtr.Zero) return;

            if (!User32.IsWindow(TargetHwnd) || User32.IsIconic(TargetHwnd))
            {
                if (IsVisible)
                {
                    User32.ShowWindow(HwndHost, User32.SW_HIDE);
                    IsVisible = false;
                }
                return;
            }

            // VSync throttle: Avoid calling expensive DWM/Magnification set source more than once per 16ms
            long now = Environment.TickCount64;
            if (!force && (now - _lastUpdateTick < 16))
            {
                return;
            }

            if (DwmApi.TryGetExtendedFrameBounds(TargetHwnd, out RECT bounds) ||
                User32.GetWindowRect(TargetHwnd, out bounds))
            {
                if (bounds.Left < -10000 || bounds.Width <= 10 || bounds.Height <= 10)
                {
                    if (IsVisible)
                    {
                        User32.ShowWindow(HwndHost, User32.SW_HIDE);
                        IsVisible = false;
                    }
                    return;
                }

                // Check if bounds actually changed
                bool boundsChanged = bounds.Left != _lastBounds.Left ||
                                     bounds.Top != _lastBounds.Top ||
                                     bounds.Width != _lastBounds.Width ||
                                     bounds.Height != _lastBounds.Height;

                if (!boundsChanged && !force)
                {
                    return;
                }

                _lastBounds = bounds;
                _lastUpdateTick = now;

                User32.SetWindowPos(
                    HwndHost,
                    IntPtr.Zero,
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    bounds.Height,
                    User32.SWP_NOACTIVATE | User32.SWP_NOZORDER | User32.SWP_SHOWWINDOW);

                User32.SetWindowPos(
                    HwndMag,
                    IntPtr.Zero,
                    0,
                    0,
                    bounds.Width,
                    bounds.Height,
                    User32.SWP_NOACTIVATE | User32.SWP_NOZORDER | User32.SWP_SHOWWINDOW);

                MagnificationApi.MagSetWindowSource(HwndMag, bounds);
                IsVisible = true;
            }
            else
            {
                if (IsVisible)
                {
                    User32.ShowWindow(HwndHost, User32.SW_HIDE);
                    IsVisible = false;
                }
            }
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsVisible = false;

            if (HwndMag != IntPtr.Zero)
            {
                try { User32.DestroyWindow(HwndMag); } catch { }
                HwndMag = IntPtr.Zero;
            }

            if (HwndHost != IntPtr.Zero)
            {
                try { User32.DestroyWindow(HwndHost); } catch { }
                HwndHost = IntPtr.Zero;
            }
        }
    }
}
