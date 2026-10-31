using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Models;
using CodeByatta.Core.Services;
using CodeByatta.UI.Views;
using Forms = System.Windows.Forms;
using WpfApp = System.Windows.Application;

namespace CodeByatta.UI
{
    public partial class App : WpfApp
    {
        private Mutex? _instanceMutex;
        private AppConfig _config = null!;
        private TopmostService _topmostService = null!;
        private OpacityService _opacityService = null!;
        private DarkModeService _darkModeService = null!;
        private PipManagerService _pipManager = null!;
        private WindowTrackingService _trackingService = null!;
        private HotkeyService _hotkeyService = null!;
        private UpdateService _updateService = null!;

        private TitleBarToolbarView? _toolbarView;
        private Forms.NotifyIcon? _trayIcon;
        private DispatcherTimer? _pinEnforcerTimer;
        private DispatcherTimer? _updateCheckTimer;
        private HwndSource? _messageHwndSource;

        private SettingsWindow? _activeSettingsWindow;
        private PipWindow? _currentPipWindow;
        public static IntPtr CurrentPipTargetHwnd { get; private set; } = IntPtr.Zero;
        private CropOverlayWindow? _currentCropOverlay;
        private readonly Dictionary<IntPtr, TopmostBorderOverlayWindow> _activeBorders = new();
        private readonly Dictionary<IntPtr, DarkModeOverlayWindow> _activeDarkOverlays = new();

        private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("App started");
            try
            {
                // Standalone cleanup argument handling for uninstaller, installer or emergency restore
                if (e.Args.Length > 0 && (Array.Exists(e.Args, a => a.Equals("--cleanup-exit", StringComparison.OrdinalIgnoreCase) ||
                                                                    a.Equals("--restore-all", StringComparison.OrdinalIgnoreCase) ||
                                                                    a.Equals("-cleanup", StringComparison.OrdinalIgnoreCase))))
                {
                    Log("Running standalone cleanup-exit routine");
                    _topmostService = new TopmostService();
                    _opacityService = new OpacityService();
                    _config = AppConfig.Load();
                    RestoreAllExternalWindows();
                    Shutdown();
                    return;
                }

                const string mutexName = "OpenWindow_SingleInstanceMutex";
                _instanceMutex = new Mutex(true, mutexName, out bool isNewInstance);

                if (!isNewInstance)
                {
                    Log("Another instance already running");
                    Forms.MessageBox.Show(
                        "OpenWindow is already running in the system tray.",
                        "OpenWindow",
                        Forms.MessageBoxButtons.OK,
                        Forms.MessageBoxIcon.Information);
                    Shutdown();
                    return;
                }

                base.OnStartup(e);

                AppDomain.CurrentDomain.ProcessExit += (s, ev) => RestoreAllExternalWindows();
                Microsoft.Win32.SystemEvents.SessionEnding += (s, ev) => RestoreAllExternalWindows();

                Log("Loading config");
                _config = AppConfig.Load();
                _topmostService = new TopmostService();
                _topmostService.TopmostStateChanged += (s, ev) => Dispatcher.Invoke(() => UpdateBorderForWindow(ev.Hwnd, ev.IsTopmost));
                _opacityService = new OpacityService();
                _opacityService.OpacityChanged += (s, ev) => Dispatcher.Invoke(() =>
                {
                    if (_activeDarkOverlays.TryGetValue(ev.Hwnd, out var overlay))
                    {
                        overlay.SetOpacity(ev.Opacity);
                    }
                });
                _darkModeService = new DarkModeService();
                _darkModeService.DarkModeStateChanged += (s, ev) => Dispatcher.Invoke(() => UpdateDarkModeForWindow(ev.Hwnd, ev.IsDarkMode));
                _pipManager = new PipManagerService();
                _trackingService = new WindowTrackingService(_config.ExcludedProcesses);
                _hotkeyService = new HotkeyService();
                _updateService = new UpdateService();

                Log("Initializing Tray");
                InitializeTrayIcon();

                // Proactively sanitize shell/taskbar windows and enforce taskbar top position
                ShellSanitizer.SanitizeShellWindows();
                ShellSanitizer.EnsureTaskbarOnTop();

                Log("Initializing Toolbar");
                InitializeToolbar();

                Log("Initializing Hotkeys");
                InitializeHotkeys();

                _trackingService.ForegroundWindowChanged += TrackingService_ForegroundWindowChanged;
                _trackingService.WindowBoundsChanged += TrackingService_WindowBoundsChanged;
                Log("Starting TrackingService");
                _trackingService.Start();

                // Periodic watchdog every 3 seconds to keep pinned windows on top and sync borders
                _pinEnforcerTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _pinEnforcerTimer.Tick += (s, ev) =>
                {
                    _topmostService.EnforcePinnedWindows();
                    SyncBorderOverlays();
                    ShellSanitizer.SanitizeShellWindows();
                    ShellSanitizer.EnsureTaskbarOnTop();

                    // If SettingsWindow is open, ensure it stays above all pinned windows
                    if (_activeSettingsWindow != null && _activeSettingsWindow.IsLoaded)
                    {
                        IntPtr h = new WindowInteropHelper(_activeSettingsWindow).Handle;
                        User32.SetWindowPos(h, User32.HWND_TOPMOST, 0, 0, 0, 0, User32.SWP_NOMOVE | User32.SWP_NOSIZE);
                        User32.BringWindowToTop(h);
                    }
                };
                _pinEnforcerTimer.Start();

                if (_config.AutoCheckForUpdates)
                {
                    StartPeriodicUpdateChecks();
                }

                Log("App ready and running in background loop");
            }
            catch (Exception ex)
            {
                Log($"FATAL EXCEPTION: {ex}");
                Forms.MessageBox.Show($"Startup error: {ex.Message}\n{ex.StackTrace}", "CodeByatta Error", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                Shutdown(1);
            }
        }

        private void InitializeToolbar()
        {
            if (!_config.ShowTitleBarToolbar) return;

            _toolbarView = new TitleBarToolbarView(
                _topmostService,
                _opacityService,
                _darkModeService,
                TogglePip,
                OpenCropWindow,
                OpenSettings,
                _config);
        }

        private void InitializeTrayIcon()
        {
            var menu = new Forms.ContextMenuStrip();

            var pinItem = new Forms.ToolStripMenuItem("📌 Pin Foreground Window (Alt+Z)", null, (s, e) =>
            {
                var hwnd = User32.GetForegroundWindow();
                if (hwnd != IntPtr.Zero && !IsOwnProcessOrWindow(hwnd) && !ShellSanitizer.IsShellOrTaskbarWindow(hwnd)) ToggleTopmostWithBorder(hwnd);
            });

            var opacityItem = new Forms.ToolStripMenuItem("🪟 Toggle Transparency (Alt+A)", null, (s, e) =>
            {
                var hwnd = User32.GetForegroundWindow();
                if (hwnd != IntPtr.Zero && !IsOwnProcessOrWindow(hwnd) && !ShellSanitizer.IsShellOrTaskbarWindow(hwnd)) _opacityService.ToggleTransparency(hwnd, (byte)(_config.DefaultOpacityPercent * 2.55));
            });

            var darkItem = new Forms.ToolStripMenuItem("🌙 Toggle Dark Mode (Alt+D)", null, (s, e) =>
            {
                _darkModeService.ToggleDarkMode();
            });

            var restoreItem = new Forms.ToolStripMenuItem("🔄 Restore All Windows to Default", null, (s, e) =>
            {
                RestoreAllExternalWindows();
            });

            var settingsItem = new Forms.ToolStripMenuItem("⚙ Settings...", null, (s, e) =>
            {
                OpenSettings();
            });

            var exitItem = new Forms.ToolStripMenuItem("❌ Exit", null, (s, e) =>
            {
                Shutdown();
            });

            menu.Items.Add(pinItem);
            menu.Items.Add(opacityItem);
            menu.Items.Add(darkItem);
            menu.Items.Add(restoreItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(settingsItem);
            menu.Items.Add(exitItem);

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            Icon appIcon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = appIcon,
                Text = "OpenWindow",
                ContextMenuStrip = menu,
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => OpenSettings();
        }

        private void InitializeHotkeys()
        {
            try
            {
                var p = new HwndSourceParameters("CodeByattaHotkeySink")
                {
                    Width = 0,
                    Height = 0,
                    WindowStyle = 0
                };
                _messageHwndSource = new HwndSource(p);
                _messageHwndSource.AddHook(HwndHook);

                _hotkeyService.Initialize(_messageHwndSource.Handle, _config.Hotkeys);
                _hotkeyService.HotkeyTriggered += HotkeyService_HotkeyTriggered;
            }
            catch (Exception ex)
            {
                Log($"Hotkey initialization warning: {ex.Message}");
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_hotkeyService.ProcessMessage(msg, wParam))
            {
                handled = true;
            }
            return IntPtr.Zero;
        }

        private bool IsOwnProcessOrWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd)) return true;
            User32.GetWindowThreadProcessId(hwnd, out uint pid);
            return pid == Environment.ProcessId;
        }

        private void HotkeyService_HotkeyTriggered(object? sender, HotkeyAction action)
        {
            // Strict PiP toggle: if any PiP is open and user presses Alt+Q, restore and close immediately
            if (action == HotkeyAction.TogglePip)
            {
                if (_currentPipWindow != null)
                {
                    _currentPipWindow.RestoreAndClose();
                    _currentPipWindow = null;
                    return;
                }
            }

            var foregroundHwnd = User32.GetForegroundWindow();
            if (foregroundHwnd == IntPtr.Zero || IsOwnProcessOrWindow(foregroundHwnd) || ShellSanitizer.IsShellOrTaskbarWindow(foregroundHwnd)) return;

            var managed = new ManagedWindow(foregroundHwnd);

            switch (action)
            {
                case HotkeyAction.ToggleTopmost:
                    ToggleTopmostWithBorder(foregroundHwnd);
                    bool isTop = _topmostService.IsTopmost(foregroundHwnd);
                    if (_config.ShowNotificationOnHotkey)
                    {
                        HotkeyNotificationHudWindow.ShowHud("Always-on-Top", isTop ? "ON" : "OFF", "pin", isTop);
                    }
                    _toolbarView?.ShowWithActionFeedback(managed, "Topmost");
                    break;
                case HotkeyAction.ToggleOpacity:
                    _opacityService.ToggleTransparency(foregroundHwnd, (byte)(_config.DefaultOpacityPercent * 2.55));
                    byte alpha = _opacityService.GetConfiguredOpacity(foregroundHwnd);
                    int pct = (int)Math.Round((alpha / 255.0) * 100);
                    if (_config.ShowNotificationOnHotkey)
                    {
                        HotkeyNotificationHudWindow.ShowHud("Window Opacity", $"{pct}%", "opacity", pct < 100);
                    }
                    _toolbarView?.ShowWithActionFeedback(managed, "Opacity");
                    break;
                case HotkeyAction.ToggleClickThrough:
                    var targetHwnd = foregroundHwnd;
                    // If foreground window is not click-through, check if cursor is currently over an active click-through window
                    if (!_opacityService.IsClickThrough(targetHwnd) && User32.GetCursorPos(out var curPt))
                    {
                        foreach (var ctHwnd in _opacityService.ActiveClickThroughWindows)
                        {
                            if (User32.IsWindow(ctHwnd) && User32.GetWindowRect(ctHwnd, out var ctRect))
                            {
                                if (curPt.X >= ctRect.Left && curPt.X <= ctRect.Right &&
                                    curPt.Y >= ctRect.Top && curPt.Y <= ctRect.Bottom)
                                {
                                    targetHwnd = ctHwnd;
                                    break;
                                }
                            }
                        }
                    }

                    _opacityService.ToggleClickThrough(targetHwnd);
                    bool isCt = _opacityService.IsClickThrough(targetHwnd);
                    if (_config.ShowNotificationOnHotkey)
                    {
                        HotkeyNotificationHudWindow.ShowHud("Click-Through", isCt ? "ON" : "OFF", "clickthrough", isCt);
                    }
                    var targetManaged = new ManagedWindow(targetHwnd);
                    _toolbarView?.ShowWithActionFeedback(targetManaged, "ClickThrough");
                    break;
                case HotkeyAction.TogglePip:
                    TogglePip(managed);
                    if (_config.ShowNotificationOnHotkey)
                    {
                        HotkeyNotificationHudWindow.ShowHud("Picture-in-Picture", _currentPipWindow != null ? "ACTIVE" : "CLOSED", "pip", _currentPipWindow != null);
                    }
                    break;
                case HotkeyAction.ToggleDarkMode:
                    _darkModeService.ToggleDarkModeForWindow(managed.Hwnd);
                    bool isDark = _darkModeService.IsDarkModeActiveForWindow(managed.Hwnd);
                    if (_config.ShowNotificationOnHotkey)
                    {
                        HotkeyNotificationHudWindow.ShowHud("Dark Mode", isDark ? "ON" : "OFF", "dark", isDark);
                    }
                    _toolbarView?.ShowWithActionFeedback(managed, "DarkMode");
                    break;
            }
        }

        private void ToggleTopmostWithBorder(IntPtr hwnd)
        {
            if (ShellSanitizer.IsShellOrTaskbarWindow(hwnd)) return;
            _topmostService.ToggleTopmost(hwnd);
        }

        private void UpdateBorderForWindow(IntPtr hwnd, bool isTopmost)
        {
            if (isTopmost && _config.HighlightTopmostWithBorder)
            {
                if (!_activeBorders.TryGetValue(hwnd, out var border))
                {
                    border = new TopmostBorderOverlayWindow(hwnd, _config.TopmostBorderColor, _config.TopmostBorderWidth);
                    _activeBorders[hwnd] = border;
                    border.Show();
                }
                else
                {
                    border.ApplyStyle(_config.TopmostBorderColor, _config.TopmostBorderWidth);
                    border.UpdateBounds();
                }
            }
            else
            {
                if (_activeBorders.TryGetValue(hwnd, out var border))
                {
                    border.Close();
                    _activeBorders.Remove(hwnd);
                }
            }
        }

        private void SyncBorderOverlays()
        {
            if (!_config.HighlightTopmostWithBorder)
            {
                foreach (var border in _activeBorders.Values)
                {
                    try { border.Close(); } catch { }
                }
                _activeBorders.Clear();
                return;
            }

            var deadHwnds = new List<IntPtr>();
            foreach (var kvp in _activeBorders)
            {
                if (!User32.IsWindow(kvp.Key) || !_topmostService.IsTopmost(kvp.Key))
                {
                    deadHwnds.Add(kvp.Key);
                }
                else if (CurrentPipTargetHwnd != IntPtr.Zero && kvp.Key == CurrentPipTargetHwnd)
                {
                    kvp.Value.Hide();
                }
                else
                {
                    kvp.Value.ApplyStyle(_config.TopmostBorderColor, _config.TopmostBorderWidth);
                    kvp.Value.UpdateBounds();
                }
            }

            foreach (var hwnd in deadHwnds)
            {
                if (_activeBorders.TryGetValue(hwnd, out var border))
                {
                    border.Close();
                    _activeBorders.Remove(hwnd);
                }
            }

            // Ensure all pinned windows have active borders (except the window currently in PiP)
            foreach (var hwnd in _topmostService.PinnedWindows)
            {
                if (CurrentPipTargetHwnd != IntPtr.Zero && hwnd == CurrentPipTargetHwnd) continue;
                if (User32.IsWindow(hwnd) && !_activeBorders.ContainsKey(hwnd))
                {
                    var border = new TopmostBorderOverlayWindow(hwnd, _config.TopmostBorderColor, _config.TopmostBorderWidth);
                    _activeBorders[hwnd] = border;
                    border.Show();
                }
            }

            // Sync and clean dead dark mode overlays
            var deadDarkHwnds = new List<IntPtr>();
            foreach (var kvp in _activeDarkOverlays)
            {
                if (!User32.IsWindow(kvp.Key))
                {
                    deadDarkHwnds.Add(kvp.Key);
                }
                else if (kvp.Value.IsVisible)
                {
                    kvp.Value.UpdateBounds();
                }
            }

            foreach (var hwnd in deadDarkHwnds)
            {
                if (_activeDarkOverlays.TryGetValue(hwnd, out var overlay))
                {
                    overlay.Close();
                    _activeDarkOverlays.Remove(hwnd);
                }
            }
        }

        private void UpdateDarkModeForWindow(IntPtr hwnd, bool isDark)
        {
            if (isDark)
            {
                if (!_activeDarkOverlays.TryGetValue(hwnd, out var overlay))
                {
                    IntPtr toolbarHwnd = _toolbarView != null ? new WindowInteropHelper(_toolbarView).Handle : IntPtr.Zero;
                    overlay = new DarkModeOverlayWindow(hwnd, toolbarHwnd);
                    _activeDarkOverlays[hwnd] = overlay;
                }
                else
                {
                    overlay.SetVisible(true);
                }

                // Synchronize active window opacity to dark overlay
                byte currentAlpha = _opacityService.GetConfiguredOpacity(hwnd);
                overlay.SetOpacity(currentAlpha);
            }
            else
            {
                if (_activeDarkOverlays.TryGetValue(hwnd, out var overlay))
                {
                    // Ultra-fast hide (0.17ms) retaining overlay pool
                    overlay.SetVisible(false);
                }
            }
        }

        private void TrackingService_ForegroundWindowChanged(object? sender, ManagedWindow? window)
        {
            Dispatcher.Invoke(() =>
            {
                if (_config.ShowTitleBarToolbar && _toolbarView != null)
                {
                    if (window != null && (IsOwnProcessOrWindow(window.Hwnd) || ShellSanitizer.IsShellOrTaskbarWindow(window.Hwnd) || (CurrentPipTargetHwnd != IntPtr.Zero && window.Hwnd == CurrentPipTargetHwnd)))
                    {
                        return; // Do not attach toolbar to our own settings/pip/overlay windows or taskbar/shell windows or active PiP target
                    }

                    // If cursor is currently over or interacting with ANY active Click-Through window,
                    // do not abruptly detach the toolbar to an underlying background window!
                    if (User32.GetCursorPos(out var pt))
                    {
                        foreach (var ctHwnd in _opacityService.ActiveClickThroughWindows)
                        {
                            if (ctHwnd == CurrentPipTargetHwnd) continue;
                            if (User32.IsWindow(ctHwnd) && !User32.IsIconic(ctHwnd) && User32.GetWindowRect(ctHwnd, out var ctBounds))
                            {
                                if (pt.X >= ctBounds.Left && pt.X <= ctBounds.Right &&
                                    pt.Y >= ctBounds.Top && pt.Y <= ctBounds.Bottom)
                                {
                                    var ctManaged = new ManagedWindow(ctHwnd);
                                    _toolbarView.AttachTo(ctManaged, keepVisible: true);
                                    return; // Retain toolbar on the click-through window!
                                }
                            }
                        }
                    }

                    _toolbarView.AttachTo(window);
                }
            });
        }

        private void TrackingService_WindowBoundsChanged(object? sender, RECT bounds)
        {
            Dispatcher.Invoke(() =>
            {
                if (_trackingService.CurrentWindow != null && CurrentPipTargetHwnd != IntPtr.Zero && _trackingService.CurrentWindow.Hwnd == CurrentPipTargetHwnd)
                {
                    return;
                }

                if (_toolbarView != null && _toolbarView.IsVisible)
                {
                    if (bounds.Left > -10000 && bounds.Width > 50 && bounds.Height > 50)
                    {
                        _toolbarView.UpdatePosition(bounds);
                    }
                }

                if (_trackingService.CurrentWindow != null && _activeBorders.TryGetValue(_trackingService.CurrentWindow.Hwnd, out var border))
                {
                    if (bounds.Left > -10000 && bounds.Width > 50 && bounds.Height > 50)
                    {
                        border.UpdateBounds();
                    }
                }

                if (_trackingService.CurrentWindow != null && _activeDarkOverlays.TryGetValue(_trackingService.CurrentWindow.Hwnd, out var darkOverlay))
                {
                    if (darkOverlay.IsVisible && bounds.Left > -10000 && bounds.Width > 50 && bounds.Height > 50)
                    {
                        darkOverlay.UpdateBounds();
                    }
                }
            });
        }

        private void TogglePip(ManagedWindow target)
        {
            Dispatcher.Invoke(() =>
            {
                // Strict single PiP policy: If an active PiP exists, toggle it OFF and restore original window!
                if (_currentPipWindow != null)
                {
                    _currentPipWindow.RestoreAndClose();
                    _currentPipWindow = null;
                    CurrentPipTargetHwnd = IntPtr.Zero;
                    return;
                }

                if (IsOwnProcessOrWindow(target.Hwnd)) return;

                CurrentPipTargetHwnd = target.Hwnd;

                // Hide floating toolbar so it does not float orphaned over the ghosted window
                _toolbarView?.Hide();

                // If target had a full-screen border overlay, hide it during PiP
                bool isTopmost = _topmostService.IsTopmost(target.Hwnd);
                if (_activeBorders.TryGetValue(target.Hwnd, out var border))
                {
                    border.Hide();
                }

                // Create clean PiP transferring topmost accent border to PiP window itself
                _currentPipWindow = new PipWindow(
                    target,
                    _pipManager,
                    _opacityService,
                    _config,
                    cropRect: null,
                    hideOriginalWindow: true,
                    isTopmost: isTopmost,
                    borderColor: _config.TopmostBorderColor,
                    borderWidth: _config.TopmostBorderWidth);

                _currentPipWindow.PipClosed += (s, e) =>
                {
                    _currentPipWindow = null;
                    CurrentPipTargetHwnd = IntPtr.Zero;
                    Dispatcher.Invoke(() =>
                    {
                        if (User32.IsWindow(target.Hwnd))
                        {
                            if (_topmostService.IsTopmost(target.Hwnd) && _config.HighlightTopmostWithBorder)
                            {
                                UpdateBorderForWindow(target.Hwnd, true);
                            }
                            _toolbarView?.AttachTo(target, keepVisible: true);
                        }
                    });
                };
                _currentPipWindow.Show();
            });
        }

        private void OpenCropWindow(ManagedWindow target)
        {
            Dispatcher.Invoke(() =>
            {
                if (IsOwnProcessOrWindow(target.Hwnd)) return;

                // Toggle behavior: If Crop is already active, close it (cancel crop, just like Escape!)
                if (_currentCropOverlay != null)
                {
                    try { _currentCropOverlay.Close(); } catch { }
                    _currentCropOverlay = null;
                    return;
                }

                // If already in PiP, toggle it off first
                if (_currentPipWindow != null)
                {
                    _currentPipWindow.RestoreAndClose();
                    _currentPipWindow = null;
                    CurrentPipTargetHwnd = IntPtr.Zero;
                }

                _currentCropOverlay = new CropOverlayWindow(target, (cropRect) =>
                {
                    _currentCropOverlay = null;
                    bool hideOriginal = _config.HideOriginalWindowOnCrop;
                    _toolbarView?.Hide();
                    CurrentPipTargetHwnd = target.Hwnd;

                    bool isTopmost = _topmostService.IsTopmost(target.Hwnd);
                    if (_activeBorders.TryGetValue(target.Hwnd, out var border))
                    {
                        border.Hide();
                    }

                    _currentPipWindow = new PipWindow(
                        target,
                        _pipManager,
                        _opacityService,
                        _config,
                        cropRect,
                        hideOriginalWindow: hideOriginal,
                        isTopmost: isTopmost,
                        borderColor: _config.TopmostBorderColor,
                        borderWidth: _config.TopmostBorderWidth);

                    _currentPipWindow.PipClosed += (s, e) =>
                    {
                        _currentPipWindow = null;
                        CurrentPipTargetHwnd = IntPtr.Zero;
                        Dispatcher.Invoke(() =>
                        {
                            if (User32.IsWindow(target.Hwnd))
                            {
                                if (_topmostService.IsTopmost(target.Hwnd) && _config.HighlightTopmostWithBorder)
                                {
                                    UpdateBorderForWindow(target.Hwnd, true);
                                }
                                _toolbarView?.AttachTo(target, keepVisible: true);
                            }
                        });
                    };
                    _currentPipWindow.Show();
                });
                _currentCropOverlay.Closed += (s, e) => _currentCropOverlay = null;
                _currentCropOverlay.Show();
            });
        }

        private void OpenSettings()
        {
            Dispatcher.Invoke(() =>
            {
                if (_activeSettingsWindow != null && _activeSettingsWindow.IsLoaded)
                {
                    _activeSettingsWindow.Activate();
                    IntPtr h = new WindowInteropHelper(_activeSettingsWindow).Handle;
                    User32.SetWindowPos(h, User32.HWND_TOPMOST, 0, 0, 0, 0, User32.SWP_NOMOVE | User32.SWP_NOSIZE);
                    User32.BringWindowToTop(h);
                    User32.SetForegroundWindow(h);
                    return;
                }

                _activeSettingsWindow = new SettingsWindow(_config);
                _activeSettingsWindow.Closed += (s, e) => _activeSettingsWindow = null;
                _activeSettingsWindow.ShowDialog();
                _activeSettingsWindow = null;

                if (_toolbarView != null)
                {
                    _toolbarView.ApplyConfigStyles();
                }

                SyncBorderOverlays();
            });
        }

        private void StartPeriodicUpdateChecks()
        {
            var initialTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            initialTimer.Tick += async (s, e) =>
            {
                initialTimer.Stop();
                await TriggerBackgroundUpdateCheckAsync();
            };
            initialTimer.Start();

            _updateCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromHours(24)
            };
            _updateCheckTimer.Tick += async (s, e) =>
            {
                await TriggerBackgroundUpdateCheckAsync();
            };
            _updateCheckTimer.Start();
        }

        private async System.Threading.Tasks.Task TriggerBackgroundUpdateCheckAsync()
        {
            if (!_config.AutoCheckForUpdates) return;

            try
            {
                var currentVer = new Version(1, 1, 0);
                var info = await _updateService.CheckForUpdatesAsync(currentVer).ConfigureAwait(false);

                if (info != null && info.IsNewer)
                {
                    if (!string.Equals(_config.IgnoredUpdateVersion, info.TagName, StringComparison.OrdinalIgnoreCase))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var dlg = new UpdateDialog(info, _updateService, _config);
                            dlg.Show();
                        });
                    }
                }

                _config.LastUpdateCheckTime = DateTime.UtcNow;
                _config.Save();
            }
            catch (Exception ex)
            {
                Log($"Background update check warning: {ex.Message}");
            }
        }

        public void RestoreAllExternalWindows()
        {
            Log("Restoring all external windows to clean default state...");
            try
            {
                _topmostService?.RestoreAllPinnedWindows();
            }
            catch (Exception ex) { Log($"Restore topmost warning: {ex.Message}"); }

            try
            {
                _opacityService?.RestoreAllWindows();
            }
            catch (Exception ex) { Log($"Restore opacity warning: {ex.Message}"); }

            try
            {
                foreach (var border in _activeBorders.Values)
                {
                    try { border.Close(); } catch { }
                }
                _activeBorders.Clear();
            }
            catch { }

            try
            {
                foreach (var overlay in _activeDarkOverlays.Values)
                {
                    try { overlay.Close(); } catch { }
                }
                _activeDarkOverlays.Clear();
            }
            catch { }

            try
            {
                if (_currentCropOverlay != null)
                {
                    try { _currentCropOverlay.Close(); } catch { }
                    _currentCropOverlay = null;
                }
            }
            catch { }

            try
            {
                if (_currentPipWindow != null)
                {
                    try { _currentPipWindow.RestoreAndClose(); } catch { }
                    _currentPipWindow = null;
                }
            }
            catch { }

            Log("All external windows restored successfully.");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log("App OnExit called");
            _pinEnforcerTimer?.Stop();
            _updateCheckTimer?.Stop();
            _trackingService?.Stop();
            _trackingService?.Dispose();
            _hotkeyService?.Dispose();
            _darkModeService?.Dispose();
            _messageHwndSource?.Dispose();

            RestoreAllExternalWindows();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();

            base.OnExit(e);
        }
    }
}
