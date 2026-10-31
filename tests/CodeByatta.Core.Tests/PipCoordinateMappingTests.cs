using System;
using CodeByatta.Core.Interop;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class PipCoordinateMappingTests
    {
        [Fact]
        public void MapCroppedCoordinateToScreen_ReturnsExactPhysicalPoints()
        {
            // Given a crop rectangle on screen from (100, 200) to (500, 600) (width=400, height=400)
            RECT cropScreenRect = new RECT(100, 200, 500, 600);

            // Center of PiP thumbnail (relX = 0.5, relY = 0.5)
            double relX = 0.5;
            double relY = 0.5;

            int screenX = cropScreenRect.Left + (int)Math.Round(relX * cropScreenRect.Width);
            int screenY = cropScreenRect.Top + (int)Math.Round(relY * cropScreenRect.Height);

            Assert.Equal(300, screenX);
            Assert.Equal(400, screenY);

            // Top-left of PiP thumbnail (relX = 0.0, relY = 0.0)
            int tlX = cropScreenRect.Left + (int)Math.Round(0.0 * cropScreenRect.Width);
            int tlY = cropScreenRect.Top + (int)Math.Round(0.0 * cropScreenRect.Height);
            Assert.Equal(100, tlX);
            Assert.Equal(200, tlY);

            // Bottom-right of PiP thumbnail (relX = 1.0, relY = 1.0)
            int brX = cropScreenRect.Left + (int)Math.Round(1.0 * cropScreenRect.Width);
            int brY = cropScreenRect.Top + (int)Math.Round(1.0 * cropScreenRect.Height);
            Assert.Equal(500, brX);
            Assert.Equal(600, brY);
        }

        [Fact]
        public void CalculateDwmCropSource_TranslatesScreenToWindowRelative()
        {
            // Source window positioned on screen at (40, 50) with size 1000x800
            RECT windowRect = new RECT(40, 50, 1040, 850);

            // User cropped region on screen at (140, 150, 540, 450)
            RECT cropScreenRect = new RECT(140, 150, 540, 450);

            int dLeft = Math.Max(0, cropScreenRect.Left - windowRect.Left);
            int dTop = Math.Max(0, cropScreenRect.Top - windowRect.Top);
            int dRight = Math.Min(windowRect.Width, dLeft + cropScreenRect.Width);
            int dBottom = Math.Min(windowRect.Height, dTop + cropScreenRect.Height);

            RECT dwmSource = new RECT(dLeft, dTop, dRight, dBottom);

            Assert.Equal(100, dwmSource.Left);
            Assert.Equal(100, dwmSource.Top);
            Assert.Equal(500, dwmSource.Right);
            Assert.Equal(400, dwmSource.Bottom);
            Assert.Equal(400, dwmSource.Width);
            Assert.Equal(300, dwmSource.Height);
        }

        [Fact]
        public void MouseWheel_WParamPacking_PreservesNegativeDelta()
        {
            short delta = -120; // Scroll down
            int keyFlags = User32.MK_CONTROL;

            IntPtr wParam = unchecked((IntPtr)((delta << 16) | (keyFlags & 0xFFFF)));
            long raw = wParam.ToInt64();

            short extractedDelta = (short)((raw >> 16) & 0xFFFF);
            int extractedFlags = (int)(raw & 0xFFFF);

            Assert.Equal(-120, extractedDelta);
            Assert.Equal(User32.MK_CONTROL, extractedFlags);
        }

        [Fact]
        public void MouseMessage_LParamPacking_PreservesCoordinates()
        {
            int screenX = 1440;
            int screenY = 900;

            IntPtr lParam = unchecked((IntPtr)((uint)(((screenY & 0xFFFF) << 16) | (screenX & 0xFFFF))));
            long raw = lParam.ToInt64();

            short lowX = unchecked((short)(raw & 0xFFFF));
            short highY = unchecked((short)((raw >> 16) & 0xFFFF));

            Assert.Equal(1440, lowX);
            Assert.Equal(900, highY);
        }
    }
}
