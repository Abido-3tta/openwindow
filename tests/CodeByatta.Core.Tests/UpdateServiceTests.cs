using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeByatta.Core.Services;
using Xunit;

namespace CodeByatta.Core.Tests
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    public class UpdateServiceTests
    {
        [Fact]
        public void UpdateInfo_IsNewer_CalculatesCorrectly()
        {
            var info1 = new UpdateInfo
            {
                CurrentVersion = new Version(1, 1, 0),
                RemoteVersion = new Version(1, 2, 0)
            };
            Assert.True(info1.IsNewer);

            var info2 = new UpdateInfo
            {
                CurrentVersion = new Version(1, 2, 0),
                RemoteVersion = new Version(1, 2, 0)
            };
            Assert.False(info2.IsNewer);

            var info3 = new UpdateInfo
            {
                CurrentVersion = new Version(2, 0, 0),
                RemoteVersion = new Version(1, 9, 9)
            };
            Assert.False(info3.IsNewer);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ValidRelease_ParsesSuccessfully()
        {
            string fakeJson = @"{
              ""tag_name"": ""v1.2.0"",
              ""name"": ""Release v1.2.0 - Fast Engine"",
              ""body"": ""- Added Titlebar Toolbar\n- Added Click-Through Persistence"",
              ""html_url"": ""https://github.com/codebyatta/openwindow/releases/tag/v1.2.0"",
              ""assets"": [
                {
                  ""name"": ""OpenWindow-v1.2.0-win-x64.zip"",
                  ""size"": 41943040,
                  ""browser_download_url"": ""https://github.com/codebyatta/openwindow/releases/download/v1.2.0/OpenWindow-v1.2.0-win-x64.zip""
                },
                {
                  ""name"": ""OpenWindow-v1.2.0-win-x86.zip"",
                  ""size"": 38797312,
                  ""browser_download_url"": ""https://github.com/codebyatta/openwindow/releases/download/v1.2.0/OpenWindow-v1.2.0-win-x86.zip""
                }
              ]
            }";

            var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(fakeJson, Encoding.UTF8, "application/json")
            });

            var httpClient = new HttpClient(handler);
            var updateService = new UpdateService(httpClient);

            var info = await updateService.CheckForUpdatesAsync(new Version(1, 1, 0));

            Assert.NotNull(info);
            Assert.True(info.IsNewer);
            Assert.Equal("v1.2.0", info.TagName);
            Assert.Equal("Release v1.2.0 - Fast Engine", info.ReleaseTitle);
            Assert.Contains("Titlebar Toolbar", info.ReleaseNotes);
            Assert.False(string.IsNullOrEmpty(info.DownloadUrl));
            Assert.True(info.AssetSizeBytes > 0);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_HttpError_ReturnsNull()
        {
            var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.NotFound));
            var httpClient = new HttpClient(handler);
            var updateService = new UpdateService(httpClient);

            var info = await updateService.CheckForUpdatesAsync(new Version(1, 1, 0));
            Assert.Null(info);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_InvalidJson_ReturnsNull()
        {
            var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>Error 500</html>", Encoding.UTF8, "text/html")
            });
            var httpClient = new HttpClient(handler);
            var updateService = new UpdateService(httpClient);

            var info = await updateService.CheckForUpdatesAsync(new Version(1, 1, 0));
            Assert.Null(info);
        }
    }
}
