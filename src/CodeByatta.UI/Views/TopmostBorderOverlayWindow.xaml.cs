using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CodeByatta.Core.Interop;
using MediaBrushes = System.Windows.Media.Brushes;

namespace CodeByatta.UI.Views
{
    public partial class TopmostBorderOverlayWindow : Window
    {
        public IntPtr TargetHwnd { get; }

        public TopmostBorderOverlayWindow(IntPtr targetHwnd, string colorHex = "#FF0000", double borderWidth = 3.0)
        {
            InitializeComponent();
            TargetHwnd = targetHwnd;

            ApplyStyle(colorHex, borderWidth);

            SourceInitialized += TopmostBorderOverlayWindow_SourceInitialized;
        }

        public void ApplyStyle(string colorHex, double borderWidth)
        {
            try
            {
                var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex)!;
                OverlayBorder.BorderBrush = brush;
            }
            catch
            {
                OverlayBorder.BorderBrush = MediaBrushes.Red;
            }
            OverlayBorder.BorderThickness = new Thickness(borderWidth);
        }

        private void TopmostBorderOverlayWindow_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            long newStyle = (long)exStyle | User32.WS_EX_TRANSPARENT | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW;
            User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)newStyle);

            UpdateBounds();
        }

        public void BringBorderToTop()
        {
            IntPtr targetRoot = User32.GetAncestor(TargetHwnd, User32.GA_ROOT);
            IntPtr pipRoot = App.CurrentPipTargetHwnd != IntPtr.Zero ? User32.GetAncestor(App.CurrentPipTargetHwnd, User32.GA_ROOT) : IntPtr.Zero;
            if (pipRoot != IntPtr.Zero && (TargetHwnd == App.CurrentPipTargetHwnd || (targetRoot != IntPtr.Zero && targetRoot == pipRoot)))
            {
                Hide();
                return;
            }

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero && User32.IsWindow(hwnd))
            {
                User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
                User32.BringWindowToTop(hwnd);
            }
        }

        public void UpdateBounds()
        {
            IntPtr targetRoot = User32.GetAncestor(TargetHwnd, User32.GA_ROOT);
            IntPtr pipRoot = App.CurrentPipTargetHwnd != IntPtr.Zero ? User32.GetAncestor(App.CurrentPipTargetHwnd, User32.GA_ROOT) : IntPtr.Zero;

            if (!User32.IsWindow(TargetHwnd) || User32.IsIconic(TargetHwnd) || (pipRoot != IntPtr.Zero && (TargetHwnd == App.CurrentPipTargetHwnd || (targetRoot != IntPtr.Zero && targetRoot == pipRoot))))
            {
                Hide();
                return;
            }

            if (DwmApi.TryGetExtendedFrameBounds(TargetHwnd, out RECT bounds) || User32.GetWindowRect(TargetHwnd, out bounds))
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                Left = bounds.Left / dpi.DpiScaleX;
                Top = bounds.Top / dpi.DpiScaleY;
                Width = Math.Max(10, bounds.Width / dpi.DpiScaleX);
                Height = Math.Max(10, bounds.Height / dpi.DpiScaleY);

                if (!IsVisible) Show();
            }
            else
            {
                Hide();
            }
        }
    }
}
