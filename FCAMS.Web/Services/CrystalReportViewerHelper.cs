using System;
using System.Data;
using System.IO;
using System.Web.Hosting;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;

namespace FCAMS.Web.Services
{
    /// <summary>
    /// Real Crystal Reports integration code. This class is fully functional
    /// AS SOON AS the two .rpt files referenced below exist under
    /// FCAMS.Web/Reports/ -- they are not included because a .rpt is a
    /// binary file authored in the Crystal Reports for Visual Studio
    /// designer (Solution Explorer > Add New Item > Crystal Report), not
    /// something that can be produced as source text.
    ///
    /// See Docs/CRYSTAL_REPORTS_SETUP.md for the exact field layout to
    /// recreate in the designer -- it mirrors ComplianceSummaryRow /
    /// AuditTrailRow exactly, so the "Database Expert > ADO.NET DataSet"
    /// step in the designer, and the SetDataSource call below, line up
    /// one-to-one.
    ///
    /// Until the .rpt files are created, ReportsController serves the
    /// iTextSharp-generated PDF (PdfReportBuilder) from the exact same
    /// stored procedures, so the *feature* -- generate and download a
    /// compliance report -- is fully working today; only the specific
    /// Crystal Reports *rendering engine* is a drop-in you add locally
    /// once you have the designer available (Crystal Reports for Visual
    /// Studio is a separate SAP-distributed installer, not part of a
    /// default VS install).
    /// </summary>
    public static class CrystalReportViewerHelper
    {
        private const string ComplianceSummaryReportFile = "~/Reports/ComplianceSummary.rpt";
        private const string AuditTrailReportFile = "~/Reports/AuditTrail.rpt";

        public static byte[] ExportComplianceSummaryToPdf(DataTable complianceSummaryData, DateTime dateFrom, DateTime dateTo)
        {
            string path = HostingEnvironment.MapPath(ComplianceSummaryReportFile);
            if (path == null || !File.Exists(path))
            {
                throw new FileNotFoundException(
                    "ComplianceSummary.rpt was not found. Create it in the Crystal Reports designer per " +
                    "Docs/CRYSTAL_REPORTS_SETUP.md, or use ReportService.GenerateComplianceSummaryPdf() " +
                    "for the equivalent iTextSharp-rendered PDF in the meantime.", path);
            }

            using (var reportDocument = new ReportDocument())
            {
                reportDocument.Load(path);
                reportDocument.SetDataSource(complianceSummaryData);
                reportDocument.SetParameterValue("DateFrom", dateFrom);
                reportDocument.SetParameterValue("DateTo", dateTo);

                using (var stream = (MemoryStream)reportDocument.ExportToStream(ExportFormatType.PortableDocFormat))
                {
                    return stream.ToArray();
                }
            }
        }

        public static byte[] ExportAuditTrailToPdf(DataTable auditTrailData, DateTime dateFrom, DateTime dateTo)
        {
            string path = HostingEnvironment.MapPath(AuditTrailReportFile);
            if (path == null || !File.Exists(path))
            {
                throw new FileNotFoundException(
                    "AuditTrail.rpt was not found. Create it in the Crystal Reports designer per " +
                    "Docs/CRYSTAL_REPORTS_SETUP.md, or use ReportService.GenerateAuditTrailPdf() " +
                    "for the equivalent iTextSharp-rendered PDF in the meantime.", path);
            }

            using (var reportDocument = new ReportDocument())
            {
                reportDocument.Load(path);
                reportDocument.SetDataSource(auditTrailData);
                reportDocument.SetParameterValue("DateFrom", dateFrom);
                reportDocument.SetParameterValue("DateTo", dateTo);

                using (var stream = (MemoryStream)reportDocument.ExportToStream(ExportFormatType.PortableDocFormat))
                {
                    return stream.ToArray();
                }
            }
        }
    }
}
