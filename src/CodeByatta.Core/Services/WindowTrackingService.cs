using System;
using System.Collections.Generic;
using System.Diagnostics;
using CodeByatta.Core.Interop;
using CodeByatta.Core.Models;

namespace CodeByatta.Core.Services
{
    public class WindowTrackingService : IDisposable
    {
        private IntPtr _hForegroundHook;
        private IntPtr _hLocationHook;
        private readonly User32.WinEventDelegate _winEventProc;

        private readonly HashSet<string> _excludedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "ShellExperienceHost",
            "SearchHost",
            "LockApp",
            "StartMenuExperienceHost",
            "TextInputHost",
            "ScreenClippingHost",
            "SnippingTool",
            "Microsoft.ScreenSketch",
            "SnippingToolApp"
        };

        private readonly HashSet<string> _excludedClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            "NotifyIconOverflowWindow",
            "TopLevelWindowForOverflowXamlIsland",
            "TaskListThumbnailWnd",
            "ControlCenterWindow",
            "QuickSettingsWindow",
            "XamlExplorerHostIslandWindow",
            "Windows.Internal.Shell.TabProxyWindow",
            "Progman",
            "WorkerW"
        };

        public ManagedWindow? CurrentWindow { get; private set; }

        public event EventHandler<ManagedWindow?>? ForegroundWindowChanged;
        public event EventHandler<RECT>? WindowBoundsChanged;

        public WindowTrackingService(IEnumerable<string>? customExclusions = null)
        {
            _winEventProc = OnWinEvent;
            if (customExclusions != null)
            {
                foreach (var proc in customExclusions)
                {
                    // Do not allow raw "explorer" exclusion to block folder windows
                    if (string.Equals(proc, "explorer", StringComparison.OrdinalIgnoreCase)) continue;
                    _excludedProcesses.Add(proc);
                }
            }

            try
            {
                _excludedProcesses.Add(Process.GetCurrentProcess().ProcessName);
            }
            catch { }
        }

        public void Start()
        {
            if (_hForegroundHook != IntPtr.Zero) return;

            _hForegroundHook = User32.SetWinEventHook(
                User32.EVENT_SYSTEM_FOREGROUND,
                User32.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventProc,
                0,
                0,
                User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS);

            _hLocationHook = User32.SetWinEventHook(
                User32.EVENT_OBJECT_LOCATIONCHANGE,
                User32.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero,
                _winEventProc,
                0,
                0,
                User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS);

            UpdateForegroundWindow(User32.GetForegroundWindow());
        }

        public void Stop()
        {
            if (_hForegroundHook != IntPtr.Zero)
            {
                User32.UnhookWinEvent(_hForegroundHook);
                _hForegroundHook = IntPtr.Zero;
            }

            if (_hLocationHook != IntPtr.Zero)
            {
                User32.UnhookWinEvent(_hLocationHook);
                _hLocationHook = IntPtr.Zero;
            }

            CurrentWindow = null;
        }

        private void OnWinEvent(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd)) return;

            if (eventType == User32.EVENT_SYSTEM_FOREGROUND)
            {
                UpdateForegroundWindow(hwnd);
            }
            else if (eventType == User32.EVENT_OBJECT_LOCATIONCHANGE)
            {
                if (CurrentWindow != null && CurrentWindow.Hwnd == hwnd && idObject == 0)
                {
                    if (CurrentWindow.GetBounds(out RECT bounds))
                    {
                        WindowBoundsChanged?.Invoke(this, bounds);
                    }
                }
            }
        }

        public void UpdateForegroundWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd) || !User32.IsWindowVisible(hwnd) || ShellSanitizer.IsShellOrTaskbarWindow(hwnd))
            {
                CurrentWindow = null;
                ForegroundWindowChanged?.Invoke(this, null);
                return;
            }

            var managed = new ManagedWindow(hwnd);

            // Filter shell background and taskbar classes
            if (_excludedClasses.Contains(managed.ClassName))
            {
                CurrentWindow = null;
                ForegroundWindowChanged?.Invoke(this, null);
                return;
            }

            // Exclude Explorer shell UI (Taskbar, overflow tray flyout, Start, etc.) except real folder windows
            if (string.Equals(managed.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(managed.ClassName, "CabinetWClass", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(managed.ClassName, "ExploreWClass", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentWindow = null;
                    ForegroundWindowChanged?.Invoke(this, null);
                    return;
                }
            }

            // Filter system background processes
            if (_excludedProcesses.Contains(managed.ProcessName))
            {
                CurrentWindow = null;
                ForegroundWindowChanged?.Invoke(this, null);
                return;
            }

            CurrentWindow = managed;
            ForegroundWindowChanged?.Invoke(this, CurrentWindow);

            if (CurrentWindow.GetBounds(out RECT bounds))
            {
                WindowBoundsChanged?.Invoke(this, bounds);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
