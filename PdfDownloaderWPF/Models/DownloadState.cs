using System;
using System.Collections.Generic;
using System.Text;

namespace PdfDownloader.Models
{
    public class DownloadState
    {
        public Dictionary<int, DownloadResult> Results { get; set; } = new();
    }
}
