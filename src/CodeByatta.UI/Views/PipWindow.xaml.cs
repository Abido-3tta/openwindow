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
        private readonly byte _prePipOpacity;
        private readonly bool _prePipClickThrough;

        private IntPtr _hThumbnail = IntPtr.Zero;
        private bool _isClosing = false;
        private bool _isInteractMode = true;
        private bool _isAdjustingAspect = false;

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

            // Record authentic pre-PiP state to guarantee 100% clean restoration
            _prePipOpacity = _opacityService?.GetConfiguredOpacity(_targetWindow.Hwnd) ?? 255;
            _prePipClickThrough = _opacityService?.IsClickThroughAuthoritative(_targetWindow.Hwnd) ?? false;

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
                // Non-intrusive Ghost Mode:
                // Layer the target window and set alpha to 1 (virtually invisible)
                // AND send it to HWND_BOTTOM so desktop clicks fall naturally through to background windows!
                // CRITICAL SAFETY INVARIANT: Never add WS_EX_TRANSPARENT to the user's application,
                // because WS_EX_TRANSPARENT marks it as click-through and corrupts state across screenshots and tools!
                _originalExStyle = (long)User32.GetWindowLongPtr(_targetWindow.Hwnd, User32.GWL_EXSTYLE);
                long ghostStyle = _originalExStyle | User32.WS_EX_LAYERED;
                User32.SetWindowLongPtr(_targetWindow.Hwnd, User32.GWL_EXSTYLE, (IntPtr)ghostStyle);
                User32.SetLayeredWindowAttributes(_targetWindow.Hwnd, 0, 1, User32.LWA_ALPHA);
                User32.SetWindowPos(_targetWindow.Hwnd, User32.HWND_BOTTOM, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
                _originalWindowGhosted = true;
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (_isAdjustingAspect || _aspectRatio <= 0.05) return;

            // Deterministic Single-Driver Aspect Ratio Enforcement:
            // Master dimension is ALWAYS Width. Height is strictly derived from client width and source aspect ratio.
            // Never alternate between setting Width and Height to prevent recursive re-entrant layout freezes!
            double headerHeight = 34.0;
            double clientW = Math.Max(20.0, ActualWidth - 2.0);
            double expectedClientH = clientW / _aspectRatio;
            double targetH = Math.Clamp(expectedClientH + headerHeight, MinHeight, SystemParameters.PrimaryScreenHeight * 0.9);

            if (Math.Abs(ActualHeight - targetH) > 2.0)
            {
                _isAdjustingAspect = true;
                try
                {
                    Height = targetH;
                }
                finally
                {
                    _isAdjustingAspect = false;
                }
            }
        }

        private long _lastMouseMoveTick = 0;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == User32.WM_NCHITTEST)
            {
                try
                {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    int screenX = unchecked((short)(lParam.ToInt32() & 0xFFFF));
                    int screenY = unchecked((short)((lParam.ToInt32() >> 16) & 0xFFFF));

                    double winLeft = Left * dpi.DpiScaleX;
                    double winTop = Top * dpi.DpiScaleY;
                    double winRight = winLeft + (ActualWidth * dpi.DpiScaleX);
                    double winBottom = winTop + (ActualHeight * dpi.DpiScaleY);

                    // Allow direct interaction with header action buttons (BtnInteract, BtnRestore, BtnClose)
                    // The header is 34px high and the buttons occupy the rightmost 190px of the header
                    double headerHeightPx = 34.0 * dpi.DpiScaleY;
                    double headerButtonsLeftPx = winRight - (190.0 * dpi.DpiScaleX);
                    if (screenY >= winTop && screenY <= winTop + headerHeightPx && screenX >= headerButtonsLeftPx)
                    {
                        // Pass hit directly to WPF visual tree for button clicks without resize interference!
                        return IntPtr.Zero;
                    }

                    const int borderThickness = 9;

                    bool isLeft = screenX >= winLeft && screenX <= winLeft + borderThickness;
                    bool isRight = screenX >= winRight - borderThickness && screenX <= winRight;
                    bool isTop = screenY >= winTop && screenY <= winTop + borderThickness;
                    bool isBottom = screenY >= winBottom - borderThickness && screenY <= winBottom;

                    if (isTop && isLeft) { handled = true; return (IntPtr)User32.HTTOPLEFT; }
                    if (isTop && isRight) { handled = true; return (IntPtr)User32.HTTOPRIGHT; }
                    if (isBottom && isLeft) { handled = true; return (IntPtr)User32.HTBOTTOMLEFT; }
                    if (isBottom && isRight) { handled = true; return (IntPtr)User32.HTBOTTOMRIGHT; }
                    if (isLeft) { handled = true; return (IntPtr)User32.HTLEFT; }
                    if (isRight) { handled = true; return (IntPtr)User32.HTRIGHT; }
                    if (isTop) { handled = true; return (IntPtr)User32.HTTOP; }
                    if (isBottom) { handled = true; return (IntPtr)User32.HTBOTTOM; }
                }
                catch { }

                return IntPtr.Zero;
            }
            else if (msg == User32.WM_SIZING && _aspectRatio > 0.05)
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
                        case User32.HTLEFT:
                        case User32.HTRIGHT:
                        case 1: // WMSZ_LEFT
                        case 2: // WMSZ_RIGHT
                        {
                            double newClientH = currentW / _aspectRatio;
                            rect.Bottom = rect.Top + (int)Math.Round(newClientH + headerPx);
                            break;
                        }
                        case User32.HTTOP:
                        case User32.HTBOTTOM:
                        case 3: // WMSZ_TOP
                        case 6: // WMSZ_BOTTOM
                        {
                            double newClientW = clientH * _aspectRatio;
                            rect.Right = rect.Left + (int)Math.Round(newClientW);
                            break;
                        }
                        case User32.HTBOTTOMRIGHT:
                        case User32.HTBOTTOMLEFT:
                        case 7: // WMSZ_BOTTOMLEFT
                        case 8: // WMSZ_BOTTOMRIGHT
                        {
                            double newClientH = currentW / _aspectRatio;
                            rect.Bottom = rect.Top + (int)Math.Round(newClientH + headerPx);
                            break;
                        }
                        case User32.HTTOPRIGHT:
                        case User32.HTTOPLEFT:
                        case 4: // WMSZ_TOPLEFT
                        case 5: // WMSZ_TOPRIGHT
                        {
                            double newClientH = currentW / _aspectRatio;
                            rect.Top = rect.Bottom - (int)Math.Round(newClientH + headerPx);
                            break;
                        }
                    }

                    System.Runtime.InteropServices.Marshal.StructureToPtr(rect, lParam, false);
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
            if (e.ClickCount == 2)
            {
                RestoreAndClose();
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
                SnapToNearestCornerIfClose();
            }
        }

        private void Header_MouseWheel(object sender, WpfMouseWheelEventArgs e)
        {
            if (_config == null || !_config.EnablePipWheelResize) return;

            double step = e.Delta > 0 ? 30.0 : -30.0;
            double targetWidth = Math.Clamp(ActualWidth + step, MinWidth, SystemParameters.PrimaryScreenWidth * 0.85);

            Width = targetWidth;
            Height = Math.Clamp((targetWidth / _aspectRatio) + 34.0, MinHeight, SystemParameters.PrimaryScreenHeight * 0.9);
            SnapToNearestCornerIfClose();
            e.Handled = true;
        }

        private void SnapToNearestCornerIfClose()
        {
            if (_config == null || !_config.EnablePipCornerSnapping) return;

            double snapThreshold = 40.0;
            double margin = 14.0;

            double screenLeft = SystemParameters.WorkArea.Left;
            double screenTop = SystemParameters.WorkArea.Top;
            double screenRight = SystemParameters.WorkArea.Right;
            double screenBottom = SystemParameters.WorkArea.Bottom;

            double w = ActualWidth;
            double h = ActualHeight;

            double curLeft = Left;
            double curTop = Top;
            double curRight = curLeft + w;
            double curBottom = curTop + h;

            // 1. Top-Left Corner
            if (Math.Abs(curLeft - screenLeft) < snapThreshold && Math.Abs(curTop - screenTop) < snapThreshold)
            {
                Left = screenLeft + margin;
                Top = screenTop + margin;
                return;
            }

            // 2. Top-Right Corner
            if (Math.Abs(curRight - screenRight) < snapThreshold && Math.Abs(curTop - screenTop) < snapThreshold)
            {
                Left = screenRight - w - margin;
                Top = screenTop + margin;
                return;
            }

            // 3. Bottom-Left Corner
            if (Math.Abs(curLeft - screenLeft) < snapThreshold && Math.Abs(curBottom - screenBottom) < snapThreshold)
            {
                Left = screenLeft + margin;
                Top = screenBottom - h - margin;
                return;
            }

            // 4. Bottom-Right Corner
            if (Math.Abs(curRight - screenRight) < snapThreshold && Math.Abs(curBottom - screenBottom) < snapThreshold)
            {
                Left = screenRight - w - margin;
                Top = screenBottom - h - margin;
                return;
            }
        }

        private void BtnInteract_Click(object sender, RoutedEventArgs e)
        {
            _isInteractMode = !_isInteractMode;

            if (_isInteractMode)
            {
                TxtInteract.Text = "Control: ON";
                TxtInteract.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x30, 0xD1, 0x58));
                BtnInteract.Background = new SolidColorBrush(MediaColor.FromArgb(0x1E, 0x30, 0xD1, 0x58));
                BtnInteract.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x33, 0x30, 0xD1, 0x58));
                ThumbnailHost.Cursor = Cursors.Arrow;
            }
            else
            {
                TxtInteract.Text = "Move: ON";
                TxtInteract.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xAA, 0xAA, 0xAA));
                BtnInteract.Background = new SolidColorBrush(MediaColor.FromArgb(0x20, 0x88, 0x88, 0x88));
                BtnInteract.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x33, 0x88, 0x88, 0x88));
                ThumbnailHost.Cursor = Cursors.SizeAll;
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
                IntPtr currentEx = User32.GetWindowLongPtr(_targetWindow.Hwnd, User32.GWL_EXSTYLE);
                long newEx = (long)currentEx;

                // Enforce removal of click-through transparency unless it was genuinely active prior to PiP
                if (!_prePipClickThrough)
                {
                    newEx &= ~User32.WS_EX_TRANSPARENT;
                }

                // If window should be fully opaque and non-click-through, clear WS_EX_LAYERED
                if (_prePipOpacity >= 255 && !_prePipClickThrough)
                {
                    newEx &= ~User32.WS_EX_LAYERED;
                }
                else
                {
                    newEx |= User32.WS_EX_LAYERED;
                }

                User32.SetWindowLongPtr(_targetWindow.Hwnd, User32.GWL_EXSTYLE, (IntPtr)newEx);

                if ((newEx & User32.WS_EX_LAYERED) != 0)
                {
                    User32.SetLayeredWindowAttributes(_targetWindow.Hwnd, 0, _prePipOpacity, User32.LWA_ALPHA);
                }

                IntPtr insertAfter = _isTopmost ? User32.HWND_TOPMOST : User32.HWND_TOP;
                User32.SetWindowPos(_targetWindow.Hwnd, insertAfter, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_SHOWWINDOW | User32.SWP_FRAMECHANGED);
                User32.BringWindowToTop(_targetWindow.Hwnd);
                _originalWindowGhosted = false;
            }

            // Cleanly restore and synchronize opacity service
            if (_opacityService != null)
            {
                _opacityService.SetClickThrough(_targetWindow.Hwnd, _prePipClickThrough);
                if (_prePipOpacity < 255)
                {
                    _opacityService.SetOpacity(_targetWindow.Hwnd, _prePipOpacity);
                }
                else
                {
                    _opacityService.SetOpacity(_targetWindow.Hwnd, 255);
                }
            }

            _targetWindow.IsClickThrough = _prePipClickThrough;

            // Sanitize window preference in config so ghost mode state is never persisted
            if (_config != null)
            {
                var pref = _config.GetOrCreateWindowPreference(_targetWindow.ProcessName);
                pref.IsClickThrough = _prePipClickThrough;
                pref.OpacityLevel = _prePipOpacity;
                _config.Save();
            }

            User32.ShowWindow(_targetWindow.Hwnd, User32.SW_SHOW);
            User32.ShowWindow(_targetWindow.Hwnd, User32.SW_RESTORE);
            User32.BringWindowToTop(_targetWindow.Hwnd);
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

            // Route to specific inner child window or control
            var screenPoint = new User32.POINT(screenX, screenY);
            targetHwnd = _targetWindow.Hwnd;

            try
            {
                var targetClientPt = screenPoint;
                User32.ScreenToClient(_targetWindow.Hwnd, ref targetClientPt);
                IntPtr child = User32.RealChildWindowFromPoint(_targetWindow.Hwnd, targetClientPt);
                if (child != IntPtr.Zero && child != _targetWindow.Hwnd)
                {
                    var childClientPt = screenPoint;
                    User32.ScreenToClient(child, ref childClientPt);
                    IntPtr subChild = User32.RealChildWindowFromPoint(child, childClientPt);
                    targetHwnd = (subChild != IntPtr.Zero && subChild != child) ? subChild : child;
                }
            }
            catch { }

            var clientPoint = screenPoint;
            User32.ScreenToClient(targetHwnd, ref clientPoint);
            lParam = (IntPtr)(((clientPoint.Y & 0xFFFF) << 16) | (clientPoint.X & 0xFFFF));
            return true;
        }

        private void ForwardMouseMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd)) return;

            uint currentThread = User32.GetCurrentThreadId();
            uint targetThread = User32.GetWindowThreadProcessId(hwnd, out _);
            bool attached = false;

            if (currentThread != targetThread && targetThread != 0)
            {
                attached = User32.AttachThreadInput(currentThread, targetThread, true);
            }

            try
            {
                User32.PostMessage(hwnd, msg, wParam, lParam);
            }
            finally
            {
                if (attached)
                {
                    User32.AttachThreadInput(currentThread, targetThread, false);
                }
            }
        }

        private void ForwardMouseWheel(IntPtr targetHwnd, short delta, int screenX, int screenY)
        {
            if (targetHwnd == IntPtr.Zero || !User32.IsWindow(targetHwnd)) return;

            int keyFlags = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) keyFlags |= User32.MK_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) keyFlags |= User32.MK_SHIFT;

            IntPtr wParam = unchecked((IntPtr)((delta << 16) | (keyFlags & 0xFFFF)));
            IntPtr lParam = unchecked((IntPtr)(((screenY & 0xFFFF) << 16) | (screenX & 0xFFFF)));

            bool isHorizontal = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            uint msg = isHorizontal ? (uint)User32.WM_MOUSEHWHEEL : (uint)User32.WM_MOUSEWHEEL;

            // 1. Post directly to child control / render host (standard Win32 delivery for Chromium, Terminal, etc.)
            User32.PostMessage(targetHwnd, msg, wParam, lParam);

            // 2. Also deliver to root top-level window if different
            if (targetHwnd != _targetWindow.Hwnd && User32.IsWindow(_targetWindow.Hwnd))
            {
                User32.PostMessage(_targetWindow.Hwnd, msg, wParam, lParam);
            }
        }

        private void ThumbnailHost_MouseDown(object sender, WpfMouseButtonEventArgs e)
        {
            if (!_isInteractMode)
            {
                if (e.ClickCount == 2)
                {
                    RestoreAndClose();
                    return;
                }
                if (e.LeftButton == MouseButtonState.Pressed) DragMove();
                return;
            }

            try
            {
                ThumbnailHost.CaptureMouse();
                WpfPoint pt = e.GetPosition(ThumbnailHost);
                if (TranslatePipToTargetCoordinates(pt, out IntPtr hwnd, out IntPtr lParam, out _, out _))
                {
                    uint msg;
                    IntPtr wParam;

                    if (e.ChangedButton == MouseButton.Right)
                    {
                        msg = e.ClickCount == 2 ? (uint)User32.WM_RBUTTONDBLCLK : (uint)User32.WM_RBUTTONDOWN;
                        wParam = (IntPtr)User32.MK_RBUTTON;
                    }
                    else if (e.ChangedButton == MouseButton.Middle)
                    {
                        msg = e.ClickCount == 2 ? (uint)User32.WM_MBUTTONDBLCLK : (uint)User32.WM_MBUTTONDOWN;
                        wParam = (IntPtr)User32.MK_MBUTTON;
                    }
                    else
                    {
                        msg = e.ClickCount == 2 ? (uint)User32.WM_LBUTTONDBLCLK : (uint)User32.WM_LBUTTONDOWN;
                        wParam = (IntPtr)User32.MK_LBUTTON;
                    }

                    ForwardMouseMessage(hwnd, msg, wParam, lParam);
                }
            }
            catch { }
        }

        private void ThumbnailHost_MouseUp(object sender, WpfMouseButtonEventArgs e)
        {
            if (!_isInteractMode) return;

            try
            {
                if (ThumbnailHost.IsMouseCaptured)
                {
                    ThumbnailHost.ReleaseMouseCapture();
                }

                WpfPoint pt = e.GetPosition(ThumbnailHost);
                if (TranslatePipToTargetCoordinates(pt, out IntPtr hwnd, out IntPtr lParam, out int screenX, out int screenY))
                {
                    if (e.ChangedButton == MouseButton.Right)
                    {
                        ForwardMouseMessage(hwnd, (uint)User32.WM_RBUTTONUP, IntPtr.Zero, lParam);
                        // In Win32, trigger WM_CONTEXTMENU with screen coordinates
                        IntPtr screenLParam = (IntPtr)(((screenY & 0xFFFF) << 16) | (screenX & 0xFFFF));
                        ForwardMouseMessage(hwnd, (uint)User32.WM_CONTEXTMENU, hwnd, screenLParam);
                    }
                    else if (e.ChangedButton == MouseButton.Middle)
                    {
                        ForwardMouseMessage(hwnd, (uint)User32.WM_MBUTTONUP, IntPtr.Zero, lParam);
                    }
                    else
                    {
                        ForwardMouseMessage(hwnd, (uint)User32.WM_LBUTTONUP, IntPtr.Zero, lParam);
                    }
                }
            }
            catch { }
        }

        private void ThumbnailHost_MouseMove(object sender, WpfMouseEventArgs e)
        {
            if (!_isInteractMode) return;

            long now = Environment.TickCount64;
            if (now - _lastMouseMoveTick < 16) return;
            _lastMouseMoveTick = now;

            try
            {
                WpfPoint pt = e.GetPosition(ThumbnailHost);
                if (TranslatePipToTargetCoordinates(pt, out IntPtr hwnd, out IntPtr lParam, out _, out _))
                {
                    int moveFlags = 0;
                    if (e.LeftButton == MouseButtonState.Pressed) moveFlags |= User32.MK_LBUTTON;
                    if (e.RightButton == MouseButtonState.Pressed) moveFlags |= User32.MK_RBUTTON;
                    if (e.MiddleButton == MouseButtonState.Pressed) moveFlags |= User32.MK_MBUTTON;
                    IntPtr wParam = (IntPtr)moveFlags;
                    ForwardMouseMessage(hwnd, (uint)User32.WM_MOUSEMOVE, wParam, lParam);
                }
            }
            catch { }
        }

        private void ThumbnailHost_PreviewMouseWheel(object sender, WpfMouseWheelEventArgs e)
        {
            if (!_isInteractMode) return;

            try
            {
                WpfPoint pt = e.GetPosition(ThumbnailHost);
                if (TranslatePipToTargetCoordinates(pt, out IntPtr hwnd, out _, out int screenX, out int screenY))
                {
                    short delta = (short)e.Delta;
                    ForwardMouseWheel(hwnd, delta, screenX, screenY);
                }
            }
            catch { }
            e.Handled = true;
        }

        #endregion
    }
}
