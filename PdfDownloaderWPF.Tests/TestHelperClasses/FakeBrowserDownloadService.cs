using PdfDownloader.Services;
using PdfDownloaderWPF.Services;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PdfDownloaderWPF.Tests.TestHelperClasses
{
    public class FakeBrowserDownloadService : IBrowserDownloadService
    {
        public int CallCount { get; private set; }

        public Task<(bool Success, string? FileName, string? Error)>
            TryDownloadWithPlaywrightAsync(
                string url,
                int recordId,
                string outputFolder,
                Func<string, int, string> fileNameFunc)
        {
            CallCount++;
            return Task.FromResult<(bool, string?, string?)>((false, null, "Browser fallback not used"));
        }
    }

    public class FakeBrowserDownloadServiceSuccess : IBrowserDownloadService
    {
        private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 fake browser content");

        public int CallCount { get; private set; }

        public async Task<(bool Success, string? FileName, string? Error)>
            TryDownloadWithPlaywrightAsync(
                string url,
                int recordId,
                string outputFolder,
                Func<string, int, string> fileNameFunc)
        {
            CallCount++;

            var fileName = fileNameFunc(url, recordId);
            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                fileName += ".pdf";

            var filePath = Path.Combine(outputFolder, fileName);
            await File.WriteAllBytesAsync(filePath, PdfBytes);

            return (true, fileName, null);
        }
    }
}