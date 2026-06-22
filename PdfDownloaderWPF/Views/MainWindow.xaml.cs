using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using PdfDownloader.Models;
using PdfDownloader.Services;
using PdfDownloaderWPF.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace PdfDownloaderWPF.Views
{
    public partial class MainWindow : Window
    {
        private TaskCompletionSource<bool>? _pauseTcs;

        public MainWindow()
        {
            InitializeComponent();
            Task.Run(EnsurePlaywrightInstalled);
        }

        private async void StartDownload(object sender, RoutedEventArgs e)
        {
            var inputFile = InputFileBox.Text;
            var outputFolder = OutputFolderBox.Text;
            var outputExcel = OutputExcelBox.Text;

            if (string.IsNullOrWhiteSpace(inputFile) || !File.Exists(inputFile))
            {
                MessageBox.Show("Please select a valid Excel input file.", "Missing Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(outputFolder) || !Directory.Exists(outputFolder))
            {
                MessageBox.Show("Please select a valid download folder.", "Missing Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(outputExcel))
            {
                MessageBox.Show("Please select a location to save the results Excel file.", "Missing Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;

            var excelService = new ExcelService();
            var stateService = new StateService();
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };

            var httpClient = new HttpClient(handler);
            var browserDownloadService = new BrowserDownloadService();
            var downloadService = new DownloadService(httpClient, browserDownloadService);

            var records = excelService.ReadRecords(inputFile)
                .OrderBy(x => x.Id)
                .ToList();

            var state = stateService.Load();
            var results = new List<DownloadResult>();

            int total = records.Count;

            var semaphore = new SemaphoreSlim(15);
            var stateLock = new object();
            int current = 0;

            var tasks = records.Select(async record =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await WaitIfPausedAsync();
                    if (state.Results.TryGetValue(record.Id, out var existing) && existing.Success)
                    {
                        lock (stateLock)
                            results.Add(existing);
                        Interlocked.Increment(ref current);
                        Dispatcher.Invoke(() => ProgressBar.Value = (double)current / total * 100);
                        return;
                    }

                    var result = await downloadService.DownloadPdfAsync(
                        record, outputFolder, FileNameService.GetFileName);

                    lock (stateLock)
                    {
                        if (result.Success)
                        {
                            state.Results[record.Id] = result;
                            stateService.Save(state);
                        }
                        results.Add(result);
                    }

                    Interlocked.Increment(ref current);
                    Dispatcher.Invoke(() => ProgressBar.Value = (double)current / total * 100);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

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

        private Task WaitIfPausedAsync()
        {
            return _pauseTcs?.Task ?? Task.CompletedTask;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pauseTcs == null)
            {
                _pauseTcs = new TaskCompletionSource<bool>();
                PauseButton.Content = "Resume";
            }
            else
            {
                _pauseTcs.SetResult(true);
                _pauseTcs = null;
                PauseButton.Content = "Pause";
            }
        }

        private static void EnsurePlaywrightInstalled()
        {
            try
            {
                var exitCode = Microsoft.Playwright.Program.Main([ "install", "chromium" ]);
                if (exitCode != 0)
                    throw new Exception("Playwright install failed");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to install required browser components: {ex.Message}\n\n" +
                    "The app will still work but some downloads may fail.",
                    "Setup Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}