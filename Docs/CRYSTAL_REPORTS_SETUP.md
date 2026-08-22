# Setting Up Crystal Reports in FCAMS

## Why this is a separate step

A Crystal Reports `.rpt` file is a **binary file** produced by the Crystal Reports designer -- a
drag-and-drop GUI tool distributed separately by SAP, not something that ships inside Visual Studio by
default and not something that can be authored as source code or text. That's why this repository
does not include `ComplianceSummary.rpt` or `AuditTrail.rpt`: including a fake or empty one would be
worse than including nothing, since it would silently fail or mislead rather than clearly document
what's needed.

What *is* included and fully working without this step:
- `Services/PdfReportBuilder.cs` -- generates both reports as PDF via iTextSharp, from the exact same
  stored procedures a Crystal Report would use.
- `Services/CrystalReportViewerHelper.cs` -- the real `CrystalDecisions.CrystalReports.Engine`
  integration code (`ReportDocument.Load`, `.SetDataSource`, `.SetParameterValue`, `.ExportToStream`),
  ready to run the moment the two `.rpt` files exist.

## Step 1 -- Install the Crystal Reports designer

Download "SAP Crystal Reports, developer version for Microsoft Visual Studio" (often abbreviated
"CR for VS") from SAP's developer downloads. It's free for development use, requires a (free)
registration, and installs as a Visual Studio extension plus a redistributable runtime. Confirm the
version matches (or is compatible with) the `CrystalDecisions.*` assembly version referenced in
`FCAMS.Web.csproj` (13.0.4000.0 at time of writing) -- if SAP has published a newer version, update
that reference to match what the installer registers.

## Step 2 -- Add the report files to the project

In Visual Studio Solution Explorer:
1. Right-click `FCAMS.Web/Reports` &rarr; **Add &rarr; New Item**.
2. Choose **Reporting &rarr; Crystal Report**, name it `ComplianceSummary.rpt`.
3. In the Crystal Reports Gallery, choose **Using the Report Wizard &rarr; Standard**, then
   **Create a New Connection &rarr; ADO.NET (XML)** -- point it at a `DataSet`/`DataTable` shape
   matching `ComplianceSummaryRow` (below), rather than connecting live to SQL Server. This keeps the
   report decoupled from a live connection string at design time; at runtime,
   `CrystalReportViewerHelper.ExportComplianceSummaryToPdf()` calls `.SetDataSource()` with a real
   `DataTable` built from `usp_Report_ComplianceSummary`'s results.
4. Repeat for `AuditTrail.rpt`, matching the `AuditTrailRow` shape.

## Step 3 -- Field layout to recreate

### ComplianceSummary.rpt
Matches `DAL/Repositories/ReportRepository.cs`'s `ComplianceSummaryRow`, which is populated from
`dbo.usp_Report_ComplianceSummary`:

| Field              | Type      | Source column         |
|---------------------|-----------|--------------------------|
| Rule Code             | String     | `RuleCode`                |
| Rule Name               | String     | `RuleName`                  |
| Severity                  | String     | `Severity`                    |
| Violation Count             | Number     | `ViolationCount`                |
| Total Amount At Risk           | Currency   | `TotalAmountAtRisk`               |
| Open Count                        | Number     | `OpenCount`                          |
| Resolved Count                       | Number     | `ResolvedCount`                        |
| False Positive Count                    | Number     | `FalsePositiveCount`                      |

Add two report parameters, `DateFrom` and `DateTo` (Date type), and reference them in the report
header text ("Period: {?DateFrom} - {?DateTo}") -- these are the same parameters
`CrystalReportViewerHelper` sets via `SetParameterValue`.

Group by `RuleCode`, with a summary footer totaling `ViolationCount` and `TotalAmountAtRisk` --
mirrors the grand-total line at the bottom of the PDF version in `PdfReportBuilder.BuildComplianceSummary`.

### AuditTrail.rpt
Matches `AuditTrailRow`, from `dbo.usp_Report_AuditTrail`:

| Field         | Type      | Source column   |
|-----------------|-----------|-------------------|
| Username          | String     | `Username`          |
| Action Type          | String     | `ActionType`           |
| Entity Name             | String     | `EntityName`              |
| Entity Id                  | String     | `EntityId`                   |
| IP Address                    | String     | `IPAddress`                     |
| Timestamp                        | DateTime   | `Timestamp`                        |

Same `DateFrom`/`DateTo` parameters. Landscape orientation recommended (matches the PDF version).

## Step 4 -- Wire the controller to use Crystal instead of (or alongside) the PDF path

`ReportsController` currently always calls `IReportService` (the iTextSharp path). Once your `.rpt`
files exist, you can add a second action, e.g.:

```csharp
[HttpPost]
public ActionResult ComplianceSummaryCrystal(ReportParametersViewModel model)
{
    var rows = _reportRepository.GetComplianceSummary(model.DateFrom, model.DateTo);
    var table = ToDataTable(rows); // simple reflection-based mapper, or fill a typed DataTable directly
    var pdfBytes = CrystalReportViewerHelper.ExportComplianceSummaryToPdf(table, model.DateFrom, model.DateTo);
    return File(pdfBytes, "application/pdf", "ComplianceSummary.pdf");
}
```

and add a second button in `Views/Reports/Index.cshtml` alongside the existing one. Both paths can
coexist indefinitely -- they share the same stored procedures, so the numbers always agree.

## Step 5 -- Deploying Crystal Reports to Azure App Service

The Crystal Reports runtime is a native/COM-adjacent dependency that does **not** deploy cleanly to
Azure App Service's sandboxed hosting model without the "Crystal Reports redistributable" MSI being
present on the host, which you don't control on a standard (non-VM) App Service plan. In practice,
teams either:
- Skip Crystal Reports entirely for the cloud deployment and rely on the PDF path (recommended for
  this project -- see `Docs/AZURE_DEPLOYMENT.md`), or
- Host the Crystal Reports-dependent piece on an Azure VM or Azure App Service **Environment**
  (dedicated, not the shared sandbox) where you can install the redistributable yourself.

This is exactly why `ReportService`/`PdfReportBuilder` exist as the primary, always-available
reporting path in this project.
