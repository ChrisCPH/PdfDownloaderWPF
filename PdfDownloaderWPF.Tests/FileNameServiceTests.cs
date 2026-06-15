using PdfDownloader.Services;
using Xunit;

namespace PdfDownloader.Tests
{
    public class FileNameServiceTests
    {
        [Fact]
        public void GetFileName_SimpleUrl_ReturnsIdPrefixedFileName()
        {
            var result = FileNameService.GetFileName("https://example.com/files/report.pdf", 1);

            Assert.Equal("1_report.pdf", result);
        }

        [Fact]
        public void GetFileName_EncodedSpaceInPath_DecodesSpaceInFileName()
        {
            var result = FileNameService.GetFileName(
                "https://www.idx.co.id/Portals/0/.../INDF_Annual%20Report_2018.pdf", 263);

            Assert.Equal("263_INDF_Annual Report_2018.pdf", result);
        }

        [Fact]
        public void GetFileName_ExtraTextAfterPdfExtension_TrimsToExtension()
        {
            // e.g. "report.pdf?v=12345" type filenames where ".pdf" isn't at the end
            var result = FileNameService.GetFileName(
                "https://example.com/report.pdf_v2_final", 5);

            Assert.Equal("5_report.pdf", result);
        }

        [Fact]
        public void GetFileName_NoExtension_KeepsOriginalNameAndAppendsNothing()
        {
            var result = FileNameService.GetFileName("https://example.com/files/report", 7);

            Assert.Equal("7_report", result);
        }

        [Fact]
        public void GetFileName_UrlWithQueryString_IgnoresQueryString()
        {
            // AbsolutePath excludes the query string, so ?v=12345 should not appear
            var result = FileNameService.GetFileName(
                "https://www.economie.gouv.fr/files/REA2023_Ang-web.pdf?v=1705416463", 132);

            Assert.Equal("132_REA2023_Ang-web.pdf", result);
        }

        [Fact]
        public void GetFileName_TrailingSlash_ReturnsFallbackName()
        {
            // Path.GetFileName returns "" for a path ending in '/'
            var result = FileNameService.GetFileName("https://example.com/files/", 10);

            Assert.Equal("10_file.pdf", result);
        }

        [Fact]
        public void GetFileName_RootUrlWithNoPath_ReturnsFallbackName()
        {
            var result = FileNameService.GetFileName("https://example.com/", 11);

            Assert.Equal("11_file.pdf", result);
        }

        [Fact]
        public void GetFileName_UppercasePdfExtension_IsRecognized()
        {
            var result = FileNameService.GetFileName("https://example.com/files/Report.PDF", 12);

            Assert.Equal("12_Report.PDF", result);
        }

        [Fact]
        public void GetFileName_MixedCasePdfInMiddleOfFileName_TrimsAtFirstOccurrence()
        {
            // filename contains ".pdf" not at the very end, mixed case
            var result = FileNameService.GetFileName("https://example.com/files/Report.PDFinfo.txt", 13);

            Assert.Equal("13_Report.PDF", result);
        }

        [Fact]
        public void GetFileName_FileNameWithSpacesAndSpecialChars_PreservesThem()
        {
            var result = FileNameService.GetFileName(
                "https://example.com/files/2024%20Annual%20Report%20(Final).pdf", 20);

            Assert.Equal("20_2024 Annual Report (Final).pdf", result);
        }

        [Fact]
        public void GetFileName_DifferentIds_ProduceDifferentPrefixes()
        {
            var url = "https://example.com/files/report.pdf";

            var result1 = FileNameService.GetFileName(url, 1);
            var result2 = FileNameService.GetFileName(url, 2);

            Assert.Equal("1_report.pdf", result1);
            Assert.Equal("2_report.pdf", result2);
        }
    }
}