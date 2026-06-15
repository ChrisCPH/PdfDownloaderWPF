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
        private readonly string _filePath;

        public StateService(string? filePath = null)
        {
            _filePath = filePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PdfDownloader", "state.json");

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        public DownloadState Load()
        {
            if (!File.Exists(_filePath))
                return new DownloadState();

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<DownloadState>(json)
                       ?? new DownloadState();
            }
            catch (JsonException)
            {
                return new DownloadState();
            }
        }

        public void Save(DownloadState state)
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(_filePath))
                File.Replace(tempPath, _filePath, null);
            else
                File.Move(tempPath, _filePath);
        }

        public void Reset()
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }
    }
}
