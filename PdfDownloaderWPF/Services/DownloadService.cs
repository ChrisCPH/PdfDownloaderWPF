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

            var urls = new List<string>();

            if (!string.IsNullOrWhiteSpace(record.PrimaryUrl))
                urls.Add(record.PrimaryUrl);

            if (!string.IsNullOrWhiteSpace(record.BackupUrl))
                urls.Add(record.BackupUrl);

            Exception? lastError = null;

            foreach (var url in urls)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    AddBrowserHeaders(request);

                    var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    
                    Debug.WriteLine($"URL: {url}");
                    Debug.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
                    Debug.WriteLine($"Final URI: {response.RequestMessage?.RequestUri}");
                    Debug.WriteLine($"Content-Type: {response.Content.Headers.ContentType}");
                    Debug.WriteLine($"Content-Length: {response.Content.Headers.ContentLength}");

                    if ((int)response.StatusCode >= 400)
                    {
                        lastError = new Exception($"HTTP {(int)response.StatusCode}");
                        continue;
                    }

                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    if (contentType != null && contentType.Contains("text/html"))
                    {
                        string? pdfUrl = null;

                        var finalUri = response.RequestMessage?.RequestUri;
                        if (finalUri != null)
                        {
                            var query = System.Web.HttpUtility.ParseQueryString(finalUri.Query);
                            var path = query["path"];
                            Debug.WriteLine($"Extracted path param: {path}");
                            if (!string.IsNullOrEmpty(path) && path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                pdfUrl = $"{finalUri.Scheme}://{finalUri.Host}{path}";
                            }
                        }

                        Debug.WriteLine($"Resolved pdfUrl from path param: {pdfUrl}");

                        if (pdfUrl == null)
                        {
                            var html = await response.Content.ReadAsStringAsync();
                            Debug.WriteLine($"HTML length: {html.Length}");
                            var match = System.Text.RegularExpressions.Regex.Match(
                                html, @"(https?:\/\/[^\s""'<>]+\.pdf[^\s""'<>]*)",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (match.Success)
                                pdfUrl = match.Value;
                            Debug.WriteLine($"Resolved pdfUrl from HTML regex: {pdfUrl}");
                        }

                        if (pdfUrl != null)
                        {
                            Debug.WriteLine($"Re-requesting: {pdfUrl}");
                            var pdfRequest = new HttpRequestMessage(HttpMethod.Get, pdfUrl);
                            AddDirectFetchHeaders(pdfRequest);

                            response = await _httpClient.SendAsync(pdfRequest, HttpCompletionOption.ResponseHeadersRead);
                            contentType = response.Content.Headers.ContentType?.MediaType;
                            Debug.WriteLine($"Second request status: {response.StatusCode}, Content-Type: {contentType}");
                        }

                        if (contentType == null || (!contentType.Contains("pdf") && !contentType.Contains("octet-stream")))
                        {
                            lastError = new Exception($"Not a PDF (Content-Type: {contentType})");
                            continue;
                        }
                    }

                    var fileName = fileNameFunc(url, record.Id);

                    if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName += ".pdf";
                    }

                    var filePath = Path.Combine(outputFolder, fileName);

                    await using var stream = await response.Content.ReadAsStreamAsync();
                    if (stream == null)
                    {
                        lastError = new Exception("Null stream");
                        continue;
                    }

                    var buffer = new byte[5];
                    var bytesRead = await stream.ReadAsync(buffer, 0, 5);
                    if (bytesRead < 4 || buffer[0] != '%' || buffer[1] != 'P' || buffer[2] != 'D' || buffer[3] != 'F')
                    {
                        lastError = new Exception("Downloaded content is not a valid PDF");
                        continue;
                    }

                    await using var file = File.Create(filePath);
                    await file.WriteAsync(buffer, 0, bytesRead);
                    await stream.CopyToAsync(file);

                    result.Success = true;
                    result.UsedUrl = url;
                    result.FileName = fileName;
                    result.DownloadDate = DateTime.Now;
                    return result;
                }
                catch (TaskCanceledException)
                {
                    lastError = new Exception("Timeout / request cancelled");
                    continue;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    continue;
                }
            }

            result.Success = false;
            result.FileName = $"{record.Id}_file.pdf";

            if (lastError != null)
                result.ErrorMessage = lastError.Message;

            return result;
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
