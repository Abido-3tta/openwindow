using System;
using System.Runtime.InteropServices;

namespace CodeByatta.Core.Interop
{
    public static class DwmApi
    {
        public const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const uint DWMWA_SYSTEMBACKDROP_TYPE = 38;

        public const uint DWM_TNP_RECTDESTINATION = 0x00000001;
        public const uint DWM_TNP_RECTSOURCE = 0x00000002;
        public const uint DWM_TNP_OPACITY = 0x00000004;
        public const uint DWM_TNP_VISIBLE = 0x00000008;
        public const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr source, out IntPtr hThumbnail);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmUnregisterThumbnail(IntPtr hThumbnail);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnail, ref DWM_THUMBNAIL_PROPERTIES props);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmQueryThumbnailSourceSize(IntPtr hThumbnail, out SIZE size);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        public struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        public static void ApplyAcrylicBackdrop(IntPtr hwnd, bool enable)
        {
            if (hwnd == IntPtr.Zero || !User32.IsWindow(hwnd)) return;

            try
            {
                // Windows 11 22H2+ modern system backdrop: 3 = DWMSBT_TRANSIENTWINDOW (Acrylic), 1 = DWMSBT_DISABLE
                int backdrop = enable ? 3 : 1;
                int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
                if (hr == 0 && enable) return;
            }
            catch { }

            try
            {
                // Fallback for Windows 10 / earlier Windows 11 builds via AccentPolicy
                var accent = new AccentPolicy
                {
                    AccentState = enable ? 4 : 0, // 4 = ACCENT_ENABLE_ACRYLICBLURBEHIND, 0 = ACCENT_DISABLED
                    AccentFlags = 2,
                    GradientColor = enable ? unchecked((int)0x99101218) : 0
                };

                int accentSize = Marshal.SizeOf(accent);
                IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
                try
                {
                    Marshal.StructureToPtr(accent, accentPtr, false);
                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = 19, // WCA_ACCENT_POLICY
                        Data = accentPtr,
                        SizeOfData = accentSize
                    };
                    SetWindowCompositionAttribute(hwnd, ref data);
                }
                finally
                {
                    Marshal.FreeHGlobal(accentPtr);
                }
            }
            catch { }
        }

        public static bool TryGetExtendedFrameBounds(IntPtr hwnd, out RECT bounds)
        {
            int hr = DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out bounds, Marshal.SizeOf<RECT>());
            return hr == 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fVisible;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fSourceClientAreaOnly;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int cx;
        public int cy;

        public SIZE(int x, int y)
        {
            cx = x;
            cy = y;
        }
    }
}
