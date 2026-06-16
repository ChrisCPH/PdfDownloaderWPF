using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PdfDownloader.Models;
using PdfDownloader.Services;
using PdfDownloaderWPF.Tests.TestHelperClasses;

namespace PdfDownloaderWPF.Tests;

public class DownloadServiceTests
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 fake content");

    private static HttpResponseMessage PdfResponse(byte[] content, string contentType = "application/pdf")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return response;
    }

    private static HttpResponseMessage HtmlResponse(string html, string? finalUri = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        if (finalUri != null)
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUri);

        return response;
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task DownloadPdfAsync_HtmlWrapperWithPathParam_ResolvesAndDownloadsRealPdf()
    {
        const string originalUrl = "https://example.com/viewer?file=annual.pdf";
        const string wrapperUri = "https://example.com/static-libs/pdf-redirect/index.html?path=/files/annual-real.pdf";
        const string resolvedUrl = "https://example.com/files/annual-real.pdf";

        var handler = new FakeHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            if (uri == originalUrl)
                return HtmlResponse("<html>loading...</html>", finalUri: wrapperUri);
            if (uri == resolvedUrl)
                return PdfResponse(PdfBytes);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 1, PrimaryUrl = originalUrl };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.True(result.Success);
        Assert.Equal(originalUrl, result.UsedUrl);
        Assert.True(File.Exists(Path.Combine(tempDir, result.FileName!)));
        Assert.Equal(0, browserService.CallCount);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_HtmlWrapperWithEmbeddedPdfLink_ResolvesViaRegexAndDownloads()
    {
        const string originalUrl = "https://example.com/viewer?file=annual.pdf";
        const string resolvedUrl = "https://example.com/files/annual-real.pdf";

        var handler = new FakeHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            if (uri == originalUrl)
            {
                var html = $"<html><body><a href=\"{resolvedUrl}\">Download</a></body></html>";
                return HtmlResponse(html, finalUri: "https://example.com/viewer-final");
            }
            if (uri == resolvedUrl)
                return PdfResponse(PdfBytes);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 2, PrimaryUrl = originalUrl };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(tempDir, result.FileName!)));
        Assert.Equal(0, browserService.CallCount);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_RedirectResolvesToAnotherHtmlPage_ReturnsNotAPdfFailure()
    {
        const string originalUrl = "https://example.com/viewer?file=annual.pdf";
        const string wrapperUri = "https://example.com/wrapper?path=/files/still-html.pdf";

        var handler = new FakeHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            if (uri == originalUrl)
                return HtmlResponse("<html>loading...</html>", finalUri: wrapperUri);
            return HtmlResponse("<html>still not a pdf</html>");
        });

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 3, PrimaryUrl = originalUrl };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.False(result.Success);
        Assert.Contains("Not a PDF", result.ErrorMessage);
        Assert.Empty(Directory.GetFiles(tempDir));

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_HtmlWrapperWithNoResolvablePdfLink_ReturnsNotAPdfFailure()
    {
        const string originalUrl = "https://example.com/viewer?file=annual.pdf";

        var handler = new FakeHttpMessageHandler(req =>
            HtmlResponse("<html><body>Nothing useful here</body></html>",
                finalUri: "https://example.com/viewer-final"));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 4, PrimaryUrl = originalUrl };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.False(result.Success);
        Assert.Contains("Not a PDF", result.ErrorMessage);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_ContentTypePdfButInvalidMagicBytes_ReturnsFailureAndLeavesNoFiles()
    {
        var notReallyPdf = Encoding.ASCII.GetBytes("This is just text, not a PDF");

        var handler = new FakeHttpMessageHandler(req => PdfResponse(notReallyPdf));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 5, PrimaryUrl = "https://example.com/fake.pdf" };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.False(result.Success);
        Assert.Contains("not a valid PDF", result.ErrorMessage);
        Assert.Empty(Directory.GetFiles(tempDir));

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_OctetStreamContentTypeWithValidMagicBytes_Succeeds()
    {
        var handler = new FakeHttpMessageHandler(req =>
            PdfResponse(PdfBytes, contentType: "application/octet-stream"));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 6, PrimaryUrl = "https://example.com/file" };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(tempDir, result.FileName!)));

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_SuccessfulDownload_LeavesNoTempFile()
    {
        var handler = new FakeHttpMessageHandler(req => PdfResponse(PdfBytes));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 7, PrimaryUrl = "https://example.com/report.pdf" };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.True(result.Success);

        var files = Directory.GetFiles(tempDir);
        Assert.Single(files);
        Assert.False(files[0].EndsWith(".tmp"));

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_FileAlreadyExistsAtDestination_OverwritesIt()
    {
        var handler = new FakeHttpMessageHandler(req => PdfResponse(PdfBytes));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 8, PrimaryUrl = "https://example.com/report.pdf" };
        var expectedFileName = FileNameService.GetFileName(record.PrimaryUrl, record.Id);
        var expectedPath = Path.Combine(tempDir, expectedFileName);

        await File.WriteAllTextAsync(expectedPath, "old partial content");

        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.True(result.Success);

        var bytesOnDisk = await File.ReadAllBytesAsync(expectedPath);
        Assert.Equal(PdfBytes, bytesOnDisk);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_FileNameFuncReturnsNameWithoutExtension_AppendsPdfExtension()
    {
        var handler = new FakeHttpMessageHandler(req => PdfResponse(PdfBytes));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 9, PrimaryUrl = "https://example.com/report" };
        Func<string, int, string> fileNameFunc = (url, id) => $"{id}_custom";

        var result = await service.DownloadPdfAsync(record, tempDir, fileNameFunc);

        Assert.True(result.Success);
        Assert.Equal("9_custom.pdf", result.FileName);
        Assert.True(File.Exists(Path.Combine(tempDir, "9_custom.pdf")));

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_PrimaryTimesOut_FallsBackToBackup()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("primary"))
                throw new TaskCanceledException("Simulated timeout");
            return PdfResponse(PdfBytes);
        });

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord
        {
            Id = 10,
            PrimaryUrl = "https://example.com/primary.pdf",
            BackupUrl = "https://example.com/backup.pdf"
        };

        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.True(result.Success);
        Assert.Equal("https://example.com/backup.pdf", result.UsedUrl);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_AllUrlsTimeOut_ReturnsTimeoutError()
    {
        var handler = new FakeHttpMessageHandler(req =>
            throw new TaskCanceledException("Simulated timeout"));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 11, PrimaryUrl = "https://example.com/slow.pdf" };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.False(result.Success);
        Assert.Contains("Timeout", result.ErrorMessage);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_PrimaryAndBackupBothFail_ErrorReflectsBackupFailure()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("primary"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        });

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord
        {
            Id = 12,
            PrimaryUrl = "https://example.com/primary.pdf",
            BackupUrl = "https://example.com/backup.pdf"
        };

        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.False(result.Success);
        Assert.Contains("403", result.ErrorMessage);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_NoUrlsProvided_ReturnsFailureWithFallbackFileName()
    {
        var handler = new FakeHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 13, PrimaryUrl = "", BackupUrl = "" };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.False(result.Success);
        Assert.Equal("13_file.pdf", result.FileName);
        Assert.Equal("No URL provided", result.ErrorMessage);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_HttpClientFails_BrowserFallbackIsCalled()
    {
        var handler = new FakeHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.Forbidden));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 14, PrimaryUrl = "https://example.com/blocked.pdf" };
        await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.True(browserService.CallCount > 0);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_HttpClientFails_BrowserFallbackSucceeds_ReturnsSuccess()
    {
        var handler = new FakeHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.Forbidden));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadServiceSuccess();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 15, PrimaryUrl = "https://example.com/blocked.pdf" };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.True(result.Success);
        Assert.Equal("https://example.com/blocked.pdf", result.UsedUrl);
        Assert.True(File.Exists(Path.Combine(tempDir, result.FileName!)));
        Assert.Equal(1, browserService.CallCount);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_HttpClientSucceeds_BrowserFallbackIsNeverCalled()
    {
        var handler = new FakeHttpMessageHandler(req => PdfResponse(PdfBytes));

        var client = new HttpClient(handler);
        var browserService = new FakeBrowserDownloadServiceSuccess();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 16, PrimaryUrl = "https://example.com/report.pdf" };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.True(result.Success);
        Assert.Equal(0, browserService.CallCount);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task DownloadPdfAsync_BothHttpClientAndBrowserFail_ReturnsHttpClientError()
    {
        var handler = new FakeHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.Forbidden));

        var client = new HttpClient(handler);

        var browserService = new FakeBrowserDownloadService();
        var service = new DownloadService(client, browserService);
        var tempDir = CreateTempDir();

        var record = new PdfRecord { Id = 17, PrimaryUrl = "https://example.com/blocked.pdf" };
        var result = await service.DownloadPdfAsync(record, tempDir, FileNameService.GetFileName);

        Assert.False(result.Success);
        Assert.Contains("403", result.ErrorMessage);

        Directory.Delete(tempDir, true);
    }
}