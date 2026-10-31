using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Integration.Tests
{
    public class MagnificationE2ETests
    {
        [Fact]
        public void DarkMode_ColorInversion_EnablesAndDisables()
        {
            using var darkMode = new DarkModeService();

            // Toggle Inverted Dark Mode
            bool enableSuccess = darkMode.EnableInvertedDarkMode();
            Assert.True(enableSuccess);
            Assert.True(darkMode.IsDarkModeActive);

            // Restore normal color profile
            bool disableSuccess = darkMode.DisableDarkMode();
            Assert.True(disableSuccess);
            Assert.False(darkMode.IsDarkModeActive);
        }
    }
}
