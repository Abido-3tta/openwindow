using System;
using System.Runtime.InteropServices;

namespace CodeByatta.Core.Interop
{
    public static class MagnificationApi
    {
        public const string WC_MAGNIFIER = "Magnifier";
        public const int MW_FILTERMODE_EXCLUDE = 0;
        public const int MW_FILTERMODE_INCLUDE = 1;

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagInitialize();

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagUninitialize();

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagSetFullscreenColorEffect(ref MAGCOLOREFFECT pEffect);

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagGetFullscreenColorEffect(out MAGCOLOREFFECT pEffect);

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagSetColorEffect(IntPtr hwnd, ref MAGCOLOREFFECT pEffect);

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagGetColorEffect(IntPtr hwnd, out MAGCOLOREFFECT pEffect);

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagSetWindowSource(IntPtr hwnd, RECT rect);

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagGetWindowSource(IntPtr hwnd, out RECT pRect);

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM pTransform);

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagGetWindowTransform(IntPtr hwnd, out MAGTRANSFORM pTransform);

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MagSetWindowFilterList(IntPtr hwnd, int dwFilterMode, int count, [In] IntPtr[] pHWND);

        [DllImport("Magnification.dll", ExactSpelling = true, SetLastError = true)]
        public static extern int MagGetWindowFilterList(IntPtr hwnd, out int pdwFilterMode, int count, [Out] IntPtr[] pHWND);

        public static readonly MAGCOLOREFFECT InvertedEffect = new MAGCOLOREFFECT
        {
            transform = new float[]
            {
                -1.0f,  0.0f,  0.0f, 0.0f, 0.0f,
                 0.0f, -1.0f,  0.0f, 0.0f, 0.0f,
                 0.0f,  0.0f, -1.0f, 0.0f, 0.0f,
                 0.0f,  0.0f,  0.0f, 1.0f, 0.0f,
                 1.0f,  1.0f,  1.0f, 0.0f, 1.0f
            }
        };

        public static readonly MAGCOLOREFFECT IdentityEffect = new MAGCOLOREFFECT
        {
            transform = new float[]
            {
                1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 0.0f, 1.0f
            }
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MAGCOLOREFFECT
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public float[] transform;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MAGTRANSFORM
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public float[] v;

        public static MAGTRANSFORM Identity => new MAGTRANSFORM
        {
            v = new float[]
            {
                1.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 1.0f
            }
        };
    }
}
