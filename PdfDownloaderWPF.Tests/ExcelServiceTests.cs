using ClosedXML.Excel;
using PdfDownloader.Models;
using PdfDownloader.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace PdfDownloader.Tests
{
    public class ExcelServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public ExcelServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ExcelServiceTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string CreateInputFile(IEnumerable<(object Id, object Primary, object Backup)> rows)
        {
            var path = Path.Combine(_tempDir, $"input_{Guid.NewGuid()}.xlsx");

            using var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Sheet1");

            ws.Cell(1, 1).Value = "Id";
            ws.Cell(1, 2).Value = "PrimaryUrl";
            ws.Cell(1, 3).Value = "BackupUrl";

            var rowIndex = 2;
            foreach (var (id, primary, backup) in rows)
            {
                SetCellValue(ws.Cell(rowIndex, 1), id);
                SetCellValue(ws.Cell(rowIndex, 2), primary);
                SetCellValue(ws.Cell(rowIndex, 3), backup);
                rowIndex++;
            }

            workbook.SaveAs(path);
            return path;
        }

        private static void SetCellValue(IXLCell cell, object value)
        {
            switch (value)
            {
                case int i:
                    cell.Value = i;
                    break;
                case string s:
                    cell.Value = s;
                    break;
                case null:
                    cell.Value = string.Empty;
                    break;
                default:
                    cell.Value = value.ToString();
                    break;
            }
        }

        [Fact]
        public void ReadRecords_BasicFile_ReturnsExpectedRecords()
        {
            var path = CreateInputFile(
            [
                (1, "https://example.com/a.pdf", "https://example.com/a-backup.pdf"),
                (2, "https://example.com/b.pdf", "")
            ]);

            var service = new ExcelService();
            var records = service.ReadRecords(path);

            Assert.Equal(2, records.Count);

            Assert.Equal(1, records[0].Id);
            Assert.Equal("https://example.com/a.pdf", records[0].PrimaryUrl);
            Assert.Equal("https://example.com/a-backup.pdf", records[0].BackupUrl);

            Assert.Equal(2, records[1].Id);
            Assert.Equal("https://example.com/b.pdf", records[1].PrimaryUrl);
            Assert.Equal("", records[1].BackupUrl);
        }

        [Fact]
        public void ReadRecords_SkipsHeaderRow()
        {
            var path = CreateInputFile(
            [
                (1, "https://example.com/a.pdf", "")
            ]);

            var service = new ExcelService();
            var records = service.ReadRecords(path);

            Assert.Single(records);
            Assert.Equal(1, records[0].Id);
        }

        [Fact]
        public void ReadRecords_EmptyBackupUrl_ReturnsEmptyString()
        {
            var path = CreateInputFile(
            [          
                (5, "https://example.com/c.pdf", "")
            ]);

            var service = new ExcelService();
            var records = service.ReadRecords(path);

            Assert.Equal(string.Empty, records[0].BackupUrl);
        }

        [Fact]
        public void ReadRecords_NoDataRows_ReturnsEmptyList()
        {
            var path = CreateInputFile(Array.Empty<(object, object, object)>());

            var service = new ExcelService();
            var records = service.ReadRecords(path);

            Assert.Empty(records);
        }

        [Fact]
        public void ReadRecords_NonSequentialIds_ReadsIdsAsGiven()
        {
            var path = CreateInputFile(
            [
                (10, "https://example.com/a.pdf", ""),
                (3, "https://example.com/b.pdf", "")
            ]);

            var service = new ExcelService();
            var records = service.ReadRecords(path);

            Assert.Equal(10, records[0].Id);
            Assert.Equal(3, records[1].Id);
        }

        [Fact]
        public void ReadRecords_NonNumericIdCell_ThrowsFormatException()
        {
            var path = CreateInputFile(
            [
                ("not-a-number", "https://example.com/a.pdf", "")
            ]);

            var service = new ExcelService();

            Assert.ThrowsAny<Exception>(() => service.ReadRecords(path));
        }


        [Fact]
        public void WriteResults_CreatesFileWithHeaderRow()
        {
            var path = Path.Combine(_tempDir, "results.xlsx");
            var results = new List<DownloadResult>();

            var service = new ExcelService();
            service.WriteResults(path, results);

            Assert.True(File.Exists(path));

            using var workbook = new XLWorkbook(path);
            var ws = workbook.Worksheet(1);

            Assert.Equal("Id", ws.Cell(1, 1).GetValue<string>());
            Assert.Equal("FileName", ws.Cell(1, 2).GetValue<string>());
            Assert.Equal("Success", ws.Cell(1, 3).GetValue<string>());
            Assert.Equal("UsedUrl", ws.Cell(1, 4).GetValue<string>());
            Assert.Equal("DownloadDate", ws.Cell(1, 5).GetValue<string>());
            Assert.Equal("ErrorMessage", ws.Cell(1, 6).GetValue<string>());
        }

        [Fact]
        public void WriteResults_WritesRowsInIdOrder()
        {
            var path = Path.Combine(_tempDir, "results.xlsx");

            var results = new List<DownloadResult>
            {
                new() { Id = 5, FileName = "5_b.pdf", Success = true, UsedUrl = "https://example.com/b.pdf", DownloadDate = new DateTime(2024, 1, 1) },
                new() { Id = 1, FileName = "1_a.pdf", Success = true, UsedUrl = "https://example.com/a.pdf", DownloadDate = new DateTime(2024, 1, 1) },
                new() { Id = 3, FileName = "3_c.pdf", Success = false, ErrorMessage = "HTTP 403" }
            };

            var service = new ExcelService();
            service.WriteResults(path, results);

            using var workbook = new XLWorkbook(path);
            var ws = workbook.Worksheet(1);

            Assert.Equal(1, ws.Cell(2, 1).GetValue<int>());
            Assert.Equal(3, ws.Cell(3, 1).GetValue<int>());
            Assert.Equal(5, ws.Cell(4, 1).GetValue<int>());
        }

        [Fact]
        public void WriteResults_FailedResult_WritesErrorMessageAndFalseSuccess()
        {
            var path = Path.Combine(_tempDir, "results.xlsx");

            var results = new List<DownloadResult>
            {
                new DownloadResult
                {
                    Id = 1,
                    Success = false,
                    FileName = "1_file.pdf",
                    ErrorMessage = "HTTP 403"
                }
            };

            var service = new ExcelService();
            service.WriteResults(path, results);

            using var workbook = new XLWorkbook(path);
            var ws = workbook.Worksheet(1);

            Assert.False(ws.Cell(2, 3).GetValue<bool>());
            Assert.Equal("HTTP 403", ws.Cell(2, 6).GetValue<string>());
            Assert.True(ws.Cell(2, 4).IsEmpty());
        }

        [Fact]
        public void WriteResults_SuccessfulResult_WritesAllFields()
        {
            var path = Path.Combine(_tempDir, "results.xlsx");
            var date = new DateTime(2026, 6, 15, 10, 30, 0);

            var results = new List<DownloadResult>
            {
                new DownloadResult
                {
                    Id = 42,
                    Success = true,
                    FileName = "42_report.pdf",
                    UsedUrl = "https://example.com/report.pdf",
                    DownloadDate = date
                }
            };

            var service = new ExcelService();
            service.WriteResults(path, results);

            using var workbook = new XLWorkbook(path);
            var ws = workbook.Worksheet(1);

            Assert.Equal(42, ws.Cell(2, 1).GetValue<int>());
            Assert.Equal("42_report.pdf", ws.Cell(2, 2).GetValue<string>());
            Assert.True(ws.Cell(2, 3).GetValue<bool>());
            Assert.Equal("https://example.com/report.pdf", ws.Cell(2, 4).GetValue<string>());
            Assert.Equal(date, ws.Cell(2, 5).GetValue<DateTime>());
        }

        [Fact]
        public void ReadRecords_RoundTripWithSpecialCharactersInUrl_PreservesUrl()
        {
            var urlWithSpecialChars = "https://example.com/files/Annual%20Report_2018.pdf?v=123";

            var path = CreateInputFile(
            [
                (1, urlWithSpecialChars, "")
            ]);

            var service = new ExcelService();
            var records = service.ReadRecords(path);

            Assert.Equal(urlWithSpecialChars, records[0].PrimaryUrl);
        }
    }
}