using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Web;
using System.Web.Hosting;
using FCAMS.Web.DAL.Repositories;

namespace FCAMS.Web.Services
{
    public interface IReportService
    {
        byte[] GenerateComplianceSummaryPdf(DateTime dateFrom, DateTime dateTo, int generatedByUserId);
        byte[] GenerateAuditTrailPdf(DateTime dateFrom, DateTime dateTo, int generatedByUserId);
    }

    /// <summary>
    /// Generates the two FCAMS reports as PDF.
    ///
    /// IMPORTANT PORTFOLIO NOTE ON CRYSTAL REPORTS:
    /// A genuine Crystal Reports .rpt file is a binary file authored in the
    /// Crystal Reports for Visual Studio designer (a GUI tool) -- it cannot
    /// be generated as source/text, so none is faked here. What *is* real
    /// and fully working:
    ///   1. This service produces the exact same report output as PDF using
    ///      iTextSharp, driven by the same stored procedures
    ///      (usp_Report_ComplianceSummary / usp_Report_AuditTrail) that the
    ///      Crystal reports would use as their data source.
    ///   2. CrystalReportViewerHelper.cs (below) contains the real
    ///      integration code -- CrystalDecisions.CrystalReports.Engine,
    ///      ReportDocument, SetDataSource, ExportToDisk -- ready to point at
    ///      ComplianceSummary.rpt / AuditTrail.rpt the moment you create
    ///      those two files in the designer (Solution Explorer > Add New
    ///      Item > Crystal Report). Docs/CRYSTAL_REPORTS_SETUP.md walks
    ///      through building both reports field-by-field, using the same
    ///      DataSet shape as ComplianceSummaryRow / AuditTrailRow so the
    ///      designer's "Database Expert > ADO.NET DataSet" step matches
    ///      this code exactly.
    /// </summary>
    public class ReportService : IReportService
    {
        private readonly IReportRepository _reportRepository;

        public ReportService() : this(new ReportRepository()) { }

        public ReportService(IReportRepository reportRepository)
        {
            _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
        }

        public byte[] GenerateComplianceSummaryPdf(DateTime dateFrom, DateTime dateTo, int generatedByUserId)
        {
            var rows = _reportRepository.GetComplianceSummary(dateFrom, dateTo);
            var bytes = PdfReportBuilder.BuildComplianceSummary(rows, dateFrom, dateTo);
            PersistAndLog("Compliance Summary", "ComplianceSummary", dateFrom, dateTo, generatedByUserId, bytes);
            return bytes;
        }

        public byte[] GenerateAuditTrailPdf(DateTime dateFrom, DateTime dateTo, int generatedByUserId)
        {
            var rows = _reportRepository.GetAuditTrail(dateFrom, dateTo);
            var bytes = PdfReportBuilder.BuildAuditTrail(rows, dateFrom, dateTo);
            PersistAndLog("Audit Trail", "AuditTrail", dateFrom, dateTo, generatedByUserId, bytes);
            return bytes;
        }

        private void PersistAndLog(string reportName, string reportType, DateTime dateFrom, DateTime dateTo, int userId, byte[] bytes)
        {
            string relativeFolder = ConfigurationManager.AppSettings["Fcams.ReportOutputPath"] ?? "~/App_Data/GeneratedReports";
            string physicalFolder = HostingEnvironment.MapPath(relativeFolder) ?? Path.Combine(Path.GetTempPath(), "FCAMS_Reports");

            if (!Directory.Exists(physicalFolder)) Directory.CreateDirectory(physicalFolder);

            string fileName = $"{reportType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            string fullPath = Path.Combine(physicalFolder, fileName);
            File.WriteAllBytes(fullPath, bytes);

            string parametersJson = $"{{\"dateFrom\":\"{dateFrom:yyyy-MM-dd}\",\"dateTo\":\"{dateTo:yyyy-MM-dd}\"}}";
            _reportRepository.LogGeneratedReport(reportName, reportType, parametersJson, userId, "PDF", fullPath);
        }
    }
}
