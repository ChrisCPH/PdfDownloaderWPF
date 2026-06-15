using PdfDownloader.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace PdfDownloader.Services
{
    public class DownloadService
    {
        private readonly HttpClient _httpClient;

        public DownloadService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36"
            );

            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        }

        public async Task<DownloadResult> DownloadPdfAsync(PdfRecord record, string outputFolder, Func<string, int, string> fileNameFunc)
        {
            var result = new DownloadResult
            {
                Id = record.Id,
                PrimaryUrl = record.PrimaryUrl,
                BackupUrl = record.BackupUrl
            };

            var urls = BuildUrlList(record);

            if (urls.Count == 0)
            {
                result.Success = false;
                result.FileName = $"{record.Id}_file.pdf";
                result.ErrorMessage = "No URL provided";
                return result;
            }

            Exception? lastError = null;

            foreach (var url in urls)
            {
                try
                {
                    var (success, fileName, error) = await TryDownloadAsync(url, record.Id, outputFolder, fileNameFunc);
                    if (success)
                    {
                        result.Success = true;
                        result.UsedUrl = url;
                        result.FileName = fileName!;
                        result.DownloadDate = DateTime.Now;
                        return result;
                    }
                    lastError = error;
                }
                catch (TaskCanceledException)
                {
                    lastError = new Exception("Timeout / request cancelled");
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            result.Success = false;
            result.FileName = $"{record.Id}_file.pdf";
            if (lastError != null)
                result.ErrorMessage = lastError.Message;

            return result;
        }

        private static List<string> BuildUrlList(PdfRecord record)
        {
            var urls = new List<string>();
            if (!string.IsNullOrWhiteSpace(record.PrimaryUrl)) urls.Add(record.PrimaryUrl);
            if (!string.IsNullOrWhiteSpace(record.BackupUrl)) urls.Add(record.BackupUrl);
            return urls;
        }

        private async Task<(bool Success, string? FileName, Exception? Error)> TryDownloadAsync(
            string url, int recordId, string outputFolder, Func<string, int, string> fileNameFunc)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddBrowserHeaders(request);

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            Debug.WriteLine($"URL: {url}");
            Debug.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
            Debug.WriteLine($"Final URI: {response.RequestMessage?.RequestUri}");

            if ((int)response.StatusCode >= 400)
                return (false, null, new Exception($"HTTP {(int)response.StatusCode}"));

            var contentType = response.Content.Headers.ContentType?.MediaType;

            if (contentType != null && contentType.Contains("text/html"))
            {
                response = await ResolveRedirectWrapperAsync(response);
                contentType = response.Content.Headers.ContentType?.MediaType;

                if (contentType == null || (!contentType.Contains("pdf") && !contentType.Contains("octet-stream")))
                    return (false, null, new Exception($"Not a PDF (Content-Type: {contentType})"));
            }

            return await SavePdfAsync(response, url, recordId, outputFolder, fileNameFunc);
        }

        private async Task<HttpResponseMessage> ResolveRedirectWrapperAsync(HttpResponseMessage response)
        {
            string? pdfUrl = null;

            var finalUri = response.RequestMessage?.RequestUri;
            if (finalUri != null)
            {
                var query = System.Web.HttpUtility.ParseQueryString(finalUri.Query);
                var path = query["path"];
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    pdfUrl = $"{finalUri.Scheme}://{finalUri.Host}{path}";
            }

            if (pdfUrl == null)
            {
                var html = await response.Content.ReadAsStringAsync();
                var match = System.Text.RegularExpressions.Regex.Match(
                    html, @"(https?:\/\/[^\s""'<>]+\.pdf[^\s""'<>]*)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                    pdfUrl = match.Value;
            }

            if (pdfUrl == null)
                return response;

            var pdfRequest = new HttpRequestMessage(HttpMethod.Get, pdfUrl);
            AddDirectFetchHeaders(pdfRequest);
            return await _httpClient.SendAsync(pdfRequest, HttpCompletionOption.ResponseHeadersRead);
        }

        private static async Task<(bool, string?, Exception?)> SavePdfAsync(
            HttpResponseMessage response, string url, int recordId, string outputFolder, Func<string, int, string> fileNameFunc)
        {
            var fileName = fileNameFunc(url, recordId);
            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                fileName += ".pdf";

            var filePath = Path.Combine(outputFolder, fileName);
            var tempPath = filePath + ".tmp";

            await using var stream = await response.Content.ReadAsStreamAsync();

            var buffer = new byte[5];
            var bytesRead = await stream.ReadAsync(buffer, 0, 5);
            if (bytesRead < 4 || buffer[0] != '%' || buffer[1] != 'P' || buffer[2] != 'D' || buffer[3] != 'F')
                return (false, null, new Exception("Downloaded content is not a valid PDF"));

            await using (var file = File.Create(tempPath))
            {
                await file.WriteAsync(buffer, 0, bytesRead);
                await stream.CopyToAsync(file);
            }

            // Only becomes the real file once fully downloaded
            File.Move(tempPath, filePath, overwrite: true);

            return (true, fileName, null);
        }

        private void AddBrowserHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-Site", "none");
            request.Headers.Add("Sec-Fetch-User", "?1");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\", \"Not_A Brand\";v=\"99\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
        }

        private void AddDirectFetchHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("Accept", "application/pdf,*/*");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Sec-Fetch-Dest", "empty");
            request.Headers.Add("Sec-Fetch-Mode", "cors");
            request.Headers.Add("Sec-Fetch-Site", "same-origin");
        }
    }
}
