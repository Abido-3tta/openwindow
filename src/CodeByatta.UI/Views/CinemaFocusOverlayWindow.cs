using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CodeByatta.Core.Interop;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace CodeByatta.UI.Views
{
    public class CinemaFocusOverlayWindow : Window
    {
        public IntPtr CurrentTargetHwnd { get; private set; } = IntPtr.Zero;
        private double _dimLevel = 0.65;

        public CinemaFocusOverlayWindow(double dimLevel = 0.65)
        {
            _dimLevel = Math.Clamp(dimLevel, 0.20, 0.90);

            Title = "OpenWindow_CinemaFocusOverlay";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(MediaColor.FromArgb((byte)Math.Round(_dimLevel * 255), 0, 0, 0));
            Topmost = false;
            ShowInTaskbar = false;
            ShowActivated = false;
            IsHitTestVisible = false;
            ResizeMode = ResizeMode.NoResize;

            SourceInitialized += CinemaFocusOverlayWindow_SourceInitialized;
            UpdateFullscreenBounds();
        }

        private void CinemaFocusOverlayWindow_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            long exStyle = (long)User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            long newStyle = exStyle | User32.WS_EX_TRANSPARENT | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW;
            User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)newStyle);
        }

        public void SetDimLevel(double dimLevel)
        {
            _dimLevel = Math.Clamp(dimLevel, 0.20, 0.90);
            Background = new SolidColorBrush(MediaColor.FromArgb((byte)Math.Round(_dimLevel * 255), 0, 0, 0));
        }

        public void UpdateFullscreenBounds()
        {
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }

        public void AttachBehindWindow(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero || !User32.IsWindow(targetHwnd))
            {
                CurrentTargetHwnd = IntPtr.Zero;
                Hide();
                return;
            }

            CurrentTargetHwnd = targetHwnd;
            UpdateFullscreenBounds();

            if (!IsVisible) Show();

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // Place this fullscreen dimming overlay DIRECTLY BEHIND targetHwnd in Z-order
                // In Win32, SetWindowPos with hWndInsertAfter = targetHwnd puts this window right after targetHwnd!
                User32.SetWindowPos(hwnd, targetHwnd, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
            }
        }
    }
}
