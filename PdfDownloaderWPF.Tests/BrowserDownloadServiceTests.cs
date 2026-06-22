using PdfDownloader.Services;
using PdfDownloaderWPF.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfDownloaderWPF.Tests
{
    [Trait("Category", "Integration")]
    public class BrowserDownloadServiceIntegrationTests
    {
        [Fact]
        public async Task TryDownloadWithPlaywrightAsync_RealPdfUrl_DownloadsSuccessfully()
        {
            var service = new BrowserDownloadService();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var (success, fileName, error) = await service.TryDownloadWithPlaywrightAsync(
                "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf",
                1,
                tempDir,
                FileNameService.GetFileName);

            Assert.True(success, $"Expected success but got error: {error}");
            Assert.NotNull(fileName);
            Assert.True(File.Exists(Path.Combine(tempDir, fileName!)));

            Directory.Delete(tempDir, true);
        }

        [Fact]
        public async Task TryDownloadWithPlaywrightAsync_InvalidUrl_ReturnsFailure()
        {
            var service = new BrowserDownloadService();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var (success, fileName, error) = await service.TryDownloadWithPlaywrightAsync(
                "https://example.com/this-does-not-exist.pdf",
                2,
                tempDir,
                FileNameService.GetFileName);

            Assert.False(success);
            Assert.NotNull(error);

            Directory.Delete(tempDir, true);
        }
    }
}
