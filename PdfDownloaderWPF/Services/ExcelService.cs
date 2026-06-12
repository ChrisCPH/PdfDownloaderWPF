using ClosedXML.Excel;
using PdfDownloader.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfDownloader.Services
{
    public class ExcelService
    {
        public List<PdfRecord> ReadRecords(string filePath)
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);

            var records = new List<PdfRecord>();

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                records.Add(new PdfRecord
                {
                    Id = row.Cell(1).GetValue<int>(),
                    PrimaryUrl = row.Cell(2).GetValue<string>(),
                    BackupUrl = row.Cell(3).GetValue<string>()
                });
            }

            return records;
        }

        public void WriteResults(string filePath, List<DownloadResult> results)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Results");

            ws.Cell(1, 1).Value = "Id";
            ws.Cell(1, 2).Value = "FileName";
            ws.Cell(1, 3).Value = "Success";
            ws.Cell(1, 4).Value = "UsedUrl";
            ws.Cell(1, 5).Value = "DownloadDate";

            var row = 2;

            foreach (var result in results.OrderBy(x => x.Id))
            {
                ws.Cell(row, 1).Value = result.Id;
                ws.Cell(row, 2).Value = result.FileName;
                ws.Cell(row, 3).Value = result.Success;
                ws.Cell(row, 4).Value = result.UsedUrl;
                ws.Cell(row, 5).Value = result.DownloadDate;

                row++;
            }

            workbook.SaveAs(filePath);
        }
    }
}
