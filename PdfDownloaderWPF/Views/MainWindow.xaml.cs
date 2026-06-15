using PdfDownloader.Models;
using PdfDownloader.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;

namespace PdfDownloaderWPF.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void StartDownload(object sender, RoutedEventArgs e)
        {
            var inputFile = InputFileBox.Text;
            var outputFolder = OutputFolderBox.Text;
            var outputExcel = OutputExcelBox.Text;

            var excelService = new ExcelService();
            var stateService = new StateService();
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };

            var httpClient = new HttpClient(handler);
            var downloadService = new DownloadService(httpClient);

            var records = excelService.ReadRecords(inputFile)
                .OrderBy(x => x.Id)
                .ToList();

            var state = stateService.Load();
            var results = new List<DownloadResult>();

            int total = records.Count;
            int current = 0;

            try
            {
                foreach (var record in records)
                {
                    current++;
                    ProgressBar.Value = (double)current / total * 100;

                    if (state.Results.TryGetValue(record.Id, out var existing) && existing.Success)
                    {
                        results.Add(existing);
                        continue;
                    }

                    var result = await downloadService.DownloadPdfAsync(
                        record,
                        outputFolder,
                        FileNameService.GetFileName);

                    if (result.Success)
                    {
                        state.Results[record.Id] = result;
                        stateService.Save(state);
                    }

                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fatal error: {ex.Message}");
            }

            excelService.WriteResults(outputExcel, results);

            MessageBox.Show("Download complete!");
        }

        private void BrowseInputFile(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                InputFileBox.Text = dialog.FileName;
            }
        }

        private void BrowseOutputFolder(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();

            if (dialog.ShowDialog() == true)
            {
                OutputFolderBox.Text = dialog.SelectedPath;
            }
        }

        private void BrowseOutputExcel(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                OutputExcelBox.Text = dialog.FileName;
            }
        }

        private void ResetProgress(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "This will clear all saved progress. The next download run will re-check every record. Continue?",
                "Reset Progress",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                var stateService = new StateService();
                stateService.Reset();
                MessageBox.Show("Progress reset. All records will be re-checked on the next download.");
            }
        }
    }
}