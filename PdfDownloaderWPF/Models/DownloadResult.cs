using System;
using System.Collections.Generic;
using System.Text;

namespace PdfDownloader.Models
{
    public class DownloadResult
    {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public string PrimaryUrl { get; set; } = "";
        public string BackupUrl { get; set; } = "";
        public string UsedUrl { get; set; } = "";
        public bool Success { get; set; }
        public DateTime? DownloadDate { get; set; }
        public string ErrorMessage { get; set; } = "";
    }
}
