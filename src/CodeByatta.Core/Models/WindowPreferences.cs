using System;

namespace CodeByatta.Core.Models
{
    public class WindowPreferences
    {
        public string ProcessName { get; set; } = string.Empty;
        
        // Relative horizontal center ratio (0.0 to 1.0, e.g. 0.5 is centered on window)
        public double ArrowCenterRatio { get; set; } = 0.5;
        public bool HaveArrowCenterRatio { get; set; } = false;

        // Vertical / side offset in DIPs from default docking position
        public double SideOffset { get; set; } = 0;
        public bool HaveOffset { get; set; } = false;

        // Per-window feature memory
        public bool IsCollapsed { get; set; } = false;
        public bool IsTopmost { get; set; } = false;
        public byte OpacityLevel { get; set; } = 255;
        public bool DisableOpacityOnMouseOver { get; set; } = true;
        public bool IsClickThrough { get; set; } = false;
        public bool IsDarkMode { get; set; } = false;
        public bool IsCinemaFocus { get; set; } = false;

        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
}
