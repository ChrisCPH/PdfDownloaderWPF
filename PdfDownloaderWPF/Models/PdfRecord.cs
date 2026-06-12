using System;
using System.Collections.Generic;
using System.Text;

namespace PdfDownloader.Models
{
    public class PdfRecord
    {
        public int Id { get; set; }
        public string PrimaryUrl { get; set; } = "";
        public string BackupUrl { get; set; } = "";
    }
}
