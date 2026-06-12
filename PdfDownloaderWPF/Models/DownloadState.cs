using System;
using System.Collections.Generic;
using System.Text;

namespace PdfDownloader.Models
{
    public class DownloadState
    {
        public HashSet<int> DownloadedIds { get; set; } = [];
    }
}
