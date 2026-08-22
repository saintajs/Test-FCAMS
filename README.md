# FCAMS — Financial Compliance & Audit Management System

A complete ASP.NET MVC 5 (.NET Framework 4.8) application for managing financial transactions, compliance rules and violations, audit engagements, audit findings, audit trails, users, and reports. Built as a realistic student portfolio project demonstrating enterprise-style .NET application development.

## Tech Stack

| Layer | Technology |
| Web Framework | ASP.NET MVC 5.2.9, C#, .NET Framework 4.8 |
| Database | Microsoft SQL Server |
| Data Access | ADO.NET + SQL Server Stored Procedures |
| Authentication | ASP.NET Forms Authentication |
| Authorization | Role-based access control |
| Password Security | Salted SHA-256 |
| Reporting | iTextSharp PDF + Crystal Reports integration |
| Testing | MSTest + Moq |
| Deployment | Windows/IIS or Azure App Service |

## Features

- User authentication and role-based authorization
- Administrator user management
- Financial transaction management
- Configurable compliance rules
- Automatic compliance violation detection
- Violation review, resolution and false-positive handling
- Audit engagement and finding management
- Finding severity, status and due-date tracking
- Audit trail and activity logging
- Centralized exception handling
- PDF report generation
- Crystal Reports integration
- SQL Server stored procedures and indexes
- Unit testing of core services

## Architecture

Browser
   ↓
ASP.NET MVC Controllers
   ↓
Services / Business Logic
   ↓
Repositories / ADO.NET
   ↓
SQL Server Stored Procedures
   ↓
Database


Controllers handle requests, Services contain business logic, Repositories handle database operations, and SQL Server stored procedures perform database-side operations.

`ComplianceRuleEngine` evaluates transactions against configured compliance rules. `AuditLogService` records important application activity. `AuthenticationService` handles authentication. `AuthorizeRoleAttribute` protects role-specific actions. `GlobalExceptionFilter` provides centralized exception handling. `PdfReportBuilder` generates PDF reports, while `CrystalReportViewerHelper` provides Crystal Reports integration.

## Project Structure


FCAMS/
├── FCAMS.sln
├── Controllers/
├── FCAMS.Web/
│   ├── Controllers/        MVC request handling
│   ├── Models/             Domain models and ViewModels
│   ├── DAL/                ADO.NET and repositories
│   ├── Services/           Business logic and reporting
│   ├── Infrastructure/     Authorization, auditing, exceptions
│   ├── Views/              Razor UI
│   ├── Content/            CSS
│   ├── Scripts/            JavaScript
│   └── Web.config
├── FCAMS.Tests/             MSTest unit tests
├── Database/
│   ├── Tables/             Database tables
│   ├── Indexes/            Database indexes
│   ├── Seed/               Initial/demo data
│   └── StoredProcedures/   Database operations
└── Docs/                    Technical documentation


## Database

The `Database` folder contains the complete SQL Server setup:

- `01_CreateTables.sql` — creates database tables
- `02_Indexes.sql` — creates indexes
- `03_SeedData.sql` — inserts initial/demo data
- `sp_Users.sql` — user operations
- `sp_Users_Admin.sql` — administrator user operations
- `sp_Roles_Admin.sql` — role administration
- `sp_Transactions.sql` — transaction operations
- `sp_Compliance.sql` — compliance rules and violations
- `sp_Audit.sql` — audits and findings
- `sp_Logging_Reports.sql` — logging and reporting operations


### Requirements

- Visual Studio 2019 or 2022
- ASP.NET and web development workload
- .NET Framework 4.8 Developer Pack
- SQL Server / SQL Server Express / Developer / LocalDB

### Setup

git clone https://github.com/Yashu123403/Financial-Compliance-Audit-Management.git
cd Financial-Compliance-Audit-Management


Create a SQL Server database and run the scripts in `Database/` in order. Then configure the connection string in `FCAMS.Web/Web.config`.

Open `FCAMS.sln` in Visual Studio, restore NuGet packages, build the solution, and run using IIS Express.

## Application Workflow


Login
  ↓
Record Financial Transaction
  ↓
Compliance Rule Evaluation
  ↓
Violation Created if Rules Trigger
  ↓
Compliance Review
  ↓
Resolve / Mark False Positive
  ↓
Audit Trail Recorded
  ↓
Create Audit Engagement
  ↓
Raise Audit Finding
  ↓
Resolve / Close Finding
  ↓
Generate Report
```

## Testing

`FCAMS.Tests` contains MSTest unit tests covering core functionality including authentication, password hashing, compliance rule processing, and paging. Tests can be executed through Visual Studio Test Explorer.

## Reporting

The application supports PDF report generation using iTextSharp. Crystal Reports integration is also included through the reporting helper. Crystal Reports `.rpt` files require separate Crystal Reports designer setup. See `Docs/CRYSTAL_REPORTS_SETUP.md`.

## Security

This is a student/portfolio project and should not be treated as a production financial compliance system without additional security hardening. The current application uses salted SHA-256 password hashing; production systems should use modern password hashing such as PBKDF2, bcrypt, scrypt, or Argon2.

Intentional limitations include no multi-tenancy, SSO/Azure AD integration, or electronic-signature workflow.

## Documentation

Additional technical documentation is available in:

Docs/
├── ARCHITECTURE.md
├── DATABASE.md
├── SECURITY.md
├── CRYSTAL_REPORTS_SETUP.md
└── AZURE_DEPLOYMENT.md

AUTHOR 
YASHU A.B.
