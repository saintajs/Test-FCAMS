# FCAMS &mdash; Financial Compliance & Audit Management System

A complete, runnable ASP.NET MVC 5 (.NET Framework 4.8) application for tracking financial
transactions, evaluating them against a configurable compliance rule engine, managing audit
engagements and findings, and producing PDF / Crystal Reports reporting &mdash; built as a
realistic, explainable student portfolio project.

This is **not** a scaffold or a "hello world" MVC app. It has a real (if intentionally scoped-down)
domain model: transactions get evaluated against six independently unit-tested compliance rules the
moment they're recorded, every state change is written to an immutable audit trail, every unhandled
exception is centrally logged, and every screen you'd expect in a system like this &mdash; a
dashboard, a transaction ledger, a violations worklist, audit engagements with findings, and a
reporting module &mdash; is fully wired up to a real SQL Server schema through stored procedures.

## Tech stack

| Layer            | Technology                                                          |
|-------------------|----------------------------------------------------------------------|
| Web framework      | ASP.NET MVC 5.2.9, .NET Framework 4.8, C#                          |
| Data access         | Pure ADO.NET (`SqlDataAccess.cs`) + SQL Server stored procedures &mdash; no ORM |
| Database              | Microsoft SQL Server 2019+ / Azure SQL Database                    |
| Authentication          | ASP.NET Forms Authentication + a custom `Users` table (salted SHA-256) |
| Reporting                 | iTextSharp-generated PDF (working today) + Crystal Reports integration code (drop-in once you add the two `.rpt` designer files) |
| Testing                     | MSTest + Moq, 25 unit tests covering the rule engine, auth, and paging |
| Deployment                    | Azure App Service (Windows) + Azure SQL Database                   |

## Why these choices (for the portfolio conversation)

- **No ORM.** Every query is a named stored procedure called through a small, explicit ADO.NET
  wrapper (`DAL/SqlDataAccess.cs`). This keeps the SQL Server-side optimization work (indexes,
  covering indexes, `OFFSET/FETCH` paging) visible and attributable, and makes "show me the query
  plan for this screen" a concrete, answerable question in an interview.
- **A data-driven rule engine.** `ComplianceRules` is a table, not a switch statement of magic
  numbers. Toggling a rule on/off, or changing its dollar threshold, is an Admin screen action with
  no redeploy. The *evaluation logic* for each rule type still lives in C# (`ComplianceRuleEngine.cs`)
  and is unit tested, which is the right split: "what counts as suspicious" is source-controlled and
  testable; "is this particular rule turned on right now, at what threshold" is data.
- **Explicit audit logging**, not an EF interceptor or a generic "log everything" filter. Controllers
  call `AuditLogService.LogCreate/LogUpdate/LogDelete` at the point where they actually have the old
  and new domain values, which produces a genuinely useful before/after audit trail instead of raw
  HTTP form dumps.
- **Honest reporting.** Crystal Reports `.rpt` files are binary, GUI-designed artifacts that cannot
  be authored as source text. Rather than fake one, this project ships full Crystal Reports
  integration *code* (`Services/CrystalReportViewerHelper.cs`) that's ready to use the moment you add
  the two report files in the designer, **and** a fully working PDF export today via iTextSharp
  (`Services/PdfReportBuilder.cs`) driven by the same stored procedures. See
  `Docs/CRYSTAL_REPORTS_SETUP.md`.

## Project layout

```
FCAMS/
├── FCAMS.sln
├── FCAMS.Web/                    ASP.NET MVC 5 application
│   ├── Controllers/              Account, Home, Transactions, Compliance, Audit, Reports, Admin, Error
│   ├── Models/                   Domain models + ViewModels
│   ├── DAL/                      SqlDataAccess (ADO.NET core) + Repositories (one per entity)
│   ├── Services/                 ComplianceRuleEngine, AuthenticationService, AuditLogService,
│   │                             ExceptionLogService, ReportService, PdfReportBuilder, PasswordHasher
│   ├── Infrastructure/           GlobalExceptionFilter, AuditableActionAttribute, AuthorizeRoleAttribute
│   ├── Views/                    Razor views, one folder per controller
│   ├── Reports/                  Where ComplianceSummary.rpt / AuditTrail.rpt go (see README.txt there)
│   └── Content, Scripts          site.css, app.js
├── FCAMS.Tests/                  MSTest unit test project (25 tests)
├── Database/
│   ├── Tables/01_CreateTables.sql
│   ├── Indexes/02_Indexes.sql
│   ├── Seed/03_SeedData.sql
│   └── StoredProcedures/         sp_Users, sp_Transactions, sp_Compliance, sp_Audit, sp_Logging_Reports
└── Docs/
    ├── ARCHITECTURE.md
    ├── DATABASE.md
    ├── SECURITY.md
    ├── CRYSTAL_REPORTS_SETUP.md
    └── AZURE_DEPLOYMENT.md
```

## Getting started locally

### Prerequisites
- Visual Studio 2019 or 2022 with the "ASP.NET and web development" workload
- SQL Server Express / Developer / LocalDB, or a full SQL Server instance
- .NET Framework 4.8 Developer Pack

### 1. Create the database
Open SQL Server Management Studio (or `sqlcmd`) against your instance and run, in order:

```sql
CREATE DATABASE FCAMS_DB;
GO
```

Then run these four scripts, in order, against `FCAMS_DB`:

```
Database/Tables/01_CreateTables.sql
Database/Indexes/02_Indexes.sql
Database/Seed/03_SeedData.sql
Database/StoredProcedures/sp_Users.sql
Database/StoredProcedures/sp_Transactions.sql
Database/StoredProcedures/sp_Compliance.sql
Database/StoredProcedures/sp_Audit.sql
Database/StoredProcedures/sp_Logging_Reports.sql
```

(`sqlcmd -S .\SQLEXPRESS -d FCAMS_DB -i Database\Tables\01_CreateTables.sql`, repeated per file, works
fine from a terminal too.)

### 2. Point the app at your database
Edit `FCAMS.Web/Web.config`, `connectionStrings` section, if your instance name isn't
`.\SQLEXPRESS` or you're not using Windows Integrated Security.

### 3. Restore NuGet packages and build
Open `FCAMS.sln` in Visual Studio &rarr; it will prompt to restore NuGet packages
(`Microsoft.AspNet.Mvc`, `Newtonsoft.Json`, `iTextSharp`, `jQuery`, `bootstrap`, `MSTest.TestFramework`,
`Moq`, etc. &mdash; see `FCAMS.Web/packages.config` and `FCAMS.Tests/packages.config`) &rarr; Build Solution.

### 4. Run
F5. The app opens to the login page. Sign in with one of the seeded demo accounts:

| Username     | Password      | Role               |
|---------------|----------------|---------------------|
| `admin`        | `Admin@123`     | Administrator        |
| `compliance`     | `Comply@123`     | Compliance Officer     |
| `auditor`         | `Audit@123`       | Auditor                 |

### 5. Run the tests
Test Explorer in Visual Studio, or:
```
vstest.console.exe FCAMS.Tests\bin\Debug\FCAMS.Tests.dll
```

## What to try

1. **Record a transaction** (Transactions &gt; + Record Transaction) for $30,000 on a Saturday &mdash;
   watch it get auto-flagged by three rules at once (amount threshold, round-dollar, weekend
   posting), each producing its own violation row.
2. **Review it** under Compliance &gt; Violations, resolve or mark it a false positive &mdash; the
   resolution note becomes part of the permanent trail.
3. **Toggle a rule off** under Compliance &gt; Rules and repeat step 1 &mdash; no redeploy needed.
4. **Open an audit engagement**, raise a finding linked to that transaction.
5. **Generate a Compliance Summary or Audit Trail PDF** under Reports.
6. **As `admin`**, check Admin &gt; System Audit Trail to see every one of the actions above logged
   with who/what/when, and Admin &gt; Exception Log (empty, unless you've hit an error).

## Known, intentional scope limits

This is a portfolio project, not a production compliance platform. Documented explicitly rather than
silently:
- Password hashing is salted SHA-256, not PBKDF2/bcrypt/Argon2 &mdash; see `Docs/SECURITY.md` for why,
  and what a production system should use instead.
- The "missing approval" rule sweep is designed to be re-run periodically (e.g. via a SQL Server Agent
  job or an Azure WebJob) over all `Pending` transactions, but no scheduler is wired up here &mdash;
  see `Docs/ARCHITECTURE.md`.
- No multi-tenancy, no SSO/Azure AD integration, no e-signature workflow.
- Crystal Reports `.rpt` files are not included (see `FCAMS.Web/Reports/README.txt`) &mdash; the
  integration code is real and ready, but the binary report definitions must be authored locally in
  the Crystal Reports designer, which is a separate install.
