using PdfDownloader.Models;
using PdfDownloader.Services;
using System;
using System.IO;
using Xunit;

namespace PdfDownloader.Tests
{
    public class StateServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _filePath;

        public StateServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "StateServiceTests_" + Guid.NewGuid());
            _filePath = Path.Combine(_tempDir, "state.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private static DownloadResult MakeResult(int id, bool success = true)
        {
            return new DownloadResult
            {
                Id = id,
                PrimaryUrl = $"https://example.com/{id}.pdf",
                BackupUrl = "",
                Success = success,
                UsedUrl = success ? $"https://example.com/{id}.pdf" : string.Empty,
                FileName = success ? $"{id}_file.pdf" : string.Empty,
                DownloadDate = success ? new DateTime(2026, 6, 15, 10, 0, 0) : default,
                ErrorMessage = success ? string.Empty : "HTTP 403"
            };
        }

        [Fact]
        public void Constructor_CreatesDirectoryIfItDoesNotExist()
        {
            Assert.False(Directory.Exists(_tempDir));

            var _ = new StateService(_filePath);

            Assert.True(Directory.Exists(_tempDir));
        }

        [Fact]
        public void Constructor_DoesNotThrowIfDirectoryAlreadyExists()
        {
            Directory.CreateDirectory(_tempDir);

            var exception = Record.Exception(() => new StateService(_filePath));

            Assert.Null(exception);
        }

        [Fact]
        public void Load_FileDoesNotExist_ReturnsEmptyState()
        {
            var service = new StateService(_filePath);

            var state = service.Load();

            Assert.NotNull(state);
            Assert.Empty(state.Results);
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsResults()
        {
            var service = new StateService(_filePath);

            var state = new DownloadState();
            state.Results[1] = MakeResult(1);
            state.Results[5] = MakeResult(5);
            state.Results[42] = MakeResult(42);

            service.Save(state);
            var loaded = service.Load();

            Assert.Equal(3, loaded.Results.Count);
            Assert.True(loaded.Results.ContainsKey(1));
            Assert.True(loaded.Results.ContainsKey(5));
            Assert.True(loaded.Results.ContainsKey(42));
        }

        [Fact]
        public void Save_ThenLoad_PreservesResultDetails()
        {
            var service = new StateService(_filePath);

            var state = new DownloadState();
            state.Results[7] = MakeResult(7);

            service.Save(state);
            var loaded = service.Load();

            var result = loaded.Results[7];

            Assert.True(result.Success);
            Assert.Equal("https://example.com/7.pdf", result.UsedUrl);
            Assert.Equal("7_file.pdf", result.FileName);
            Assert.Equal(new DateTime(2026, 6, 15, 10, 0, 0), result.DownloadDate);
        }

        [Fact]
        public void Save_CreatesFileOnDisk()
        {
            var service = new StateService(_filePath);
            var state = new DownloadState();
            state.Results[1] = MakeResult(1);

            service.Save(state);

            Assert.True(File.Exists(_filePath));
        }

        [Fact]
        public void Save_OverwritesPreviousState()
        {
            var service = new StateService(_filePath);

            var firstState = new DownloadState();
            firstState.Results[1] = MakeResult(1);
            service.Save(firstState);

            var secondState = new DownloadState();
            secondState.Results[2] = MakeResult(2);
            secondState.Results[3] = MakeResult(3);
            service.Save(secondState);

            var loaded = service.Load();

            Assert.Equal(2, loaded.Results.Count);
            Assert.False(loaded.Results.ContainsKey(1));
            Assert.True(loaded.Results.ContainsKey(2));
            Assert.True(loaded.Results.ContainsKey(3));
        }

        [Fact]
        public void Reset_DeletesExistingFile()
        {
            var service = new StateService(_filePath);
            var state = new DownloadState();
            state.Results[1] = MakeResult(1);
            service.Save(state);

            Assert.True(File.Exists(_filePath));

            service.Reset();

            Assert.False(File.Exists(_filePath));
        }

        [Fact]
        public void Reset_FileDoesNotExist_DoesNotThrow()
        {
            var service = new StateService(_filePath);

            var exception = Record.Exception(() => service.Reset());

            Assert.Null(exception);
        }

        [Fact]
        public void Load_AfterReset_ReturnsEmptyState()
        {
            var service = new StateService(_filePath);
            var state = new DownloadState();
            state.Results[1] = MakeResult(1);
            service.Save(state);

            service.Reset();
            var loaded = service.Load();

            Assert.Empty(loaded.Results);
        }

        [Fact]
        public void Load_CorruptJson_ReturnsEmptyStateInsteadOfThrowing()
        {
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(_filePath, "{ this is not valid json ");

            var service = new StateService(_filePath);

            var state = service.Load();

            Assert.NotNull(state);
            Assert.Empty(state.Results);
        }

        [Fact]
        public void Save_LeavesNoTempFileOnDisk()
        {
            var service = new StateService(_filePath);
            var state = new DownloadState();
            state.Results[1] = MakeResult(1);

            service.Save(state);

            Assert.False(File.Exists(_filePath + ".tmp"));
            Assert.True(File.Exists(_filePath));
        }
    }
}