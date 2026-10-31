using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Models;
using CodeByatta.Core.Services;
using WpfPoint = System.Windows.Point;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfMouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using MouseButton = System.Windows.Input.MouseButton;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using Cursors = System.Windows.Input.Cursors;
using MediaColor = System.Windows.Media.Color;

namespace CodeByatta.UI.Views
{
    public partial class PipWindow : Window
    {
        private readonly ManagedWindow _targetWindow;
        private readonly PipManagerService _pipManager;
        private readonly OpacityService? _opacityService;
        private readonly AppConfig? _config;
        private readonly RECT? _cropRect;
        private readonly bool _hideOriginalWindow;
        private readonly bool _isTopmost;
        private readonly string? _borderColor;
        private readonly double _borderWidth;
        private readonly double _aspectRatio;

        private IntPtr _hThumbnail = IntPtr.Zero;
        private bool _isTransparent = false;
        private bool _isClosing = false;
        private bool _isInteractMode = true;

        // Ghost Mode state (keeps DWM live, browsers rendering at 60fps, automation uninhibited)
        private bool _originalWindowGhosted = false;
        private long _originalExStyle = 0;

        private const int WM_SIZING = 0x0214;
        private const int WMSZ_LEFT = 1;
        private const int WMSZ_RIGHT = 2;
        private const int WMSZ_TOP = 3;
        private const int WMSZ_TOPLEFT = 4;
        private const int WMSZ_TOPRIGHT = 5;
        private const int WMSZ_BOTTOM = 6;
        private const int WMSZ_BOTTOMLEFT = 7;
        private const int WMSZ_BOTTOMRIGHT = 8;

        public IntPtr TargetHwnd => _targetWindow.Hwnd;
        public event EventHandler? PipClosed;

        public PipWindow(
            ManagedWindow targetWindow,
            PipManagerService pipManager,
            RECT? cropRect = null,
            bool hideOriginalWindow = false)
            : this(targetWindow, pipManager, null, null, cropRect, hideOriginalWindow, false, null, 2.0)
        {
        }

        public PipWindow(
            ManagedWindow targetWindow,
            PipManagerService pipManager,
            OpacityService? opacityService,
            AppConfig? config,
            RECT? cropRect = null,
            bool hideOriginalWindow = false,
            bool isTopmost = false,
            string? borderColor = null,
            double borderWidth = 2.0)
        {
            InitializeComponent();
            _targetWindow = targetWindow;
            _pipManager = pipManager;
            _opacityService = opacityService;
            _config = config;
            _cropRect = cropRect;
            _hideOriginalWindow = hideOriginalWindow;
            _isTopmost = isTopmost;
            _borderColor = borderColor;
            _borderWidth = borderWidth;

            // Calculate source aspect ratio
            int sourceW = 0, sourceH = 0;
            if (_cropRect.HasValue && _cropRect.Value.Width > 0 && _cropRect.Value.Height > 0)
            {
                sourceW = _cropRect.Value.Width;
                sourceH = _cropRect.Value.Height;
            }
            else if (_targetWindow.GetBounds(out var tb) && tb.Width > 0 && tb.Height > 0)
            {
                sourceW = tb.Width;
                sourceH = tb.Height;
            }

            _aspectRatio = (sourceW > 0 && sourceH > 0) ? ((double)sourceW / sourceH) : (16.0 / 9.0);

            // Compute proportional initial size maintaining source aspect ratio
            double initW = 420;
            double clientH = initW / _aspectRatio;
            double totalH = clientH + 34; // 34px header height
            Width = initW;
            Height = Math.Clamp(totalH, 150, 900);

            TxtTitle.Text = string.IsNullOrWhiteSpace(_targetWindow.Title)
                ? _targetWindow.ProcessName
                : $"{_targetWindow.ProcessName} - {_targetWindow.Title}";

            if (_cropRect.HasValue)
            {
                CroppedBadge.Visibility = Visibility.Visible;
            }

            if (_isTopmost && !string.IsNullOrEmpty(_borderColor))
            {
                try
                {
                    var col = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(_borderColor);
                    WindowOuterBorder.BorderBrush = new SolidColorBrush(col);
                    WindowOuterBorder.BorderThickness = new Thickness(Math.Clamp(_borderWidth, 1.5, 4.0));
                }
                catch { }
            }

            SourceInitialized += PipWindow_SourceInitialized;
            Closed += PipWindow_Closed;
        }

        private void PipWindow_SourceInitialized(object? sender, EventArgs e)
        {
            UpdateThumbnail();

            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WndProc);

            if (_hideOriginalWindow && _targetWindow.Hwnd != IntPtr.Zero && User32.IsWindow(_targetWindow.Hwnd))
            {
                // Apply Ghost Mode: Layered + Transparent with Alpha=1
                // Window is 99.6% invisible to human eyes, desktop clicks pass through it,
                // BUT DWM keeps rendering 100% live, browser tab timers do not suspend, and automation frameworks stay unblocked!
                _originalExStyle = (long)User32.GetWindowLongPtr(_targetWindow.Hwnd, User32.GWL_EXSTYLE);
                long ghostStyle = _originalExStyle | User32.WS_EX_LAYERED | User32.WS_EX_TRANSPARENT;
                User32.SetWindowLongPtr(_targetWindow.Hwnd, User32.GWL_EXSTYLE, (IntPtr)ghostStyle);
                User32.SetLayeredWindowAttributes(_targetWindow.Hwnd, 0, 1, User32.LWA_ALPHA);
                _originalWindowGhosted = true;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SIZING && _aspectRatio > 0.05)
            {
                try
                {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    double headerPx = 34.0 * dpi.DpiScaleY;
                    RECT rect = System.Runtime.InteropServices.Marshal.PtrToStructure<RECT>(lParam);
                    int edge = wParam.ToInt32();

                    int currentW = rect.Right - rect.Left;
                    int currentH = rect.Bottom - rect.Top;
                    double clientH = Math.Max(20.0, currentH - headerPx);

                    switch (edge)
                    {
                        case WMSZ_LEFT:
                        case WMSZ_RIGHT:
                        {
                            double newClientH = currentW / _aspectRatio;
                            rect.Bottom = rect.Top + (int)Math.Round(newClientH + headerPx);
                            break;
                        }
                        case WMSZ_TOP:
                        case WMSZ_BOTTOM:
                        {
                            double newClientW = clientH * _aspectRatio;
                            rect.Right = rect.Left + (int)Math.Round(newClientW);
                            break;
                        }
                        case WMSZ_BOTTOMRIGHT:
                        case WMSZ_BOTTOMLEFT:
                        {
                            double newClientH = currentW / _aspectRatio;
                            rect.Bottom = rect.Top + (int)Math.Round(newClientH + headerPx);
                            break;
                        }
                        case WMSZ_TOPRIGHT:
                        case WMSZ_TOPLEFT:
                        {
                            double newClientH = currentW / _aspectRatio;
                            rect.Top = rect.Bottom - (int)Math.Round(newClientH + headerPx);
                            break;
                        }
                    }

                    System.Runtime.InteropServices.Marshal.StructureToPtr(rect, lParam, true);
                    handled = true;
                    return (IntPtr)1;
                }
                catch { }
            }
            return IntPtr.Zero;
        }

        private void UpdateThumbnail()
        {
            IntPtr destHwnd = new WindowInteropHelper(this).Handle;
            if (destHwnd == IntPtr.Zero || _targetWindow.Hwnd == IntPtr.Zero) return;

            // Accurate physical pixel calculation for DWM composition across high-DPI screens
            var dpi = VisualTreeHelper.GetDpi(this);
            WpfPoint hostTopLeft = ThumbnailHost.TranslatePoint(new WpfPoint(0, 0), this);

            int destLeft = (int)Math.Round(hostTopLeft.X * dpi.DpiScaleX);
            int destTop = (int)Math.Round(hostTopLeft.Y * dpi.DpiScaleY);
            int destWidth = (int)Math.Round((ThumbnailHost.ActualWidth > 0 ? ThumbnailHost.ActualWidth : Width - 2) * dpi.DpiScaleX);
            int destHeight = (int)Math.Round((ThumbnailHost.ActualHeight > 0 ? ThumbnailHost.ActualHeight : Height - 36) * dpi.DpiScaleY);

            RECT destRect = new RECT(destLeft, destTop, destLeft + destWidth, destTop + destHeight);

            if (_hThumbnail == IntPtr.Zero)
            {
                _hThumbnail = _pipManager.RegisterPipThumbnail(destHwnd, _targetWindow.Hwnd, destRect, _cropRect);
            }
            else
            {
                _pipManager.UpdatePipBounds(_hThumbnail, destRect, _cropRect);
            }
        }

        private void ThumbnailHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateThumbnail();
        }

        private void Header_MouseDown(object sender, WpfMouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnInteract_Click(object sender, RoutedEventArgs e)
        {
            _isInteractMode = !_isInteractMode;

            if (_isInteractMode)
            {
                TxtInteract.Text = "🖱️ Control: ON";
                TxtInteract.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x30, 0xD1, 0x58));
                BtnInteract.Background = new SolidColorBrush(MediaColor.FromArgb(0x26, 0x30, 0xD1, 0x58));
                BtnInteract.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x40, 0x30, 0xD1, 0x58));
                ThumbnailHost.Cursor = Cursors.Cross;
            }
            else
            {
                TxtInteract.Text = "✋ Move: ON";
                TxtInteract.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xAA, 0xAA, 0xAA));
                BtnInteract.Background = new SolidColorBrush(MediaColor.FromArgb(0x26, 0x88, 0x88, 0x88));
                BtnInteract.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x40, 0x88, 0x88, 0x88));
                ThumbnailHost.Cursor = Cursors.SizeAll;
            }
        }

        private void BtnOpacityToggle_Click(object sender, RoutedEventArgs e)
        {
            _isTransparent = !_isTransparent;
            if (_isTransparent)
            {
                byte savedAlpha = _opacityService?.GetConfiguredOpacity(_targetWindow.Hwnd) ?? 255;
                if (savedAlpha == 255 && _config != null)
                {
                    savedAlpha = (byte)Math.Round(_config.DefaultOpacityPercent * 2.55);
                }
                double targetOpacity = Math.Clamp((double)savedAlpha / 255.0, 0.15, 0.90);
                Opacity = targetOpacity;
            }
            else
            {
                Opacity = 1.0;
            }
        }

        public void RestoreAndClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            RestoreOriginalWindow();
            Close();
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            RestoreAndClose();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            RestoreAndClose();
        }

        private void RestoreOriginalWindow()
        {
            if (_targetWindow.Hwnd == IntPtr.Zero || !User32.IsWindow(_targetWindow.Hwnd)) return;

            if (_originalWindowGhosted)
            {
                User32.SetWindowLongPtr(_targetWindow.Hwnd, User32.GWL_EXSTYLE, (IntPtr)_originalExStyle);
                User32.SetLayeredWindowAttributes(_targetWindow.Hwnd, 0, 255, User32.LWA_ALPHA);
                User32.SetWindowPos(_targetWindow.Hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_FRAMECHANGED);
                _originalWindowGhosted = false;
            }

            User32.ShowWindow(_targetWindow.Hwnd, User32.SW_SHOW);
            User32.ShowWindow(_targetWindow.Hwnd, User32.SW_RESTORE);
            User32.SetForegroundWindow(_targetWindow.Hwnd);
        }

        private void PipWindow_Closed(object? sender, EventArgs e)
        {
            RestoreOriginalWindow();

            if (_hThumbnail != IntPtr.Zero)
            {
                _pipManager.UnregisterPipThumbnail(_hThumbnail);
                _hThumbnail = IntPtr.Zero;
            }

            PipClosed?.Invoke(this, EventArgs.Empty);
        }

        #region Interactive Mouse Control & Message Translation

        private bool TranslatePipToTargetCoordinates(
            WpfPoint hostPoint,
            out IntPtr targetHwnd,
            out IntPtr lParam,
            out int screenX,
            out int screenY)
        {
            targetHwnd = _targetWindow.Hwnd;
            lParam = IntPtr.Zero;
            screenX = 0;
            screenY = 0;

            if (_targetWindow.Hwnd == IntPtr.Zero || !User32.IsWindow(_targetWindow.Hwnd)) return false;
            if (!_targetWindow.GetBounds(out RECT targetRect)) return false;

            double hostW = ThumbnailHost.ActualWidth;
            double hostH = ThumbnailHost.ActualHeight;
            if (hostW <= 0 || hostH <= 0) return false;

            double relX = Math.Clamp(hostPoint.X / hostW, 0.0, 1.0);
            double relY = Math.Clamp(hostPoint.Y / hostH, 0.0, 1.0);

            int targetLocalX;
            int targetLocalY;

            if (_cropRect.HasValue && _cropRect.Value.Width > 0 && _cropRect.Value.Height > 0)
            {
                targetLocalX = _cropRect.Value.Left + (int)Math.Round(relX * _cropRect.Value.Width);
                targetLocalY = _cropRect.Value.Top + (int)Math.Round(relY * _cropRect.Value.Height);
            }
            else
            {
                targetLocalX = (int)Math.Round(relX * targetRect.Width);
                targetLocalY = (int)Math.Round(relY * targetRect.Height);
            }

            screenX = targetRect.Left + targetLocalX;
            screenY = targetRect.Top + targetLocalY;

            // Route to specific inner child window or control if applicable (defensively guarded against screenshot hangs)
            var screenPoint = new User32.POINT(screenX, screenY);
            IntPtr candidateHwnd = IntPtr.Zero;
            try
            {
                candidateHwnd = User32.WindowFromPoint(screenPoint);
            }
            catch { }

            if (candidateHwnd != IntPtr.Zero && (candidateHwnd == _targetWindow.Hwnd || User32.GetAncestor(candidateHwnd, User32.GA_ROOT) == _targetWindow.Hwnd))
            {
                targetHwnd = candidateHwnd;
            }
            else
            {
                targetHwnd = _targetWindow.Hwnd;
            }

            var clientPoint = screenPoint;
            User32.ScreenToClient(targetHwnd, ref clientPoint);
            lParam = (IntPtr)(((clientPoint.Y & 0xFFFF) << 16) | (clientPoint.X & 0xFFFF));
            return true;
        }

        private void ThumbnailHost_MouseDown(object sender, WpfMouseButtonEventArgs e)
        {
            if (!_isInteractMode)
            {
                if (e.LeftButton == MouseButtonState.Pressed) DragMove();
                return;
            }

            try
            {
                WpfPoint pt = e.GetPosition(ThumbnailHost);
                if (TranslatePipToTargetCoordinates(pt, out IntPtr hwnd, out IntPtr lParam, out _, out _))
                {
                    uint msg = e.ChangedButton == MouseButton.Right ? (uint)User32.WM_RBUTTONDOWN : (uint)User32.WM_LBUTTONDOWN;
                    IntPtr wParam = e.ChangedButton == MouseButton.Right ? (IntPtr)User32.MK_RBUTTON : (IntPtr)User32.MK_LBUTTON;
                    User32.PostMessage(hwnd, msg, wParam, lParam);
                }
            }
            catch { }
        }

        private void ThumbnailHost_MouseUp(object sender, WpfMouseButtonEventArgs e)
        {
            if (!_isInteractMode) return;

            try
            {
                WpfPoint pt = e.GetPosition(ThumbnailHost);
                if (TranslatePipToTargetCoordinates(pt, out IntPtr hwnd, out IntPtr lParam, out _, out _))
                {
                    uint msg = e.ChangedButton == MouseButton.Right ? (uint)User32.WM_RBUTTONUP : (uint)User32.WM_LBUTTONUP;
                    User32.PostMessage(hwnd, msg, IntPtr.Zero, lParam);
                }
            }
            catch { }
        }

        private void ThumbnailHost_MouseMove(object sender, WpfMouseEventArgs e)
        {
            if (!_isInteractMode) return;

            try
            {
                WpfPoint pt = e.GetPosition(ThumbnailHost);
                if (TranslatePipToTargetCoordinates(pt, out IntPtr hwnd, out IntPtr lParam, out _, out _))
                {
                    IntPtr wParam = e.LeftButton == MouseButtonState.Pressed ? (IntPtr)User32.MK_LBUTTON : IntPtr.Zero;
                    User32.PostMessage(hwnd, (uint)User32.WM_MOUSEMOVE, wParam, lParam);
                }
            }
            catch { }
        }

        private void ThumbnailHost_MouseWheel(object sender, WpfMouseWheelEventArgs e)
        {
            if (!_isInteractMode) return;

            try
            {
                WpfPoint pt = e.GetPosition(ThumbnailHost);
                if (TranslatePipToTargetCoordinates(pt, out IntPtr hwnd, out _, out int screenX, out int screenY))
                {
                    short delta = (short)e.Delta;
                    IntPtr wParam = (IntPtr)((delta << 16) & unchecked((int)0xFFFF0000));
                    IntPtr screenLParam = (IntPtr)(((screenY & 0xFFFF) << 16) | (screenX & 0xFFFF));
                    User32.PostMessage(hwnd, (uint)User32.WM_MOUSEWHEEL, wParam, screenLParam);
                }
            }
            catch { }
            e.Handled = true;
        }

        #endregion
    }
}
