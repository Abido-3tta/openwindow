using CodeByatta.Core.Models;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class AppConfigTests
    {
        [Fact]
        public void AppConfig_Defaults_AreValid()
        {
            var config = new AppConfig();

            Assert.True(config.StartWithWindows);
            Assert.Equal(80, config.DefaultOpacityPercent);
            Assert.True(config.ShowTitleBarToolbar);
            Assert.True(config.AutoHideToolbar);
            Assert.True(config.HighlightTopmostWithBorder);
            Assert.True(config.HideOriginalWindowOnCrop);
            Assert.Equal("#FF0000", config.TopmostBorderColor);
            Assert.Equal(3.07, config.TopmostBorderWidth);
            Assert.Equal("#30D158", config.ArrowColor);
            Assert.True(config.DisableOpacityOnMouseOver);
            Assert.False(config.RequireClickToExpandToolbar);
            Assert.True(config.AutoCheckForUpdates);
            Assert.Null(config.LastUpdateCheckTime);
            Assert.Equal(string.Empty, config.IgnoredUpdateVersion);
            Assert.Equal(0x5A, (int)config.Hotkeys.TopmostKey); // 'Z'
            Assert.NotNull(config.Hotkeys);
            Assert.Contains("ShellExperienceHost", config.ExcludedProcesses);
            Assert.Contains("OpenWindow", config.ExcludedProcesses);
        }

        [Fact]
        public void AppConfig_WindowSettings_CanAddAndRemove()
        {
            var config = new AppConfig();
            var pref = config.GetOrCreateWindowPreference("notepad");
            pref.IsTopmost = true;
            pref.OpacityLevel = 180;
            pref.ArrowCenterRatio = 0.42;

            Assert.True(config.WindowSettings.ContainsKey("notepad"));
            Assert.Equal(180, config.WindowSettings["notepad"].OpacityLevel);

            bool removed = config.WindowSettings.Remove("notepad");
            Assert.True(removed);
            Assert.False(config.WindowSettings.ContainsKey("notepad"));
        }

        [Fact]
        public void AppConfig_SanitizePreferences_CleansExplorerAndMapsToFolder()
        {
            var config = new AppConfig();
            config.WindowSettings["explorer"] = new WindowPreferences
            {
                ProcessName = "explorer",
                OpacityLevel = 153,
                IsTopmost = true,
                IsClickThrough = true
            };

            config.SanitizePreferences();

            Assert.False(config.WindowSettings.ContainsKey("explorer"));
            Assert.True(config.WindowSettings.ContainsKey("explorer_folder"));
            Assert.Equal(255, config.WindowSettings["explorer_folder"].OpacityLevel);
            Assert.False(config.WindowSettings["explorer_folder"].IsTopmost);
            Assert.False(config.WindowSettings["explorer_folder"].IsClickThrough);
        }

        [Fact]
        public void AppConfig_GetOrCreateWindowPreference_MapsExplorerToExplorerFolder()
        {
            var config = new AppConfig();
            var pref = config.GetOrCreateWindowPreference("explorer");

            Assert.Equal("explorer_folder", pref.ProcessName);
            Assert.True(config.WindowSettings.ContainsKey("explorer_folder"));
            Assert.False(config.WindowSettings.ContainsKey("explorer"));
        }
    }
}
