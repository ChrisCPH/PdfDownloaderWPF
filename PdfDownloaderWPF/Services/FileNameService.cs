using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PdfDownloader.Services
{
    public static class FileNameService
    {
        public static string GetFileName(string url, int id)
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));

            if (string.IsNullOrWhiteSpace(fileName))
                return $"{id}_file.pdf";

            var lower = fileName.ToLowerInvariant();
            var index = lower.IndexOf(".pdf");

            string cleanName = index >= 0 ? fileName.Substring(0, index + 4) : fileName;

            return $"{id}_{cleanName}";
        }
    }
}
