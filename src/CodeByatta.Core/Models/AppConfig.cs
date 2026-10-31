using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CodeByatta.Core.Models
{
    public class AppConfig
    {
        public bool StartWithWindows { get; set; } = true;
        public byte DefaultOpacityPercent { get; set; } = 80;
        public bool ShowTitleBarToolbar { get; set; } = true;
        public bool AutoHideToolbar { get; set; } = true;
        public bool RequireClickToExpandToolbar { get; set; } = false;
        public int HoverTimeMs { get; set; } = 300;
        public bool IntegrateWithMinimizeButton { get; set; } = false;

        // Custom Toolbar Position Defaults
        public double CustomToolbarOffsetX { get; set; } = 0;
        public double CustomToolbarOffsetY { get; set; } = 0;

        // Opacity Features (Matching WindowTop DisableWhenMouseAbove)
        public bool DisableOpacityOnMouseOver { get; set; } = true;

        // Auto Updates & Release Notifications
        public bool AutoCheckForUpdates { get; set; } = true;
        public DateTime? LastUpdateCheckTime { get; set; } = null;
        public string IgnoredUpdateVersion { get; set; } = string.Empty;

        // Per-Window Preferences & Location Memory
        public bool AutoRememberWindowPreferences { get; set; } = true;
        public Dictionary<string, WindowPreferences> WindowSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // Set Top Feature (Screenshot 1)
        public bool HighlightTopmostWithBorder { get; set; } = true;
        public bool DisableTopmostOnMaximized { get; set; } = false;
        public string TopmostBorderColor { get; set; } = "#FF0000"; // Red
        public double TopmostBorderWidth { get; set; } = 3.07;
        public bool ShowNotificationOnHotkey { get; set; } = true;

        // PiP Crop (Screenshot 1)
        public bool HideOriginalWindowOnCrop { get; set; } = true;

        // Toolbar Button Visibility (Screenshot 4)
        public bool ShowSetTopButton { get; set; } = true;
        public bool ShowAnchorButton { get; set; } = true;
        public bool ShowPipButton { get; set; } = true;
        public bool ShowCropButton { get; set; } = true;
        public bool ShowDarkModeButton { get; set; } = true;
        public bool ShowOpacityButton { get; set; } = true;
        public bool ShowClickThroughButton { get; set; } = true;
        public bool ShowHoverOpacityButton { get; set; } = true;
        public bool ShowCinemaFocusButton { get; set; } = true;
        public bool ShowGlassButton { get; set; } = false;
        public bool EnablePipCornerSnapping { get; set; } = true;
        public bool EnablePipWheelResize { get; set; } = true;
        public double CinemaFocusDimLevel { get; set; } = 0.65;
        public string ArrowColor { get; set; } = "#30D158"; // Green / Accent

        // Hotkeys & Mouse Wheel (Screenshot 2)
        public HotkeyConfig Hotkeys { get; set; } = new HotkeyConfig();
        public string MouseWheelOpacityModifier { get; set; } = "Alt"; // "Disabled", "Alt", "Ctrl"
        public int MouseWheelSensitivity { get; set; } = 10;

        // Exclusions & Game Mode (Screenshot 3)
        public bool IsWhitelistMode { get; set; } = false;
        public bool DisableOnGameMode { get; set; } = true;
        public List<string> ExcludedProcesses { get; set; } = new List<string>
        {
            "ShellExperienceHost",
            "SearchHost",
            "LockApp",
            "StartMenuExperienceHost",
            "TextInputHost",
            "OpenWindow",
            "CodeByatta.UI"
        };

        public WindowPreferences GetOrCreateWindowPreference(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) processName = "default";
            // Map explorer folder windows to explorer_folder to isolate them from shell
            if (string.Equals(processName, "explorer", StringComparison.OrdinalIgnoreCase))
            {
                processName = "explorer_folder";
            }

            if (!WindowSettings.TryGetValue(processName, out var pref))
            {
                pref = new WindowPreferences { ProcessName = processName };
                WindowSettings[processName] = pref;
            }
            return pref;
        }

        public void SanitizePreferences()
        {
            if (WindowSettings.TryGetValue("explorer", out var expPref))
            {
                expPref.OpacityLevel = 255;
                expPref.IsTopmost = false;
                expPref.IsClickThrough = false;

                if (!WindowSettings.ContainsKey("explorer_folder"))
                {
                    expPref.ProcessName = "explorer_folder";
                    WindowSettings["explorer_folder"] = expPref;
                }
                WindowSettings.Remove("explorer");
                Save();
            }
        }

        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeByatta",
            "OpenWindow");

        private static readonly string LegacyConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodeByatta",
            "WindowsTop");

        private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                    if (cfg != null)
                    {
                        cfg.SanitizePreferences();
                        return cfg;
                    }
                }

                // Smooth migration from legacy WindowsTop config path if present
                string legacyPath = Path.Combine(LegacyConfigDir, "config.json");
                if (File.Exists(legacyPath))
                {
                    string json = File.ReadAllText(legacyPath);
                    var legacyConfig = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                    if (legacyConfig != null)
                    {
                        legacyConfig.SanitizePreferences();
                        legacyConfig.Save();
                        return legacyConfig;
                    }
                }
            }
            catch
            {
                // Never immediately overwrite corrupt or partial config! Backup first!
                try
                {
                    if (File.Exists(ConfigPath))
                    {
                        string corruptBackup = ConfigPath + $".bak_{DateTime.UtcNow:yyyyMMddHHmmss}";
                        File.Copy(ConfigPath, corruptBackup, true);
                    }
                }
                catch { }
            }

            var config = new AppConfig();
            config.Save();
            return config;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                string json = JsonSerializer.Serialize(this, JsonOpts);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Silently ignore write failures in non-critical environments
            }
        }
    }

    public class HotkeyConfig
    {
        public uint Modifier { get; set; } = 0x0001; // MOD_ALT
        
        public uint TopmostKey { get; set; } = 0x5A;      // 'Z' -> Alt+Z (Screenshot 2)
        public uint OpacityKey { get; set; } = 0x41;      // 'A' -> Alt+A
        public uint ClickThroughKey { get; set; } = 0x43; // 'C' -> Alt+C
        public uint PipKey { get; set; } = 0x51;          // 'Q' -> Alt+Q
        public uint DarkModeKey { get; set; } = 0x44;     // 'D' -> Alt+D
        public uint CinemaFocusKey { get; set; } = 0x46;  // 'F' -> Alt+F
    }
}
