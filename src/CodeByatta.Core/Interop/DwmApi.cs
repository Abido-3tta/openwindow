using System;
using System.Runtime.InteropServices;

namespace CodeByatta.Core.Interop
{
    public static class DwmApi
    {
        public const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;

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
