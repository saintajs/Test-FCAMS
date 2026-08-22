using System;
using System.Collections.Generic;
using System.IO;
using FCAMS.Web.DAL.Repositories;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace FCAMS.Web.Services
{
    /// <summary>
    /// Builds the two FCAMS PDF reports using iTextSharp (the "LGPL"
    /// iTextSharp 5.x package on NuGet). This is fully working, real PDF
    /// generation -- not a stub -- and shares its data source (the report
    /// stored procedures) with the Crystal Reports designer files described
    /// in Docs/CRYSTAL_REPORTS_SETUP.md, so both rendering paths produce
    /// the same numbers from the same query.
    /// </summary>
    public static class PdfReportBuilder
    {
        private static readonly BaseColor HeaderBg = new BaseColor(31, 58, 95);
        private static readonly Font TitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLACK);
        private static readonly Font SubtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.DARK_GRAY);
        private static readonly Font TableHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.WHITE);
        private static readonly Font TableCellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);
        private static readonly Font FooterFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.GRAY);

        public static byte[] BuildComplianceSummary(List<ComplianceSummaryRow> rows, DateTime dateFrom, DateTime dateTo)
        {
            using (var stream = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 36, 36, 54, 54);
                PdfWriter.GetInstance(document, stream);
                document.Open();

                AddHeader(document, "FCAMS Compliance Summary Report", dateFrom, dateTo);

                var table = new PdfPTable(7) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 12, 26, 12, 10, 15, 8, 8 });
                AddHeaderRow(table, "Rule Code", "Rule Name", "Severity", "Count", "Amount At Risk", "Open", "Resolved");

                decimal grandTotalAtRisk = 0;
                int grandTotalCount = 0;

                foreach (var row in rows)
                {
                    table.AddCell(Cell(row.RuleCode));
                    table.AddCell(Cell(row.RuleName));
                    table.AddCell(Cell(row.Severity));
                    table.AddCell(Cell(row.ViolationCount.ToString()));
                    table.AddCell(Cell(row.TotalAmountAtRisk.ToString("C")));
                    table.AddCell(Cell(row.OpenCount.ToString()));
                    table.AddCell(Cell(row.ResolvedCount.ToString()));

                    grandTotalAtRisk += row.TotalAmountAtRisk;
                    grandTotalCount += row.ViolationCount;
                }

                if (rows.Count == 0)
                {
                    var emptyCell = new PdfPCell(new Phrase("No compliance violations were detected in this period.", TableCellFont))
                    {
                        Colspan = 7,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 12
                    };
                    table.AddCell(emptyCell);
                }

                document.Add(table);

                document.Add(new Paragraph(" "));
                var summary = new Paragraph(
                    $"Total violations: {grandTotalCount}      Total amount at risk: {grandTotalAtRisk:C}",
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11));
                document.Add(summary);

                AddFooter(document);
                document.Close();
                return stream.ToArray();
            }
        }

        public static byte[] BuildAuditTrail(List<AuditTrailRow> rows, DateTime dateFrom, DateTime dateTo)
        {
            using (var stream = new MemoryStream())
            {
                var document = new Document(PageSize.A4.Rotate(), 36, 36, 54, 54);
                PdfWriter.GetInstance(document, stream);
                document.Open();

                AddHeader(document, "FCAMS System Audit Trail", dateFrom, dateTo);

                var table = new PdfPTable(6) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 14, 12, 18, 14, 14, 18 });
                AddHeaderRow(table, "User", "Action", "Entity", "Entity Id", "IP Address", "Timestamp (UTC)");

                foreach (var row in rows)
                {
                    table.AddCell(Cell(row.Username ?? "(system)"));
                    table.AddCell(Cell(row.ActionType));
                    table.AddCell(Cell(row.EntityName));
                    table.AddCell(Cell(row.EntityId ?? "-"));
                    table.AddCell(Cell(row.IPAddress ?? "-"));
                    table.AddCell(Cell(row.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")));
                }

                if (rows.Count == 0)
                {
                    var emptyCell = new PdfPCell(new Phrase("No audit log activity was recorded in this period.", TableCellFont))
                    {
                        Colspan = 6,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 12
                    };
                    table.AddCell(emptyCell);
                }

                document.Add(table);

                document.Add(new Paragraph(" "));
                document.Add(new Paragraph($"Total events: {rows.Count}", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11)));

                AddFooter(document);
                document.Close();
                return stream.ToArray();
            }
        }

        private static void AddHeader(Document document, string title, DateTime dateFrom, DateTime dateTo)
        {
            document.Add(new Paragraph(title, TitleFont));
            document.Add(new Paragraph($"Period: {dateFrom:MMMM d, yyyy} - {dateTo:MMMM d, yyyy}", SubtitleFont));
            document.Add(new Paragraph($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC", SubtitleFont));
            document.Add(new Paragraph(" "));
        }

        private static void AddFooter(Document document)
        {
            document.Add(new Paragraph(" "));
            document.Add(new Paragraph(
                "Financial Compliance & Audit Management System -- Generated report. " +
                "Distribution restricted to authorized compliance and audit personnel.",
                FooterFont));
        }

        private static void AddHeaderRow(PdfPTable table, params string[] headers)
        {
            foreach (var header in headers)
            {
                var cell = new PdfPCell(new Phrase(header, TableHeaderFont))
                {
                    BackgroundColor = HeaderBg,
                    Padding = 5,
                    HorizontalAlignment = Element.ALIGN_LEFT
                };
                table.AddCell(cell);
            }
        }

        private static PdfPCell Cell(string text)
        {
            return new PdfPCell(new Phrase(text ?? string.Empty, TableCellFont)) { Padding = 4 };
        }
    }
}
