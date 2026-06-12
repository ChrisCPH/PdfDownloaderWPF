using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using PdfDownloader.Models;

namespace PdfDownloader.Services
{
    using System.Text.Json;

    public class StateService
    {
        private static readonly string FileName = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PdfDownloader", "state.json");

        public StateService()
        {
            var directory = Path.GetDirectoryName(FileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public DownloadState Load()
        {
            if (!File.Exists(FileName))
                return new DownloadState();

            var json = File.ReadAllText(FileName);
            return JsonSerializer.Deserialize<DownloadState>(json)
                   ?? new DownloadState();
        }

        public void Save(DownloadState state)
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(FileName, json);
        }

        public void Reset()
        {
            if (File.Exists(FileName))
                File.Delete(FileName);
        }
    }
}
