using System;
using System.Runtime.InteropServices;
using CodeByatta.Core.Interop;

namespace CodeByatta.Core.Services
{
    public class PipManagerService
    {
        public IntPtr RegisterPipThumbnail(IntPtr destHwnd, IntPtr srcHwnd, RECT destRect, RECT? cropRect = null)
        {
            if (destHwnd == IntPtr.Zero || srcHwnd == IntPtr.Zero) return IntPtr.Zero;

            int hr = DwmApi.DwmRegisterThumbnail(destHwnd, srcHwnd, out IntPtr hThumbnail);
            if (hr != 0 || hThumbnail == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DwmApi.DWM_TNP_VISIBLE | DwmApi.DWM_TNP_RECTDESTINATION | DwmApi.DWM_TNP_OPACITY,
                fVisible = true,
                opacity = 255,
                rcDestination = destRect
            };

            if (cropRect.HasValue && cropRect.Value.Width > 0 && cropRect.Value.Height > 0)
            {
                props.dwFlags |= DwmApi.DWM_TNP_RECTSOURCE;
                props.rcSource = cropRect.Value;
            }

            DwmApi.DwmUpdateThumbnailProperties(hThumbnail, ref props);
            return hThumbnail;
        }

        public bool UpdatePipBounds(IntPtr hThumbnail, RECT destRect, RECT? cropRect = null, byte opacity = 255)
        {
            if (hThumbnail == IntPtr.Zero) return false;

            var props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DwmApi.DWM_TNP_VISIBLE | DwmApi.DWM_TNP_RECTDESTINATION | DwmApi.DWM_TNP_OPACITY,
                fVisible = true,
                opacity = opacity,
                rcDestination = destRect
            };

            if (cropRect.HasValue && cropRect.Value.Width > 0 && cropRect.Value.Height > 0)
            {
                props.dwFlags |= DwmApi.DWM_TNP_RECTSOURCE;
                props.rcSource = cropRect.Value;
            }

            int hr = DwmApi.DwmUpdateThumbnailProperties(hThumbnail, ref props);
            return hr == 0;
        }

        public void UnregisterPipThumbnail(IntPtr hThumbnail)
        {
            if (hThumbnail != IntPtr.Zero)
            {
                DwmApi.DwmUnregisterThumbnail(hThumbnail);
            }
        }
    }
}
