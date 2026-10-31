using System;
using System.Diagnostics;
using System.Text;
using CodeByatta.Core.Interop;

namespace CodeByatta.Core.Models
{
    public class ManagedWindow
    {
        public IntPtr Hwnd { get; }
        public string Title { get; private set; } = string.Empty;
        public string ProcessName { get; private set; } = string.Empty;
        public string ClassName { get; private set; } = string.Empty;
        public uint ProcessId { get; private set; }
        public bool IsTopmost { get; set; }
        public byte Opacity { get; set; } = 255;
        public bool IsClickThrough { get; set; }
        public bool IsDarkMode { get; set; }
        public bool IsPipActive { get; set; }

        public ManagedWindow(IntPtr hwnd)
        {
            Hwnd = hwnd;
            RefreshInfo();
        }

        public void RefreshInfo()
        {
            if (!User32.IsWindow(Hwnd))
            {
                Title = string.Empty;
                ProcessName = string.Empty;
                ClassName = string.Empty;
                return;
            }

            var sb = new StringBuilder(512);
            User32.GetWindowText(Hwnd, sb, sb.Capacity);
            Title = sb.ToString();

            var classSb = new StringBuilder(256);
            User32.GetClassName(Hwnd, classSb, classSb.Capacity);
            ClassName = classSb.ToString();

            User32.GetWindowThreadProcessId(Hwnd, out uint pid);
            ProcessId = pid;

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                ProcessName = proc.ProcessName;
            }
            catch
            {
                ProcessName = "Unknown";
            }

            IntPtr exStyle = User32.GetWindowLongPtr(Hwnd, User32.GWL_EXSTYLE);
            long styleVal = (long)exStyle;
            IsTopmost = (styleVal & User32.WS_EX_TOPMOST) != 0;
            IsClickThrough = (styleVal & User32.WS_EX_TRANSPARENT) != 0;

            bool isLayered = (styleVal & User32.WS_EX_LAYERED) != 0;
            if (!isLayered)
            {
                Opacity = 255;
            }
            else if (User32.GetLayeredWindowAttributes(Hwnd, out _, out byte alpha, out uint flags))
            {
                if ((flags & User32.LWA_ALPHA) != 0)
                {
                    Opacity = alpha;
                }
                else
                {
                    Opacity = 255;
                }
            }
            else
            {
                Opacity = 255;
            }
        }

        public bool GetBounds(out RECT bounds)
        {
            if (DwmApi.TryGetExtendedFrameBounds(Hwnd, out bounds))
            {
                return true;
            }
            return User32.GetWindowRect(Hwnd, out bounds);
        }

        public override string ToString()
        {
            return $"[{Hwnd}] {ProcessName} ({ClassName}): {Title}";
        }
    }
}
