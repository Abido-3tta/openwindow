using System;
using System.Text.Json;
using CodeByatta.Core.Models;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class WindowPreferencesTests
    {
        [Fact]
        public void WindowPreferences_DefaultValues_AreCenteredAndInactive()
        {
            var pref = new WindowPreferences();

            Assert.Equal(0.5, pref.ArrowCenterRatio);
            Assert.False(pref.HaveArrowCenterRatio);
            Assert.Equal(0.0, pref.SideOffset);
            Assert.False(pref.HaveOffset);
            Assert.False(pref.IsCollapsed);
            Assert.False(pref.IsTopmost);
            Assert.Equal(255, pref.OpacityLevel);
            Assert.True(pref.DisableOpacityOnMouseOver);
            Assert.False(pref.IsClickThrough);
            Assert.False(pref.IsDarkMode);
        }

        [Fact]
        public void WindowPreferences_JsonRoundTrip_PreservesAllFields()
        {
            var original = new WindowPreferences
            {
                ProcessName = "chrome",
                ArrowCenterRatio = 0.62,
                HaveArrowCenterRatio = true,
                SideOffset = 0,
                HaveOffset = true,
                IsCollapsed = true,
                IsTopmost = true,
                OpacityLevel = 204,
                DisableOpacityOnMouseOver = true,
                IsClickThrough = false,
                IsDarkMode = false,
                LastModified = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc)
            };

            string json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<WindowPreferences>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("chrome", deserialized.ProcessName);
            Assert.Equal(0.62, deserialized.ArrowCenterRatio);
            Assert.True(deserialized.HaveArrowCenterRatio);
            Assert.True(deserialized.IsCollapsed);
            Assert.True(deserialized.IsTopmost);
            Assert.Equal(204, deserialized.OpacityLevel);
        }
    }
}
