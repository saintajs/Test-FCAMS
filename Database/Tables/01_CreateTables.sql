/* ============================================================================
   Financial Compliance & Audit Management System (FCAMS)
   Script: 01_CreateTables.sql
   Purpose: Creates the core schema for the system.
   Target : Microsoft SQL Server 2019+ (works on Azure SQL Database too)
   Run order: 01 -> 02 (indexes) -> 03 (seed) -> all SQL files in StoredProcedures
   ============================================================================ */

IF DB_ID('FCAMS') IS NULL
BEGIN
    PRINT 'Run this script while connected to the FCAMS database (create it first via CREATE DATABASE FCAMS;)';
END
GO

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   1. Roles - static reference table for RBAC
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL DROP TABLE dbo.Roles;
GO
CREATE TABLE dbo.Roles
(
    RoleId          INT             IDENTITY(1,1)   NOT NULL,
    RoleName        NVARCHAR(50)                    NOT NULL,
    Description     NVARCHAR(200)   NULL,
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (RoleId),
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
);
GO

/* ---------------------------------------------------------------------------
   2. Departments
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Departments', 'U') IS NOT NULL DROP TABLE dbo.Departments;
GO
CREATE TABLE dbo.Departments
(
    DepartmentId    INT             IDENTITY(1,1)   NOT NULL,
    DepartmentName  NVARCHAR(100)                   NOT NULL,
    CostCenterCode  NVARCHAR(20)                    NOT NULL,
    IsActive        BIT             NOT NULL        CONSTRAINT DF_Departments_IsActive DEFAULT (1),
    CONSTRAINT PK_Departments PRIMARY KEY CLUSTERED (DepartmentId),
    CONSTRAINT UQ_Departments_CostCenterCode UNIQUE (CostCenterCode)
);
GO

/* ---------------------------------------------------------------------------
   3. Users
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO
CREATE TABLE dbo.Users
(
    UserId          INT             IDENTITY(1,1)   NOT NULL,
    Username        NVARCHAR(50)                    NOT NULL,
    Email           NVARCHAR(150)                   NOT NULL,
    FullName        NVARCHAR(150)                   NOT NULL,
    PasswordHash    VARBINARY(64)                   NOT NULL,   -- SHA-256 hash
    PasswordSalt    VARBINARY(32)                   NOT NULL,   -- random salt, per user
    RoleId          INT                             NOT NULL,
    DepartmentId    INT             NULL,
    IsActive        BIT             NOT NULL        CONSTRAINT DF_Users_IsActive DEFAULT (1),
    FailedLoginCount INT            NOT NULL        CONSTRAINT DF_Users_FailedLoginCount DEFAULT (0),
    LockedOutUntil  DATETIME2(0)    NULL,
    CreatedDate     DATETIME2(0)    NOT NULL        CONSTRAINT DF_Users_CreatedDate DEFAULT (SYSUTCDATETIME()),
    LastLoginDate   DATETIME2(0)    NULL,
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (UserId),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId),
    CONSTRAINT FK_Users_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentId)
);
GO

/* ---------------------------------------------------------------------------
   4. FinancialTransactions - the transactional data compliance rules run over
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.FinancialTransactions', 'U') IS NOT NULL DROP TABLE dbo.FinancialTransactions;
GO
CREATE TABLE dbo.FinancialTransactions
(
    TransactionId       BIGINT          IDENTITY(1,1)   NOT NULL,
    TransactionRef      NVARCHAR(30)                    NOT NULL,   -- external/business reference number
    TransactionDate     DATE                            NOT NULL,
    PostedDate          DATETIME2(0)    NOT NULL        CONSTRAINT DF_FinTxn_PostedDate DEFAULT (SYSUTCDATETIME()),
    Amount              DECIMAL(18,2)                   NOT NULL,
    CurrencyCode        CHAR(3)                         NOT NULL    CONSTRAINT DF_FinTxn_Currency DEFAULT ('USD'),
    AccountCode         NVARCHAR(20)                    NOT NULL,
    DepartmentId        INT                             NOT NULL,
    CounterpartyName    NVARCHAR(200)   NULL,
    Description         NVARCHAR(500)   NULL,
    Status              NVARCHAR(20)                    NOT NULL    CONSTRAINT DF_FinTxn_Status DEFAULT ('Pending'),
        -- Status values enforced by CK_FinTxn_Status: Pending, Approved, Rejected, Flagged, Reversed
    CreatedByUserId     INT                             NOT NULL,
    CreatedDate         DATETIME2(0)    NOT NULL        CONSTRAINT DF_FinTxn_CreatedDate DEFAULT (SYSUTCDATETIME()),
    ModifiedDate        DATETIME2(0)    NULL,
    RowVersion          ROWVERSION,
    CONSTRAINT PK_FinancialTransactions PRIMARY KEY CLUSTERED (TransactionId),
    CONSTRAINT UQ_FinTxn_TransactionRef UNIQUE (TransactionRef),
    CONSTRAINT FK_FinTxn_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentId),
    CONSTRAINT FK_FinTxn_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_FinTxn_Status CHECK (Status IN ('Pending','Approved','Rejected','Flagged','Reversed')),
    CONSTRAINT CK_FinTxn_Amount CHECK (Amount <> 0)
);
GO

/* ---------------------------------------------------------------------------
   5. ComplianceRules - configurable rule catalog (data-driven, not hardcoded)
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.ComplianceRules', 'U') IS NOT NULL DROP TABLE dbo.ComplianceRules;
GO
CREATE TABLE dbo.ComplianceRules
(
    RuleId              INT             IDENTITY(1,1)   NOT NULL,
    RuleCode            NVARCHAR(20)                    NOT NULL,
    RuleName            NVARCHAR(150)                   NOT NULL,
    Description         NVARCHAR(500)   NULL,
    RuleType            NVARCHAR(30)                    NOT NULL,
        -- AmountThreshold | DuplicateTransaction | RoundDollar | WeekendPosting | MissingApproval | VelocityCheck
    ThresholdValue      DECIMAL(18,2)   NULL,
    ThresholdWindowDays INT             NULL,           -- for velocity/duplicate checks
    Severity            NVARCHAR(20)                    NOT NULL,  -- Low | Medium | High | Critical
    IsActive            BIT             NOT NULL        CONSTRAINT DF_Rules_IsActive DEFAULT (1),
    CreatedDate         DATETIME2(0)    NOT NULL        CONSTRAINT DF_Rules_CreatedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_ComplianceRules PRIMARY KEY CLUSTERED (RuleId),
    CONSTRAINT UQ_ComplianceRules_RuleCode UNIQUE (RuleCode),
    CONSTRAINT CK_ComplianceRules_Severity CHECK (Severity IN ('Low','Medium','High','Critical')),
    CONSTRAINT CK_ComplianceRules_RuleType CHECK (RuleType IN
        ('AmountThreshold','DuplicateTransaction','RoundDollar','WeekendPosting','MissingApproval','VelocityCheck'))
);
GO

/* ---------------------------------------------------------------------------
   6. ComplianceViolations - result of the rule engine evaluating a transaction
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.ComplianceViolations', 'U') IS NOT NULL DROP TABLE dbo.ComplianceViolations;
GO
CREATE TABLE dbo.ComplianceViolations
(
    ViolationId         BIGINT          IDENTITY(1,1)   NOT NULL,
    TransactionId       BIGINT                          NOT NULL,
    RuleId              INT                             NOT NULL,
    DetectedDate        DATETIME2(0)    NOT NULL        CONSTRAINT DF_Violations_DetectedDate DEFAULT (SYSUTCDATETIME()),
    Severity            NVARCHAR(20)                    NOT NULL,
    DetailMessage       NVARCHAR(1000)                  NOT NULL,
    Status              NVARCHAR(20)                    NOT NULL   CONSTRAINT DF_Violations_Status DEFAULT ('Open'),
        -- Open | UnderReview | Resolved | FalsePositive
    ResolvedByUserId    INT             NULL,
    ResolvedDate        DATETIME2(0)    NULL,
    ResolutionNotes     NVARCHAR(1000)  NULL,
    CONSTRAINT PK_ComplianceViolations PRIMARY KEY CLUSTERED (ViolationId),
    CONSTRAINT FK_Violations_Transactions FOREIGN KEY (TransactionId) REFERENCES dbo.FinancialTransactions (TransactionId),
    CONSTRAINT FK_Violations_Rules FOREIGN KEY (RuleId) REFERENCES dbo.ComplianceRules (RuleId),
    CONSTRAINT FK_Violations_ResolvedBy FOREIGN KEY (ResolvedByUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Violations_Status CHECK (Status IN ('Open','UnderReview','Resolved','FalsePositive'))
);
GO

/* ---------------------------------------------------------------------------
   7. AuditEngagements - a planned/ongoing audit of a department or period
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.AuditEngagements', 'U') IS NOT NULL DROP TABLE dbo.AuditEngagements;
GO
CREATE TABLE dbo.AuditEngagements
(
    AuditId             INT             IDENTITY(1,1)   NOT NULL,
    AuditName           NVARCHAR(200)                   NOT NULL,
    DepartmentId        INT                             NOT NULL,
    LeadAuditorUserId   INT                             NOT NULL,
    PeriodStart         DATE                            NOT NULL,
    PeriodEnd           DATE                            NOT NULL,
    Status              NVARCHAR(20)                    NOT NULL   CONSTRAINT DF_Audit_Status DEFAULT ('Planned'),
        -- Planned | InProgress | Completed | Closed
    Scope                NVARCHAR(1000)  NULL,
    CreatedDate         DATETIME2(0)    NOT NULL        CONSTRAINT DF_Audit_CreatedDate DEFAULT (SYSUTCDATETIME()),
    CompletedDate       DATETIME2(0)    NULL,
    CONSTRAINT PK_AuditEngagements PRIMARY KEY CLUSTERED (AuditId),
    CONSTRAINT FK_Audit_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments (DepartmentId),
    CONSTRAINT FK_Audit_LeadAuditor FOREIGN KEY (LeadAuditorUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Audit_Status CHECK (Status IN ('Planned','InProgress','Completed','Closed')),
    CONSTRAINT CK_Audit_PeriodOrder CHECK (PeriodEnd >= PeriodStart)
);
GO

/* ---------------------------------------------------------------------------
   8. AuditFindings - specific findings raised during an audit engagement
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.AuditFindings', 'U') IS NOT NULL DROP TABLE dbo.AuditFindings;
GO
CREATE TABLE dbo.AuditFindings
(
    FindingId           BIGINT          IDENTITY(1,1)   NOT NULL,
    AuditId              INT                            NOT NULL,
    TransactionId        BIGINT          NULL,
    ViolationId          BIGINT          NULL,
    FindingTitle          NVARCHAR(200)                 NOT NULL,
    FindingDescription    NVARCHAR(2000)                NOT NULL,
    Severity              NVARCHAR(20)                  NOT NULL,
    Status                 NVARCHAR(20)   NOT NULL       CONSTRAINT DF_Findings_Status DEFAULT ('Raised'),
        -- Raised | InRemediation | Closed | Accepted (risk accepted)
    RaisedByUserId        INT                            NOT NULL,
    RaisedDate            DATETIME2(0)   NOT NULL        CONSTRAINT DF_Findings_RaisedDate DEFAULT (SYSUTCDATETIME()),
    DueDate                DATE           NULL,
    ClosedDate             DATETIME2(0)  NULL,
    CONSTRAINT PK_AuditFindings PRIMARY KEY CLUSTERED (FindingId),
    CONSTRAINT FK_Findings_Audit FOREIGN KEY (AuditId) REFERENCES dbo.AuditEngagements (AuditId),
    CONSTRAINT FK_Findings_Transactions FOREIGN KEY (TransactionId) REFERENCES dbo.FinancialTransactions (TransactionId),
    CONSTRAINT FK_Findings_Violations FOREIGN KEY (ViolationId) REFERENCES dbo.ComplianceViolations (ViolationId),
    CONSTRAINT FK_Findings_RaisedBy FOREIGN KEY (RaisedByUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_Findings_Severity CHECK (Severity IN ('Low','Medium','High','Critical')),
    CONSTRAINT CK_Findings_Status CHECK (Status IN ('Raised','InRemediation','Closed','Accepted'))
);
GO

/* ---------------------------------------------------------------------------
   9. AuditLogs - immutable system-wide activity trail (who did what, when)
      This is separate from AuditEngagements/AuditFindings, which model the
      *business process* of auditing. AuditLogs is the *technical* trail of
      every create/update/delete/login the application performs.
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.AuditLogs', 'U') IS NOT NULL DROP TABLE dbo.AuditLogs;
GO
CREATE TABLE dbo.AuditLogs
(
    AuditLogId          BIGINT          IDENTITY(1,1)   NOT NULL,
    UserId               INT             NULL,           -- NULL for anonymous/system actions
    Username             NVARCHAR(50)    NULL,           -- denormalized snapshot, survives user deletion
    ActionType            NVARCHAR(30)                   NOT NULL,  -- Create | Update | Delete | Login | LoginFailed | Logout | View | Export
    EntityName            NVARCHAR(100)                  NOT NULL,  -- e.g. 'FinancialTransaction'
    EntityId               NVARCHAR(50)   NULL,
    OldValues              NVARCHAR(MAX)  NULL,           -- JSON snapshot before change
    NewValues              NVARCHAR(MAX)  NULL,           -- JSON snapshot after change
    IPAddress               NVARCHAR(45)  NULL,
    UserAgent               NVARCHAR(300) NULL,
    Timestamp                DATETIME2(3) NOT NULL       CONSTRAINT DF_AuditLogs_Timestamp DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (AuditLogId),
    CONSTRAINT CK_AuditLogs_ActionType CHECK (ActionType IN
        ('Create','Update','Delete','Login','LoginFailed','Logout','View','Export'))
);
GO

/* ---------------------------------------------------------------------------
   10. ExceptionLogs - centralized application exception logging
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.ExceptionLogs', 'U') IS NOT NULL DROP TABLE dbo.ExceptionLogs;
GO
CREATE TABLE dbo.ExceptionLogs
(
    ExceptionLogId       BIGINT          IDENTITY(1,1)  NOT NULL,
    Source                 NVARCHAR(200)                NOT NULL,  -- Controller/Action or Service.Method
    Message                 NVARCHAR(2000)               NOT NULL,
    ExceptionType             NVARCHAR(300) NULL,
    StackTrace                NVARCHAR(MAX) NULL,
    InnerException             NVARCHAR(MAX) NULL,
    Severity                    NVARCHAR(20) NOT NULL    CONSTRAINT DF_ExLog_Severity DEFAULT ('Error'),
        -- Warning | Error | Critical
    RequestUrl                   NVARCHAR(500) NULL,
    UserId                        INT           NULL,
    Timestamp                      DATETIME2(3) NOT NULL CONSTRAINT DF_ExLog_Timestamp DEFAULT (SYSUTCDATETIME()),
    IsResolved                      BIT          NOT NULL CONSTRAINT DF_ExLog_IsResolved DEFAULT (0),
    CONSTRAINT PK_ExceptionLogs PRIMARY KEY CLUSTERED (ExceptionLogId),
    CONSTRAINT CK_ExLog_Severity CHECK (Severity IN ('Warning','Error','Critical'))
);
GO

/* ---------------------------------------------------------------------------
   11. GeneratedReports - metadata for every report run (for the report history
       screen and to physically locate exported PDF/Crystal Reports output)
--------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.GeneratedReports', 'U') IS NOT NULL DROP TABLE dbo.GeneratedReports;
GO
CREATE TABLE dbo.GeneratedReports
(
    ReportId              BIGINT         IDENTITY(1,1)  NOT NULL,
    ReportName             NVARCHAR(150)                NOT NULL,
    ReportType               NVARCHAR(40)                NOT NULL, -- ComplianceSummary | AuditTrail | ViolationDetail
    ParametersJson             NVARCHAR(1000) NULL,
    GeneratedByUserId            INT           NOT NULL,
    GeneratedDate                  DATETIME2(0) NOT NULL CONSTRAINT DF_Reports_GeneratedDate DEFAULT (SYSUTCDATETIME()),
    OutputFormat                    NVARCHAR(10) NOT NULL CONSTRAINT DF_Reports_Format DEFAULT ('PDF'),
    FilePath                          NVARCHAR(400) NULL,
    CONSTRAINT PK_GeneratedReports PRIMARY KEY CLUSTERED (ReportId),
    CONSTRAINT FK_Reports_Users FOREIGN KEY (GeneratedByUserId) REFERENCES dbo.Users (UserId)
);
GO

PRINT 'FCAMS core tables created successfully.';
GO
