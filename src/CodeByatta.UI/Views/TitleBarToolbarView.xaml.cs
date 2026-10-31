using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Models;
using CodeByatta.Core.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;

namespace CodeByatta.UI.Views
{
    public partial class TitleBarToolbarView : Window
    {
        private ManagedWindow? _targetWindow;
        private readonly TopmostService _topmostService;
        private readonly OpacityService _opacityService;
        private readonly DarkModeService _darkModeService;
        private readonly Action<ManagedWindow> _onOpenPip;
        private readonly Action<ManagedWindow> _onCropPip;
        private readonly Action _onOpenSettings;
        private readonly AppConfig _config;

        private readonly DispatcherTimer _autoHideTimer;
        private readonly DispatcherTimer _proximityTimer;
        private bool _isMouseOverToolbar = false;
        private bool _isExpanded = true;
        private RECT _lastBounds;
        private int _hoverElapsedMs = 0;
        private bool _mustLeaveAnchorBeforeHoverExpand = false;

        // Per-Window and Per-Process Location Memory (ArrowCenterRatio & SideOffset matching WindowTop)
        private double _currentRatio = 0.5; // Default center
        private double _currentVerticalOffset = 0.0;
        private readonly Dictionary<IntPtr, WindowPreferences> _windowPreferencesByHwnd = new();

        // Horizontal slider drag tracking
        private bool _isDragging = false;
        private bool _isAnchorDragging = false;
        private WpfPoint _dragStartScreenPos;
        private double _dragStartLeft;
        private double _anchorDragStartLeft;

        public bool AutoHideEnabled { get; set; } = true;

        public TitleBarToolbarView(
            TopmostService topmostService,
            OpacityService opacityService,
            DarkModeService darkModeService,
            Action<ManagedWindow> onOpenPip,
            Action<ManagedWindow> onCropPip,
            Action onOpenSettings,
            AppConfig config)
        {
            InitializeComponent();
            _topmostService = topmostService;
            _opacityService = opacityService;
            _darkModeService = darkModeService;
            _onOpenPip = onOpenPip;
            _onCropPip = onCropPip;
            _onOpenSettings = onOpenSettings;
            _config = config;

            ApplyConfigStyles();

            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _autoHideTimer.Tick += (s, e) =>
            {
                _autoHideTimer.Stop();
                if (_targetWindow != null && _targetWindow.IsClickThrough) return;
                if (PopupOpacitySlider != null && PopupOpacitySlider.IsOpen) return;

                if (AutoHideEnabled && !_isMouseOverToolbar && _isExpanded)
                {
                    Collapse();
                    SaveCurrentWindowPreferenceState();
                }
            };

            // Precision proximity timer: runs at 50ms intervals
            _proximityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _proximityTimer.Tick += ProximityTimer_Tick;
            _proximityTimer.Start();

            SourceInitialized += TitleBarToolbarView_SourceInitialized;
        }

        public void ApplyConfigStyles()
        {
            try
            {
                if (!string.IsNullOrEmpty(_config.ArrowColor))
                {
                    var arrowBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(_config.ArrowColor)!;
                    PathAnchorArrow.Stroke = arrowBrush;
                }
                else
                {
                    PathAnchorArrow.Stroke = new SolidColorBrush(MediaColor.FromRgb(0xCB, 0xD5, 0xE1));
                }
            }
            catch
            {
                PathAnchorArrow.Stroke = new SolidColorBrush(MediaColor.FromRgb(0xCB, 0xD5, 0xE1));
            }

            BtnTopmost.Visibility = _config.ShowSetTopButton ? Visibility.Visible : Visibility.Collapsed;
            BtnOpacity.Visibility = _config.ShowOpacityButton ? Visibility.Visible : Visibility.Collapsed;
            BtnHoverOpacity.Visibility = _config.ShowHoverOpacityButton ? Visibility.Visible : Visibility.Collapsed;
            BtnClickThrough.Visibility = _config.ShowClickThroughButton ? Visibility.Visible : Visibility.Collapsed;
            BtnPip.Visibility = _config.ShowPipButton ? Visibility.Visible : Visibility.Collapsed;
            BtnCrop.Visibility = _config.ShowCropButton ? Visibility.Visible : Visibility.Collapsed;
            BtnDarkMode.Visibility = _config.ShowDarkModeButton ? Visibility.Visible : Visibility.Collapsed;
            BtnCinemaFocus.Visibility = _config.ShowCinemaFocusButton ? Visibility.Visible : Visibility.Collapsed;
            AutoHideEnabled = _config.AutoHideToolbar;
        }

        private void TitleBarToolbarView_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            long newStyle = (long)exStyle | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW;
            User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)newStyle);
        }

        public void AttachTo(ManagedWindow? target, bool keepVisible = false)
        {
            if (_targetWindow != null)
            {
                _opacityService.SetTemporaryHoverReveal(_targetWindow.Hwnd, false);
            }

            // Exclude shell, taskbar, system tray overflow, and own process windows
            if (target != null)
            {
                if (ShellSanitizer.IsShellOrTaskbarOrSelf(target.Hwnd))
                {
                    Hide();
                    return;
                }

                string cls = target.ClassName;
                if (string.Equals(cls, "NotifyIconOverflowWindow", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "TopLevelWindowForOverflowXamlIsland", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "TaskListThumbnailWnd", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "ControlCenterWindow", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "QuickSettingsWindow", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "Progman", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "WorkerW", StringComparison.OrdinalIgnoreCase))
                {
                    Hide();
                    return;
                }

                if (string.Equals(target.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(cls, "CabinetWClass", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(cls, "ExploreWClass", StringComparison.OrdinalIgnoreCase))
                    {
                        Hide();
                        return;
                    }
                }
            }

            _targetWindow = target;
            if (_targetWindow == null || !User32.IsWindow(_targetWindow.Hwnd) || User32.IsIconic(_targetWindow.Hwnd) || (App.CurrentPipTargetHwnd != IntPtr.Zero && _targetWindow.Hwnd == App.CurrentPipTargetHwnd))
            {
                Hide();
                return;
            }

            byte configuredOp = _opacityService.GetConfiguredOpacity(_targetWindow.Hwnd);
            if (configuredOp < 255)
            {
                _targetWindow.Opacity = configuredOp;
            }

            // 1. Retrieve or initialize isolated WindowPreferences strictly per-HWND
            WindowPreferences pref;
            if (!_windowPreferencesByHwnd.TryGetValue(_targetWindow.Hwnd, out pref!))
            {
                pref = new WindowPreferences
                {
                    ProcessName = _targetWindow.ProcessName,
                    OpacityLevel = _targetWindow.Opacity,
                    IsTopmost = _targetWindow.IsTopmost,
                    IsClickThrough = _targetWindow.IsClickThrough,
                    DisableOpacityOnMouseOver = false,
                    IsDarkMode = _darkModeService.IsDarkModeActiveForWindow(_targetWindow.Hwnd),
                    IsCollapsed = true,
                    ArrowCenterRatio = 0.5,
                    HaveArrowCenterRatio = true
                };

                // Restore toolbar positioning if remembered
                if (_config.AutoRememberWindowPreferences)
                {
                    var saved = _config.GetOrCreateWindowPreference(_targetWindow.ProcessName);
                    if (saved != null)
                    {
                        pref.ArrowCenterRatio = saved.ArrowCenterRatio;
                        pref.HaveArrowCenterRatio = saved.HaveArrowCenterRatio;
                        pref.SideOffset = saved.SideOffset;
                        pref.HaveOffset = saved.HaveOffset;
                        pref.IsCollapsed = saved.IsCollapsed;
                    }
                }

                _windowPreferencesByHwnd[_targetWindow.Hwnd] = pref;
            }

            // Restore exact relative horizontal ratio & vertical offset
            _currentRatio = pref.HaveArrowCenterRatio ? pref.ArrowCenterRatio : 0.5;
            _currentVerticalOffset = pref.HaveOffset ? pref.SideOffset : 0.0;

            if (_targetWindow.GetBounds(out _lastBounds))
            {
                // Ignore invalid bounds from minimizing animation
                if (_lastBounds.Left < -10000 || _lastBounds.Width <= 50 || _lastBounds.Height <= 50)
                {
                    Hide();
                    return;
                }

                // Restore expanded/collapsed state according to window memory
                if (pref.IsCollapsed && !_targetWindow.IsClickThrough)
                {
                    Collapse();
                }
                else
                {
                    Expand();
                    if (AutoHideEnabled && !keepVisible)
                    {
                        _autoHideTimer.Stop();
                        _autoHideTimer.Start();
                    }
                }

                UpdatePosition(_lastBounds);
                UpdateVisualState();
                Show();
            }
            else
            {
                Hide();
            }
        }

        public void ShowWithActionFeedback(ManagedWindow target, string actionName)
        {
            _targetWindow = target;
            Expand();
            AttachTo(target, keepVisible: true);
            UpdateVisualState();

            if (_targetWindow != null && !_targetWindow.IsClickThrough)
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Start();
            }
        }

        public double GetToolbarTargetWidth(bool isExp)
        {
            if (isExp)
            {
                FullToolbarPill.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
                double desired = FullToolbarPill.DesiredSize.Width;
                return desired > 50 ? desired : 420.0;
            }
            else
            {
                AnchorHandle.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
                double desired = AnchorHandle.DesiredSize.Width;
                return desired > 30 ? desired : 130.0;
            }
        }

        public void Expand()
        {
            _isExpanded = true;
            _hoverElapsedMs = 0;
            AnchorHandle.Visibility = Visibility.Collapsed;
            FullToolbarPill.Visibility = Visibility.Visible;
            if (_lastBounds.Width > 50)
            {
                UpdatePosition(_lastBounds, forceExpanded: true);
            }
        }

        public void Collapse()
        {
            if (PopupOpacitySlider != null && PopupOpacitySlider.IsOpen)
            {
                PopupOpacitySlider.IsOpen = false;
            }
            _isExpanded = false;
            _hoverElapsedMs = 0;
            FullToolbarPill.Visibility = Visibility.Collapsed;
            AnchorHandle.Visibility = Visibility.Visible;
            if (_lastBounds.Width > 50)
            {
                UpdatePosition(_lastBounds, forceExpanded: false);
            }
        }

        private void ProximityTimer_Tick(object? sender, EventArgs e)
        {
            if (!User32.GetCursorPos(out var cursor)) return;
            ShellSanitizer.CheckCursorOverTaskbar(cursor);
            var dpi = VisualTreeHelper.GetDpi(this);

            // 1. ACTIVE CLICK-THROUGH WINDOWS MONITORING & HEADER RE-DETECTION:
            // When cursor hovers or clicks on the upper header bar of ANY active Click-Through window:
            var ctHwnds = _opacityService.ActiveClickThroughWindows;
            foreach (var ctHwnd in ctHwnds)
            {
                if (!User32.IsWindow(ctHwnd) || User32.IsIconic(ctHwnd) || (App.CurrentPipTargetHwnd != IntPtr.Zero && ctHwnd == App.CurrentPipTargetHwnd)) continue;
                if (!User32.GetWindowRect(ctHwnd, out RECT ctBounds)) continue;
                if (ctBounds.Left < -10000 || ctBounds.Width <= 50 || ctBounds.Height <= 50) continue;

                double ctHeaderTop = ctBounds.Top;
                double ctHeaderBottom = ctBounds.Top + (38 * dpi.DpiScaleY);

                bool isCursorInCtHeader = cursor.X >= ctBounds.Left && cursor.X <= ctBounds.Right &&
                                          cursor.Y >= ctHeaderTop - 10 && cursor.Y <= ctHeaderBottom + 10;
                bool isCursorInCtBody = cursor.X >= ctBounds.Left && cursor.X <= ctBounds.Right &&
                                        cursor.Y > ctHeaderBottom + 10 && cursor.Y <= ctBounds.Bottom;

                if (isCursorInCtHeader)
                {
                    // Make the upper header bar interactive so clicks and focus work natively without taskbar
                    _opacityService.SetHeaderInteractive(ctHwnd, true);

                    // If toolbar is not currently attached to this window, immediately attach and expand!
                    if (_targetWindow == null || _targetWindow.Hwnd != ctHwnd)
                    {
                        var managed = new ManagedWindow(ctHwnd);
                        AttachTo(managed, keepVisible: true);
                        Expand();
                    }

                    // Check if user clicked on the header bar to bring window to foreground
                    short lButton = User32.GetAsyncKeyState(User32.VK_LBUTTON);
                    if ((lButton & 0x8000) != 0)
                    {
                        User32.SetForegroundWindow(ctHwnd);
                        User32.BringWindowToTop(ctHwnd);
                    }
                    break;
                }
                else if (isCursorInCtBody)
                {
                    // Over the body: enforce click-through transparency
                    _opacityService.SetHeaderInteractive(ctHwnd, false);
                }
                else
                {
                    // Outside the window: maintain click-through transparency
                    _opacityService.SetHeaderInteractive(ctHwnd, false);
                }
            }

            if (_targetWindow == null || !User32.IsWindow(_targetWindow.Hwnd) || User32.IsIconic(_targetWindow.Hwnd) || (App.CurrentPipTargetHwnd != IntPtr.Zero && _targetWindow.Hwnd == App.CurrentPipTargetHwnd))
            {
                if (IsVisible) Hide();
                return;
            }

            // Z-Order Topmost Overlay Guarantee: ensure toolbar remains topmost over target window and never transparent
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                long style = (long)User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
                if ((style & User32.WS_EX_TRANSPARENT) != 0)
                {
                    style &= ~User32.WS_EX_TRANSPARENT;
                    User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)style);
                }

                User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
            }

            bool isOverAnchor = false;
            bool isOverPill = false;

            if (!_isExpanded)
            {
                // Titlebar Header Proximity: check if cursor is anywhere along the targeted window's titlebar header
                double headerTop = _lastBounds.Top;
                double headerBottom = _lastBounds.Top + (38 * dpi.DpiScaleY);
                bool isCursorInHeader = cursor.X >= _lastBounds.Left && cursor.X <= _lastBounds.Right &&
                                        cursor.Y >= headerTop - 10 && cursor.Y <= headerBottom + 5;

                double handleLeft = Left * dpi.DpiScaleX;
                double handleTop = Top * dpi.DpiScaleY;
                double handleWidth = (ActualWidth > 0 ? ActualWidth : 120) * dpi.DpiScaleX;
                double handleHeight = (ActualHeight > 0 ? ActualHeight : 32) * dpi.DpiScaleY;

                isOverAnchor = cursor.X >= handleLeft && cursor.X <= handleLeft + handleWidth &&
                               cursor.Y >= handleTop && cursor.Y <= handleTop + handleHeight;

                if (isCursorInHeader || isOverAnchor)
                {
                    if (!AnchorHandle.IsVisible)
                    {
                        AnchorHandle.Visibility = Visibility.Visible;
                    }

                    if (isOverAnchor && !_config.RequireClickToExpandToolbar && !_mustLeaveAnchorBeforeHoverExpand)
                    {
                        _hoverElapsedMs += 50;
                        if (_hoverElapsedMs >= Math.Max(100, _config.HoverTimeMs))
                        {
                            Expand();
                            _hoverElapsedMs = 0;
                        }
                    }
                    else if (!isOverAnchor)
                    {
                        _hoverElapsedMs = 0;
                        _mustLeaveAnchorBeforeHoverExpand = false;
                    }
                }
                else
                {
                    _hoverElapsedMs = 0;
                    _mustLeaveAnchorBeforeHoverExpand = false;
                    if (AnchorHandle.IsVisible && !_isMouseOverToolbar)
                    {
                        AnchorHandle.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                double pillLeft = Left * dpi.DpiScaleX;
                double pillTop = Top * dpi.DpiScaleY;
                double pillWidth = (ActualWidth > 0 ? ActualWidth : 410) * dpi.DpiScaleX;
                double pillHeight = (ActualHeight > 0 ? ActualHeight : 44) * dpi.DpiScaleY;

                isOverPill = cursor.X >= pillLeft - 10 && cursor.X <= pillLeft + pillWidth + 10 &&
                             cursor.Y >= pillTop - 5 && cursor.Y <= pillTop + pillHeight + 5;

                bool isPopupOpen = PopupOpacitySlider != null && PopupOpacitySlider.IsOpen;
                bool isOverPopup = false;
                if (isPopupOpen)
                {
                    if (PopupOpacitySlider!.IsMouseOver)
                    {
                        isOverPopup = true;
                    }
                    else if (PopupOpacitySlider!.Child is FrameworkElement popupContent)
                    {
                        if (popupContent.IsMouseOver)
                        {
                            isOverPopup = true;
                        }
                        else
                        {
                            try
                            {
                                var ps = PresentationSource.FromVisual(popupContent) as HwndSource;
                                if (ps != null && ps.Handle != IntPtr.Zero && User32.GetWindowRect(ps.Handle, out RECT popupWinRect))
                                {
                                    isOverPopup = cursor.X >= popupWinRect.Left - 8 && cursor.X <= popupWinRect.Right + 8 &&
                                                  cursor.Y >= popupWinRect.Top - 8 && cursor.Y <= popupWinRect.Bottom + 8;
                                }
                            }
                            catch { }
                        }
                    }
                }

                // OUTSIDE-CLICK AUTO-COLLAPSE:
                // If user clicks anywhere outside the toolbar pill (and outside the popup if open), immediately collapse
                short lButton = User32.GetAsyncKeyState(User32.VK_LBUTTON);
                short rButton = User32.GetAsyncKeyState(User32.VK_RBUTTON);
                bool isClickingOutside = ((lButton & 0x8000) != 0 || (rButton & 0x8000) != 0) 
                                         && !isOverPill && !_isMouseOverToolbar && !isOverPopup;

                if (isClickingOutside)
                {
                    if (isPopupOpen)
                    {
                        PopupOpacitySlider!.IsOpen = false;
                    }
                    if (AutoHideEnabled)
                    {
                        _mustLeaveAnchorBeforeHoverExpand = true;
                        Collapse();
                        SaveCurrentWindowPreferenceState();
                        return;
                    }
                }

                if (isOverPill || _isMouseOverToolbar || isOverPopup || isPopupOpen)
                {
                    _autoHideTimer.Stop();
                }
                else if (AutoHideEnabled && (_targetWindow == null || !_targetWindow.IsClickThrough))
                {
                    if (!_autoHideTimer.IsEnabled && _isExpanded)
                    {
                        _autoHideTimer.Interval = TimeSpan.FromMilliseconds(350);
                        _autoHideTimer.Start();
                    }
                }
            }

            bool isOverToolbar = (FullToolbarPill.IsVisible && isOverPill) || (AnchorHandle.IsVisible && isOverAnchor) || _isMouseOverToolbar;

            if (isOverToolbar && _targetWindow != null && _targetWindow.IsClickThrough)
            {
                _opacityService.SetHeaderInteractive(_targetWindow.Hwnd, true);
            }

            // Hover Opacity Reveal: Auto-restore 100% opacity when cursor is over a transparent window body ONLY IF that window has Hover Reveal enabled
            var transparentHwnds = _opacityService.ActiveTransparentWindows;
            IntPtr toolbarHwnd = new WindowInteropHelper(this).Handle;

            foreach (var tHwnd in transparentHwnds)
            {
                if (App.CurrentPipTargetHwnd != IntPtr.Zero && tHwnd == App.CurrentPipTargetHwnd) continue;
                if (!User32.IsWindow(tHwnd) || User32.IsIconic(tHwnd)) continue;
                if (_opacityService.IsClickThrough(tHwnd)) continue;

                // Check per-window preference (strictly isolated, never bleeding across processes or tabs)
                bool hasHoverReveal = false;
                if (_windowPreferencesByHwnd.TryGetValue(tHwnd, out var tPref))
                {
                    hasHoverReveal = tPref.DisableOpacityOnMouseOver;
                }

                if (!hasHoverReveal)
                {
                    _opacityService.SetTemporaryHoverReveal(tHwnd, false);
                    continue;
                }

                if (!User32.GetWindowRect(tHwnd, out RECT tRect)) continue;
                if (tRect.Width <= 50 || tRect.Height <= 50 || tRect.Left < -10000) continue;

                bool isCursorInWindow = cursor.X >= tRect.Left && cursor.X <= tRect.Right &&
                                        cursor.Y >= tRect.Top && cursor.Y <= tRect.Bottom;

                bool isOverToolbarForThisWindow = (tHwnd == _targetWindow?.Hwnd) && isOverToolbar;

                if (isCursorInWindow && !isOverToolbarForThisWindow)
                {
                    // Ensure cursor is directly over this window and not on another occluding window on top
                    IntPtr ptHwnd = User32.WindowFromPoint(cursor);
                    IntPtr rootHwnd = ptHwnd != IntPtr.Zero ? User32.GetAncestor(ptHwnd, User32.GA_ROOT) : IntPtr.Zero;
                    if (rootHwnd == IntPtr.Zero) rootHwnd = ptHwnd;

                    bool isDirectlyOver = (rootHwnd == tHwnd) || (rootHwnd == toolbarHwnd);
                    if (isDirectlyOver)
                    {
                        _opacityService.SetTemporaryHoverReveal(tHwnd, true);
                    }
                    else
                    {
                        _opacityService.SetTemporaryHoverReveal(tHwnd, false);
                    }
                }
                else
                {
                    _opacityService.SetTemporaryHoverReveal(tHwnd, false);
                }
            }
        }

        public void UpdatePosition(RECT bounds, bool? forceExpanded = null)
        {
            if (_targetWindow == null || User32.IsIconic(_targetWindow.Hwnd) || (App.CurrentPipTargetHwnd != IntPtr.Zero && _targetWindow.Hwnd == App.CurrentPipTargetHwnd))
            {
                Hide();
                return;
            }

            // Discard offscreen minimize coordinate anomalies (-32000)
            if (bounds.Left < -10000 || bounds.Width <= 50 || bounds.Height <= 50)
            {
                return;
            }
            _lastBounds = bounds;

            var dpi = VisualTreeHelper.GetDpi(this);
            double dipLeft = bounds.Left / dpi.DpiScaleX;
            double dipTop = bounds.Top / dpi.DpiScaleY;
            double dipWidth = bounds.Width / dpi.DpiScaleX;

            // Adaptive Compact Mode for narrow windows (< 580 DIPs):
            if (dipWidth < 580)
            {
                BtnCrop.Visibility = Visibility.Collapsed;
                BtnDarkMode.Visibility = Visibility.Collapsed;
                BtnSettings.Visibility = Visibility.Collapsed;
            }
            else
            {
                BtnCrop.Visibility = _config.ShowCropButton ? Visibility.Visible : Visibility.Collapsed;
                BtnDarkMode.Visibility = _config.ShowDarkModeButton ? Visibility.Visible : Visibility.Collapsed;
                BtnSettings.Visibility = Visibility.Visible;
            }

            bool exp = forceExpanded ?? _isExpanded;
            double currentWidth = GetToolbarTargetWidth(exp);

            // Tabbed browser & editor titlebar awareness (avoid active tabs on the left)
            bool isTabbedApp = _targetWindow != null && (
                _targetWindow.ProcessName.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
                _targetWindow.ProcessName.Contains("msedge", StringComparison.OrdinalIgnoreCase) ||
                _targetWindow.ProcessName.Contains("firefox", StringComparison.OrdinalIgnoreCase) ||
                _targetWindow.ProcessName.Contains("brave", StringComparison.OrdinalIgnoreCase) ||
                _targetWindow.ProcessName.Contains("code", StringComparison.OrdinalIgnoreCase));

            double minLeftOffset = isTabbedApp ? Math.Min(180, dipWidth * 0.25) : Math.Min(70, dipWidth * 0.15);

            // Strict Header In-Window Placement:
            // Left boundary avoidance: avoid window icon / tabs
            // Right boundary avoidance: avoid Minimize/Maximize/Close buttons (at least 140 DIPs or 25% width)
            double minX = dipLeft + minLeftOffset;
            double maxX = Math.Max(minX, dipLeft + dipWidth - currentWidth - Math.Min(140, dipWidth * 0.25));

            // Centered horizontally on the targeted window's titlebar
            double targetCenter = dipLeft + (_currentRatio * dipWidth);
            double targetX = targetCenter - (currentWidth / 2.0);
            targetX = Math.Clamp(targetX, minX, maxX);

            // Docking rule: STRICTLY inside the titlebar header (never floats above or into window client body)
            double targetY = dipTop + 3.0;

            Left = targetX;
            Top = targetY;

            // Topmost overlay guarantee: keep toolbar z-order directly above target window
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
            }
        }

        private void UpdateOffsetsAfterDrag()
        {
            if (_targetWindow == null || ShellSanitizer.IsShellOrTaskbarWindow(_targetWindow.Hwnd) || !_targetWindow.GetBounds(out var bounds)) return;
            if (bounds.Left < -10000 || bounds.Width <= 50 || bounds.Height <= 50) return;

            var dpi = VisualTreeHelper.GetDpi(this);
            double dipLeft = bounds.Left / dpi.DpiScaleX;
            double dipTop = bounds.Top / dpi.DpiScaleY;
            double dipWidth = bounds.Width / dpi.DpiScaleX;
            double currentWidth = ActualWidth > 0 ? ActualWidth : (_isExpanded ? 410 : 120);
            double currentHeight = ActualHeight > 0 ? ActualHeight : 34;

            // Constrain vertically strictly inside titlebar
            Top = dipTop + 3.0;
            _currentVerticalOffset = 0;

            // Constrain horizontally within titlebar bounds
            double minX = dipLeft + Math.Min(70, dipWidth * 0.15);
            double maxX = Math.Max(minX, dipLeft + dipWidth - currentWidth - Math.Min(140, dipWidth * 0.25));
            double clampedX = Math.Clamp(Left, minX, maxX);
            Left = clampedX;

            // Calculate precise horizontal ratio relative to this window's titlebar
            double toolbarCenter = clampedX + (currentWidth / 2.0);
            _currentRatio = Math.Clamp((toolbarCenter - dipLeft) / Math.Max(1.0, dipWidth), 0.05, 0.95);

            // Save in active HWND cache
            if (!_windowPreferencesByHwnd.TryGetValue(_targetWindow.Hwnd, out var pref))
            {
                pref = new WindowPreferences { ProcessName = _targetWindow.ProcessName };
                _windowPreferencesByHwnd[_targetWindow.Hwnd] = pref;
            }

            pref.ProcessName = _targetWindow.ProcessName;
            pref.ArrowCenterRatio = _currentRatio;
            pref.HaveArrowCenterRatio = true;
            pref.SideOffset = 0;
            pref.HaveOffset = true;
            pref.IsCollapsed = !_isExpanded;
            pref.LastModified = DateTime.UtcNow;

            // Persist in config.json under ProcessName
            var savedPref = _config.GetOrCreateWindowPreference(_targetWindow.ProcessName);
            savedPref.ArrowCenterRatio = _currentRatio;
            savedPref.HaveArrowCenterRatio = true;
            savedPref.SideOffset = 0;
            savedPref.HaveOffset = true;
            savedPref.IsCollapsed = !_isExpanded;
            savedPref.LastModified = DateTime.UtcNow;

            _config.Save();
        }

        public void UpdateVisualState()
        {
            if (_targetWindow == null) return;
            _targetWindow.RefreshInfo();

            // Pin state
            bool isTop = _targetWindow.IsTopmost;
            DotTopmost.Visibility = isTop ? Visibility.Visible : Visibility.Collapsed;
            BtnTopmost.Background = isTop ? new SolidColorBrush(MediaColor.FromArgb(0x55, 0xFF, 0x3B, 0x30)) : MediaBrushes.Transparent;

            // Opacity percentage state
            byte currentAlpha = _opacityService.GetConfiguredOpacity(_targetWindow.Hwnd);
            if (currentAlpha == 255) currentAlpha = _targetWindow.Opacity;
            int percent = (int)Math.Round((currentAlpha / 255.0) * 100);
            if (currentAlpha <= 5) percent = 0;
            TxtOpacityPercent.Text = $"{percent}%";
            TxtOpacityPercent.Foreground = percent < 100 ? new SolidColorBrush(MediaColor.FromRgb(0x4C, 0xC2, 0xFF)) : MediaBrushes.White;
            BtnOpacity.Background = percent < 100 ? new SolidColorBrush(MediaColor.FromArgb(0x44, 0x4C, 0xC2, 0xFF)) : MediaBrushes.Transparent;

            if (!SliderOpacity.IsMouseCaptureWithin && !_isUpdatingSliderInternally)
            {
                _isUpdatingSliderInternally = true;
                SliderOpacity.Value = percent;
                TxtSliderPercentBadge.Text = $"{percent}%";
                _isUpdatingSliderInternally = false;
            }

            // Click-through state
            bool isClickThrough = _targetWindow.IsClickThrough;
            DotClickThrough.Visibility = isClickThrough ? Visibility.Visible : Visibility.Collapsed;
            TxtClickThroughStatus.Visibility = isClickThrough ? Visibility.Visible : Visibility.Collapsed;
            TxtClickThroughStatus.Text = isClickThrough ? "ON" : "";
            BtnClickThrough.Background = isClickThrough ? new SolidColorBrush(MediaColor.FromArgb(0x55, 0x30, 0xD1, 0x58)) : MediaBrushes.Transparent;

            // Dark Mode state
            bool isDark = _targetWindow != null && _darkModeService.IsDarkModeActiveForWindow(_targetWindow.Hwnd);
            DotDarkMode.Visibility = isDark ? Visibility.Visible : Visibility.Collapsed;
            BtnDarkMode.Background = isDark ? new SolidColorBrush(MediaColor.FromArgb(0x55, 0xFF, 0xD6, 0x0A)) : MediaBrushes.Transparent;

            // Cinema Focus state
            if (System.Windows.Application.Current is App appInst)
            {
                bool isCinema = appInst.IsCinemaFocusActive;
                DotCinemaFocus.Visibility = isCinema ? Visibility.Visible : Visibility.Collapsed;
                BtnCinemaFocus.Background = isCinema ? new SolidColorBrush(MediaColor.FromArgb(0x55, 0xBF, 0x5A, 0xF2)) : MediaBrushes.Transparent;
            }

            // Hover Opacity Reveal state (strictly isolated per-window)
            bool isHoverOpacityOn = false;
            if (_targetWindow != null && _windowPreferencesByHwnd.TryGetValue(_targetWindow.Hwnd, out var currentPref))
            {
                isHoverOpacityOn = currentPref.DisableOpacityOnMouseOver;
            }
            DotHoverOpacity.Visibility = isHoverOpacityOn ? Visibility.Visible : Visibility.Collapsed;
            BtnHoverOpacity.Background = isHoverOpacityOn ? new SolidColorBrush(MediaColor.FromArgb(0x55, 0x30, 0xD1, 0x58)) : MediaBrushes.Transparent;
        }

        private void SaveCurrentWindowPreferenceState()
        {
            if (_targetWindow == null || ShellSanitizer.IsShellOrTaskbarWindow(_targetWindow.Hwnd) || (App.CurrentPipTargetHwnd != IntPtr.Zero && _targetWindow.Hwnd == App.CurrentPipTargetHwnd)) return;

            // Update live per-window state strictly isolated to this HWND
            if (!_windowPreferencesByHwnd.TryGetValue(_targetWindow.Hwnd, out var pref))
            {
                pref = new WindowPreferences();
                _windowPreferencesByHwnd[_targetWindow.Hwnd] = pref;
            }

            pref.ProcessName = _targetWindow.ProcessName;
            pref.IsTopmost = _targetWindow.IsTopmost;
            byte op = _opacityService.GetConfiguredOpacity(_targetWindow.Hwnd);
            if (op == 255) op = _targetWindow.Opacity;
            pref.OpacityLevel = op;
            pref.IsClickThrough = _targetWindow.IsClickThrough;
            pref.IsCollapsed = !_isExpanded;
            pref.ArrowCenterRatio = _currentRatio;
            pref.HaveArrowCenterRatio = true;
            pref.SideOffset = 0;
            pref.HaveOffset = true;
            pref.IsDarkMode = _darkModeService.IsDarkModeActiveForWindow(_targetWindow.Hwnd);
            pref.LastModified = DateTime.UtcNow;

            // Save layout/toolbar positioning to config without polluting other windows
            var savedPref = _config.GetOrCreateWindowPreference(_targetWindow.ProcessName);
            savedPref.ArrowCenterRatio = _currentRatio;
            savedPref.HaveArrowCenterRatio = true;
            savedPref.SideOffset = 0;
            savedPref.HaveOffset = true;
            savedPref.IsCollapsed = !_isExpanded;
            savedPref.LastModified = DateTime.UtcNow;
            _config.Save();
        }

        private void StartHorizontalDrag(MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _dragStartScreenPos = PointToScreen(e.GetPosition(this));
                _dragStartLeft = Left;
                FullToolbarPill.CaptureMouse();
                e.Handled = true;
            }
        }

        private void FullToolbarPill_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.OriginalSource is not WpfButton && e.OriginalSource is not TextBlock)
            {
                StartHorizontalDrag(e);
            }
        }

        private void DragGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            StartHorizontalDrag(e);
        }

        private void FullToolbarPill_MouseMove(object sender, WpfMouseEventArgs e)
        {
            if (_isDragging && _targetWindow != null && _targetWindow.GetBounds(out var bounds))
            {
                var currentScreenPos = PointToScreen(e.GetPosition(this));
                var dpi = VisualTreeHelper.GetDpi(this);
                double deltaX = (currentScreenPos.X - _dragStartScreenPos.X) / dpi.DpiScaleX;

                double dipLeft = bounds.Left / dpi.DpiScaleX;
                double dipTop = bounds.Top / dpi.DpiScaleY;
                double dipWidth = bounds.Width / dpi.DpiScaleX;
                double width = GetToolbarTargetWidth(true);

                bool isTabbedApp = _targetWindow.ProcessName.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
                                   _targetWindow.ProcessName.Contains("msedge", StringComparison.OrdinalIgnoreCase) ||
                                   _targetWindow.ProcessName.Contains("firefox", StringComparison.OrdinalIgnoreCase) ||
                                   _targetWindow.ProcessName.Contains("brave", StringComparison.OrdinalIgnoreCase) ||
                                   _targetWindow.ProcessName.Contains("code", StringComparison.OrdinalIgnoreCase);
                double minLeftOffset = isTabbedApp ? Math.Min(180, dipWidth * 0.25) : Math.Min(70, dipWidth * 0.15);

                double minX = dipLeft + minLeftOffset;
                double maxX = Math.Max(minX, dipLeft + dipWidth - width - Math.Min(140, dipWidth * 0.25));

                double newLeft = Math.Clamp(_dragStartLeft + deltaX, minX, maxX);
                Left = newLeft;
                Top = dipTop + 3.0; // Strictly locked vertically in the titlebar

                // Update ratio live
                double toolbarCenter = newLeft + (width / 2.0);
                _currentRatio = Math.Clamp((toolbarCenter - dipLeft) / Math.Max(1.0, dipWidth), 0.05, 0.95);
            }
        }

        private void FullToolbarPill_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                FullToolbarPill.ReleaseMouseCapture();
                SaveCurrentWindowPreferenceState();
            }
        }

        private void BtnTopmost_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow == null) return;
            _topmostService.ToggleTopmost(_targetWindow.Hwnd);
            UpdateVisualState();
            SaveCurrentWindowPreferenceState();
        }

        private void BtnOpacity_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow == null) return;
            _autoHideTimer.Stop();

            // Cycle through opacities: 100% -> 80% -> 60% -> 40% -> 20% -> 0% -> 100%
            byte current = _opacityService.GetConfiguredOpacity(_targetWindow.Hwnd);
            if (current == 255) current = _targetWindow.Opacity;
            byte next;
            if (current >= 230) next = 204;       // 80%
            else if (current >= 180) next = 153;  // 60%
            else if (current >= 130) next = 102;  // 40%
            else if (current >= 75) next = 51;    // 20%
            else if (current >= 20) next = 5;     // 0% (alpha 5 - clean minimum visible/ghost)
            else next = 255;                      // 100%

            _targetWindow.Opacity = next;
            _opacityService.SetOpacity(_targetWindow.Hwnd, next);

            UpdateVisualState();
            SaveCurrentWindowPreferenceState();

            // Display interactive horizontal slider popup right underneath
            PopupOpacitySlider.IsOpen = true;
            _autoHideTimer.Stop();
        }

        private void BtnCinemaFocus_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow == null) return;
            _autoHideTimer.Stop();
            if (System.Windows.Application.Current is App app)
            {
                app.ToggleCinemaFocus(_targetWindow.Hwnd);
                UpdateVisualState();
                SaveCurrentWindowPreferenceState();
            }
        }

        private bool _isUpdatingSliderInternally;

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_targetWindow == null || _isUpdatingSliderInternally) return;

            int percent = (int)Math.Round(e.NewValue);
            byte alpha = (byte)Math.Clamp((int)Math.Round((percent / 100.0) * 255), 10, 255);

            _targetWindow.Opacity = alpha;
            _opacityService.SetOpacity(_targetWindow.Hwnd, alpha);

            TxtOpacityPercent.Text = $"{percent}%";
            TxtOpacityPercent.Foreground = percent < 100 ? new SolidColorBrush(MediaColor.FromRgb(0x4C, 0xC2, 0xFF)) : MediaBrushes.White;
            TxtSliderPercentBadge.Text = $"{percent}%";

            SaveCurrentWindowPreferenceState();
        }

        private void BtnPresetOpacity_Click(object sender, RoutedEventArgs e)
        {
            _autoHideTimer.Stop();
            if (sender is System.Windows.Controls.Button btn && int.TryParse(btn.Tag?.ToString(), out int val))
            {
                SliderOpacity.Value = val;
            }
            // Ensure toolbar stays expanded and visible
            Expand();
            e.Handled = true;
        }

        private void BtnOpacity_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_targetWindow == null) return;

            byte current = _opacityService.GetConfiguredOpacity(_targetWindow.Hwnd);
            if (current == 255) current = _targetWindow.Opacity;
            int step = _config.MouseWheelSensitivity > 0 ? _config.MouseWheelSensitivity * 2 : 15;
            int delta = e.Delta > 0 ? step : -step;
            int newAlpha = Math.Clamp(current + delta, 40, 255);

            _targetWindow.Opacity = (byte)newAlpha;
            _opacityService.SetOpacity(_targetWindow.Hwnd, (byte)newAlpha);

            UpdateVisualState();
            SaveCurrentWindowPreferenceState();
            e.Handled = true;
        }

        private void BtnHoverOpacity_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow == null) return;

            if (!_windowPreferencesByHwnd.TryGetValue(_targetWindow.Hwnd, out var pref))
            {
                pref = new WindowPreferences { ProcessName = _targetWindow.ProcessName };
                _windowPreferencesByHwnd[_targetWindow.Hwnd] = pref;
            }

            pref.DisableOpacityOnMouseOver = !pref.DisableOpacityOnMouseOver;

            // If turned off for this specific window, immediately restore it to its base opacity
            if (!pref.DisableOpacityOnMouseOver)
            {
                _opacityService.SetTemporaryHoverReveal(_targetWindow.Hwnd, false);
            }

            UpdateVisualState();
            SaveCurrentWindowPreferenceState();
        }

        private void BtnClickThrough_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow == null) return;

            bool enabling = !_targetWindow.IsClickThrough;
            if (enabling)
            {
                _opacityService.SetClickThrough(_targetWindow.Hwnd, true);
                _targetWindow.IsClickThrough = true;
                Expand();
                _autoHideTimer.Stop();
            }
            else
            {
                _opacityService.SetClickThrough(_targetWindow.Hwnd, false);
                _targetWindow.IsClickThrough = false;
                User32.SetForegroundWindow(_targetWindow.Hwnd);
                if (AutoHideEnabled)
                {
                    _autoHideTimer.Stop();
                    _autoHideTimer.Start();
                }
            }

            UpdateVisualState();
            SaveCurrentWindowPreferenceState();
        }

        private void BtnPip_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow == null) return;
            _onOpenPip(_targetWindow);
        }

        private void BtnCrop_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow == null) return;
            _onCropPip(_targetWindow);
        }

        private void BtnDarkMode_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow != null)
            {
                _darkModeService.ToggleDarkModeForWindow(_targetWindow.Hwnd);
                UpdateVisualState();
                SaveCurrentWindowPreferenceState();
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            _onOpenSettings();
        }

        private void BtnCollapse_Click(object sender, RoutedEventArgs e)
        {
            _mustLeaveAnchorBeforeHoverExpand = true;
            Collapse();
            SaveCurrentWindowPreferenceState();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_targetWindow != null)
            {
                _opacityService.SetTemporaryHoverReveal(_targetWindow.Hwnd, false);
            }
            Hide();
        }

        private void StartAnchorHorizontalDrag(MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _dragStartScreenPos = PointToScreen(e.GetPosition(this));
                _anchorDragStartLeft = Left;
                _isAnchorDragging = false;
                AnchorHandle.CaptureMouse();
                e.Handled = true;
            }
        }

        private void AnchorDragGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            StartAnchorHorizontalDrag(e);
        }

        private void AnchorHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (_targetWindow != null)
                {
                    _topmostService.ToggleTopmost(_targetWindow.Hwnd);
                    UpdateVisualState();
                    SaveCurrentWindowPreferenceState();
                }
                e.Handled = true;
                return;
            }

            StartAnchorHorizontalDrag(e);
        }

        private void AnchorHandle_MouseMove(object sender, WpfMouseEventArgs e)
        {
            if (AnchorHandle.IsMouseCaptured && _targetWindow != null && _targetWindow.GetBounds(out var bounds))
            {
                var currentScreenPos = PointToScreen(e.GetPosition(this));
                var dpi = VisualTreeHelper.GetDpi(this);
                double deltaX = (currentScreenPos.X - _dragStartScreenPos.X) / dpi.DpiScaleX;

                if (!_isAnchorDragging && Math.Abs(deltaX) > 3)
                {
                    _isAnchorDragging = true;
                }

                if (_isAnchorDragging)
                {
                    double dipLeft = bounds.Left / dpi.DpiScaleX;
                    double dipTop = bounds.Top / dpi.DpiScaleY;
                    double dipWidth = bounds.Width / dpi.DpiScaleX;
                    double width = GetToolbarTargetWidth(false);

                    bool isTabbedApp = _targetWindow.ProcessName.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
                                       _targetWindow.ProcessName.Contains("msedge", StringComparison.OrdinalIgnoreCase) ||
                                       _targetWindow.ProcessName.Contains("firefox", StringComparison.OrdinalIgnoreCase) ||
                                       _targetWindow.ProcessName.Contains("brave", StringComparison.OrdinalIgnoreCase) ||
                                       _targetWindow.ProcessName.Contains("code", StringComparison.OrdinalIgnoreCase);
                    double minLeftOffset = isTabbedApp ? Math.Min(180, dipWidth * 0.25) : Math.Min(70, dipWidth * 0.15);

                    double minX = dipLeft + minLeftOffset;
                    double maxX = Math.Max(minX, dipLeft + dipWidth - width - Math.Min(140, dipWidth * 0.25));

                    double newLeft = Math.Clamp(_anchorDragStartLeft + deltaX, minX, maxX);
                    Left = newLeft;
                    Top = dipTop + 3.0;

                    double toolbarCenter = newLeft + (width / 2.0);
                    _currentRatio = Math.Clamp((toolbarCenter - dipLeft) / Math.Max(1.0, dipWidth), 0.05, 0.95);
                }
            }
        }

        private void AnchorHandle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (AnchorHandle.IsMouseCaptured)
            {
                AnchorHandle.ReleaseMouseCapture();

                if (_isAnchorDragging)
                {
                    _isAnchorDragging = false;
                    SaveCurrentWindowPreferenceState();
                    e.Handled = true;
                    return;
                }

                _isAnchorDragging = false;

                // Handle single-click expand
                if (e.ChangedButton == MouseButton.Left)
                {
                    _mustLeaveAnchorBeforeHoverExpand = false;
                    Expand();
                    SaveCurrentWindowPreferenceState();
                    e.Handled = true;
                }
            }
        }

        private void AnchorHandle_MouseEnter(object sender, WpfMouseEventArgs e)
        {
            // Hover start handled by precision timer
        }

        private void AnchorHandle_MouseLeave(object sender, WpfMouseEventArgs e)
        {
            _hoverElapsedMs = 0;
        }

        private void Window_MouseEnter(object sender, WpfMouseEventArgs e)
        {
            _isMouseOverToolbar = true;
            _autoHideTimer.Stop();
        }

        private void Window_MouseLeave(object sender, WpfMouseEventArgs e)
        {
            _isMouseOverToolbar = false;
            if (PopupOpacitySlider != null && PopupOpacitySlider.IsOpen)
            {
                return; // Do NOT auto-hide while the opacity popup is open
            }
            if (AutoHideEnabled && (_targetWindow == null || !_targetWindow.IsClickThrough))
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Interval = TimeSpan.FromMilliseconds(350);
                _autoHideTimer.Start();
            }
        }

        private void FullToolbarPill_MouseEnter(object sender, WpfMouseEventArgs e)
        {
            _isMouseOverToolbar = true;
            _autoHideTimer.Stop();
        }

        private void FullToolbarPill_MouseLeave(object sender, WpfMouseEventArgs e)
        {
            _isMouseOverToolbar = false;
            if (PopupOpacitySlider != null && PopupOpacitySlider.IsOpen)
            {
                return;
            }
            if (AutoHideEnabled && (_targetWindow == null || !_targetWindow.IsClickThrough))
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Interval = TimeSpan.FromMilliseconds(350);
                _autoHideTimer.Start();
            }
        }
    }
}
