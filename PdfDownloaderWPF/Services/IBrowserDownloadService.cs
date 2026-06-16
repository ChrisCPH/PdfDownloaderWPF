using System;
using System.Collections.Generic;
using System.Text;

namespace PdfDownloaderWPF.Services
{
    public interface IBrowserDownloadService
    {
        Task<(bool Success, string? FileName, string? Error)> TryDownloadWithPlaywrightAsync(
            string url, int recordId, string outputFolder, Func<string, int, string> fileNameFunc);
    }
}
