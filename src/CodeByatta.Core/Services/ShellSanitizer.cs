using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using CodeByatta.Core.Interop;

namespace CodeByatta.Core.Services
{
    public static class ShellSanitizer
    {
        private static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
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
            "WorkerW",
            "DV2ControlHost"
        };

        private static readonly HashSet<string> ShellProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "ShellExperienceHost",
            "SearchHost",
            "LockApp",
            "StartMenuExperienceHost",
            "TextInputHost"
        };

        public static bool IsShellOrTaskbarWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd)) return true;

            var sbClass = new StringBuilder(256);
            User32.GetClassName(hwnd, sbClass, sbClass.Capacity);
            string className = sbClass.ToString();

            if (ShellClasses.Contains(className)) return true;

            User32.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return true;

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                string procName = proc.ProcessName;

                if (ShellProcesses.Contains(procName)) return true;

                if (string.Equals(procName, "explorer", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(className, "CabinetWClass", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(className, "ExploreWClass", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Process may have exited or access denied
            }

            return false;
        }

        public static void SanitizeShellWindows()
        {
            string[] targets = new[]
            {
                "NotifyIconOverflowWindow",
                "TopLevelWindowForOverflowXamlIsland",
                "Shell_TrayWnd",
                "Shell_SecondaryTrayWnd"
            };

            foreach (var cls in targets)
            {
                IntPtr hwnd = IntPtr.Zero;
                while ((hwnd = User32.FindWindowEx(IntPtr.Zero, hwnd, cls, null)) != IntPtr.Zero)
                {
                    ResetWindowOpacity(hwnd);
                }
            }
        }

        public static void ResetWindowOpacity(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd)) return;

            IntPtr exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
            long styleLong = (long)exStyle;

            if ((styleLong & User32.WS_EX_LAYERED) != 0 || (styleLong & User32.WS_EX_TRANSPARENT) != 0)
            {
                long cleanedStyle = styleLong & ~User32.WS_EX_LAYERED & ~User32.WS_EX_TRANSPARENT;
                User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, (IntPtr)cleanedStyle);
                User32.SetLayeredWindowAttributes(hwnd, 0, 255, User32.LWA_ALPHA);
                User32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_FRAMECHANGED);
            }
        }

        public static void EnsureTaskbarOnTop()
        {
            IntPtr hTray = User32.FindWindow("Shell_TrayWnd", null);
            if (hTray != IntPtr.Zero && User32.IsWindow(hTray))
            {
                User32.SetWindowPos(hTray, User32.HWND_TOPMOST, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
            }

            IntPtr hSec = IntPtr.Zero;
            while ((hSec = User32.FindWindowEx(IntPtr.Zero, hSec, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                User32.SetWindowPos(hSec, User32.HWND_TOPMOST, 0, 0, 0, 0,
                    User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
            }
        }

        public static void CheckCursorOverTaskbar(User32.POINT cursor)
        {
            IntPtr hTray = User32.FindWindow("Shell_TrayWnd", null);
            if (hTray != IntPtr.Zero && User32.GetWindowRect(hTray, out RECT trayRect))
            {
                if (cursor.X >= trayRect.Left && cursor.X <= trayRect.Right &&
                    cursor.Y >= trayRect.Top - 8 && cursor.Y <= trayRect.Bottom + 8)
                {
                    EnsureTaskbarOnTop();
                    return;
                }
            }

            IntPtr hSec = IntPtr.Zero;
            while ((hSec = User32.FindWindowEx(IntPtr.Zero, hSec, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                if (User32.GetWindowRect(hSec, out RECT secRect))
                {
                    if (cursor.X >= secRect.Left && cursor.X <= secRect.Right &&
                        cursor.Y >= secRect.Top - 8 && cursor.Y <= secRect.Bottom + 8)
                    {
                        EnsureTaskbarOnTop();
                        return;
                    }
                }
            }
        }
    }
}
