using Microsoft.Playwright;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PdfDownloaderWPF.Services
{
    public class BrowserDownloadService : IBrowserDownloadService
    {
        public async Task<(bool Success, string? FileName, string? Error)>
            TryDownloadWithPlaywrightAsync(
                string url,
                int recordId,
                string outputFolder,
                Func<string, int, string> fileNameFunc)
        {

            using var playwright = await Playwright.CreateAsync();
            var browserResult = await TryBrowserDownloadAsync(
                playwright, url, recordId, outputFolder, fileNameFunc);

            return browserResult;
        }

        private static async Task<(bool Success, string? FileName, string? Error)>
            TryBrowserDownloadAsync(
                IPlaywright playwright,
                string url,
                int recordId,
                string outputFolder,
                Func<string, int, string> fileNameFunc)
        {
            try
            {
                await using var browser = await playwright.Chromium.LaunchAsync(new()
                {
                    Headless = true
                });

                // Handles sites that trigger a download dialog when PDF is served
                try
                {
                    var context = await browser.NewContextAsync(new()
                    {
                        AcceptDownloads = true,
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120"
                    });

                    var page = await context.NewPageAsync();
                    var downloadTask = page.WaitForDownloadAsync(new() { Timeout = 10000 });

                    await page.GotoAsync(url, new()
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 15000
                    });

                    var download = await downloadTask;

                    using var stream = await download.CreateReadStreamAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    var bytes = ms.ToArray();

                    if (IsPdf(bytes))
                        return await SaveBytesAsync(bytes, url, recordId, outputFolder, fileNameFunc);

                    await context.CloseAsync();
                }
                catch
                {
                }

                // Handles sites that render PDFs inline (no download dialog triggered)
                try
                {
                    byte[]? pdfBytes = null;

                    var context = await browser.NewContextAsync(new()
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120"
                    });

                    var page = await context.NewPageAsync();

                    await page.RouteAsync("**/*", async route =>
                    {
                        try
                        {
                            var response = await route.FetchAsync();

                            if (pdfBytes == null)
                            {
                                response.Headers.TryGetValue("content-type", out var ct);
                                if (ct != null && ct.Contains("pdf", StringComparison.OrdinalIgnoreCase))
                                    pdfBytes = await response.BodyAsync();
                            }

                            await route.FulfillAsync(new() { Response = response });
                        }
                        catch
                        {
                            await route.ContinueAsync();
                        }
                    });

                    try
                    {
                        await page.GotoAsync(url, new()
                        {
                            WaitUntil = WaitUntilState.NetworkIdle,
                            Timeout = 30000
                        });
                    }
                    catch 
                    { 
                    }

                    if (pdfBytes == null)
                        await Task.Delay(2000);

                    await context.CloseAsync();

                    if (pdfBytes != null && IsPdf(pdfBytes))
                        return await SaveBytesAsync(pdfBytes, url, recordId, outputFolder, fileNameFunc);
                }
                catch
                {
                }

                return (false, null, "Browser could not capture a PDF from this URL");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private static async Task<(bool, string?, string?)> SaveBytesAsync(
            byte[] bytes, string url, int recordId, string outputFolder, Func<string, int, string> fileNameFunc)
        {
            var fileName = fileNameFunc(url, recordId);
            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                fileName += ".pdf";

            var filePath = Path.Combine(outputFolder, fileName);
            await File.WriteAllBytesAsync(filePath, bytes);

            return (true, fileName, null);
        }

        private static bool IsPdf(byte[] bytes)
        {
            return bytes != null &&
                   bytes.Length >= 4 &&
                   bytes[0] == '%' &&
                   bytes[1] == 'P' &&
                   bytes[2] == 'D' &&
                   bytes[3] == 'F';
        }
    }
}