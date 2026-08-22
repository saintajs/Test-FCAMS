FCAMS.Web/Reports/
================

This folder is where the two Crystal Reports definition files belong:

    ComplianceSummary.rpt
    AuditTrail.rpt

They are NOT included in this project because a .rpt file is a binary
file authored using the Crystal Reports for Visual Studio designer (a GUI
tool distributed separately by SAP) -- it cannot be hand-written as text,
so no fake/placeholder .rpt is included here.

To add them yourself:
  1. Install "SAP Crystal Reports, developer version for Microsoft Visual
     Studio" (see Docs/CRYSTAL_REPORTS_SETUP.md for the download link and
     version notes).
  2. In Solution Explorer, right-click this Reports folder > Add > New
     Item > Reporting > Crystal Report.
  3. Follow Docs/CRYSTAL_REPORTS_SETUP.md, which lists the exact field
     layout for both reports -- it mirrors the ComplianceSummaryRow /
     AuditTrailRow classes in DAL/Repositories/ReportRepository.cs field
     for field, so the designer's "Database Expert > ADO.NET DataSet" step
     matches CrystalReportViewerHelper.cs exactly.

Until you do this, FCAMS is NOT missing any functionality: the Reports
screen generates fully working PDF output for both reports today, using
iTextSharp (see Services/PdfReportBuilder.cs) driven by the exact same
stored procedures (usp_Report_ComplianceSummary / usp_Report_AuditTrail)
that the Crystal Reports would use as their data source. Adding the .rpt
files later gives you a second, richer rendering path (subreports,
charts, drill-down) without changing any data-access code.
