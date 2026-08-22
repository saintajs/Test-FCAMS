/* ============================================================================
   Stored Procedures: AuditLogs (technical trail), ExceptionLogs, Reports
   ============================================================================ */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- usp_AuditLog_Insert
-- Called by AuditLogService.cs from the global action filter on every
-- Create/Update/Delete/Login/Logout/Export action in the app.
-- =============================================
IF OBJECT_ID('dbo.usp_AuditLog_Insert', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_AuditLog_Insert;
GO
CREATE PROCEDURE dbo.usp_AuditLog_Insert
    @UserId       INT = NULL,
    @Username      NVARCHAR(50) = NULL,
    @ActionType     NVARCHAR(30),
    @EntityName      NVARCHAR(100),
    @EntityId         NVARCHAR(50) = NULL,
    @OldValues          NVARCHAR(MAX) = NULL,
    @NewValues            NVARCHAR(MAX) = NULL,
    @IPAddress              NVARCHAR(45) = NULL,
    @UserAgent                NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AuditLogs (UserId, Username, ActionType, EntityName, EntityId, OldValues, NewValues, IPAddress, UserAgent)
    VALUES (@UserId, @Username, @ActionType, @EntityName, @EntityId, @OldValues, @NewValues, @IPAddress, @UserAgent);
END
GO

-- =============================================
-- usp_AuditLog_GetRecent  (dashboard "recent activity" widget)
-- =============================================
IF OBJECT_ID('dbo.usp_AuditLog_GetRecent', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_AuditLog_GetRecent;
GO
CREATE PROCEDURE dbo.usp_AuditLog_GetRecent
    @Top INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top) AuditLogId, UserId, Username, ActionType, EntityName, EntityId, Timestamp
    FROM   dbo.AuditLogs
    ORDER BY Timestamp DESC;
END
GO

-- =============================================
-- usp_AuditLog_GetByEntity  ("show history for this record" screen)
-- Uses IX_AuditLogs_Entity.
-- =============================================
IF OBJECT_ID('dbo.usp_AuditLog_GetByEntity', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_AuditLog_GetByEntity;
GO
CREATE PROCEDURE dbo.usp_AuditLog_GetByEntity
    @EntityName NVARCHAR(100),
    @EntityId   NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT AuditLogId, UserId, Username, ActionType, OldValues, NewValues, IPAddress, Timestamp
    FROM   dbo.AuditLogs
    WHERE  EntityName = @EntityName AND EntityId = @EntityId
    ORDER BY Timestamp DESC;
END
GO

-- =============================================
-- usp_AuditLog_GetPaged  (full audit-trail search screen)
-- =============================================
IF OBJECT_ID('dbo.usp_AuditLog_GetPaged', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_AuditLog_GetPaged;
GO
CREATE PROCEDURE dbo.usp_AuditLog_GetPaged
    @UserId       INT = NULL,
    @ActionType    NVARCHAR(30) = NULL,
    @DateFrom       DATETIME2(0) = NULL,
    @DateTo          DATETIME2(0) = NULL,
    @PageNumber       INT = 1,
    @PageSize          INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize NOT BETWEEN 1 AND 500 SET @PageSize = 50;

    ;WITH Filtered AS
    (
        SELECT AuditLogId, UserId, Username, ActionType, EntityName, EntityId,
               IPAddress, Timestamp
        FROM   dbo.AuditLogs
        WHERE  (@UserId IS NULL OR UserId = @UserId)
          AND  (@ActionType IS NULL OR ActionType = @ActionType)
          AND  (@DateFrom IS NULL OR Timestamp >= @DateFrom)
          AND  (@DateTo IS NULL OR Timestamp <= @DateTo)
    )
    SELECT *, COUNT(*) OVER() AS TotalRowCount
    FROM   Filtered
    ORDER BY Timestamp DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- =============================================
-- usp_ExceptionLog_Insert
-- =============================================
IF OBJECT_ID('dbo.usp_ExceptionLog_Insert', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ExceptionLog_Insert;
GO
CREATE PROCEDURE dbo.usp_ExceptionLog_Insert
    @Source          NVARCHAR(200),
    @Message          NVARCHAR(2000),
    @ExceptionType     NVARCHAR(300) = NULL,
    @StackTrace          NVARCHAR(MAX) = NULL,
    @InnerException        NVARCHAR(MAX) = NULL,
    @Severity                NVARCHAR(20) = 'Error',
    @RequestUrl                NVARCHAR(500) = NULL,
    @UserId                      INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ExceptionLogs
        (Source, Message, ExceptionType, StackTrace, InnerException, Severity, RequestUrl, UserId)
    VALUES
        (@Source, @Message, @ExceptionType, @StackTrace, @InnerException, @Severity, @RequestUrl, @UserId);
END
GO

-- =============================================
-- usp_ExceptionLog_GetRecent
-- =============================================
IF OBJECT_ID('dbo.usp_ExceptionLog_GetRecent', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ExceptionLog_GetRecent;
GO
CREATE PROCEDURE dbo.usp_ExceptionLog_GetRecent
    @Top INT = 100,
    @UnresolvedOnly BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top) ExceptionLogId, Source, Message, ExceptionType, Severity,
           RequestUrl, UserId, Timestamp, IsResolved
    FROM   dbo.ExceptionLogs
    WHERE  (@UnresolvedOnly = 0 OR IsResolved = 0)
    ORDER BY Timestamp DESC;
END
GO

-- =============================================
-- usp_Report_ComplianceSummary
-- The primary Crystal Reports / PDF data source: violation counts grouped
-- by rule and severity for a date range, with the underlying transaction
-- total exposed at risk. Pre-aggregated in T-SQL (rather than pulled row
-- by row into .NET and grouped in memory) so the report scales with the
-- database, not with IIS worker-process memory.
-- =============================================
IF OBJECT_ID('dbo.usp_Report_ComplianceSummary', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Report_ComplianceSummary;
GO
CREATE PROCEDURE dbo.usp_Report_ComplianceSummary
    @DateFrom DATE,
    @DateTo   DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  r.RuleCode, r.RuleName, v.Severity,
            COUNT(*) AS ViolationCount,
            SUM(t.Amount) AS TotalAmountAtRisk,
            SUM(CASE WHEN v.Status IN ('Open','UnderReview') THEN 1 ELSE 0 END) AS OpenCount,
            SUM(CASE WHEN v.Status = 'Resolved' THEN 1 ELSE 0 END) AS ResolvedCount,
            SUM(CASE WHEN v.Status = 'FalsePositive' THEN 1 ELSE 0 END) AS FalsePositiveCount
    FROM    dbo.ComplianceViolations v
    INNER JOIN dbo.ComplianceRules r ON r.RuleId = v.RuleId
    INNER JOIN dbo.FinancialTransactions t ON t.TransactionId = v.TransactionId
    WHERE   CAST(v.DetectedDate AS DATE) BETWEEN @DateFrom AND @DateTo
    GROUP BY r.RuleCode, r.RuleName, v.Severity
    ORDER BY r.RuleCode, v.Severity;
END
GO

-- =============================================
-- usp_Report_AuditTrail
-- Data source for the Audit Trail Crystal Report / PDF export.
-- =============================================
IF OBJECT_ID('dbo.usp_Report_AuditTrail', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Report_AuditTrail;
GO
CREATE PROCEDURE dbo.usp_Report_AuditTrail
    @DateFrom DATETIME2(0),
    @DateTo   DATETIME2(0)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Username, ActionType, EntityName, EntityId, IPAddress, Timestamp
    FROM   dbo.AuditLogs
    WHERE  Timestamp BETWEEN @DateFrom AND @DateTo
    ORDER BY Timestamp DESC;
END
GO

-- =============================================
-- usp_Report_LogGenerated
-- =============================================
IF OBJECT_ID('dbo.usp_Report_LogGenerated', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Report_LogGenerated;
GO
CREATE PROCEDURE dbo.usp_Report_LogGenerated
    @ReportName       NVARCHAR(150),
    @ReportType        NVARCHAR(40),
    @ParametersJson      NVARCHAR(1000) = NULL,
    @GeneratedByUserId     INT,
    @OutputFormat             NVARCHAR(10),
    @FilePath                   NVARCHAR(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.GeneratedReports
        (ReportName, ReportType, ParametersJson, GeneratedByUserId, OutputFormat, FilePath)
    VALUES
        (@ReportName, @ReportType, @ParametersJson, @GeneratedByUserId, @OutputFormat, @FilePath);
END
GO

-- =============================================
-- usp_Dashboard_GetSummary
-- One round trip, multiple result sets, for the Home/Dashboard screen.
-- =============================================
IF OBJECT_ID('dbo.usp_Dashboard_GetSummary', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Dashboard_GetSummary;
GO
CREATE PROCEDURE dbo.usp_Dashboard_GetSummary
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: KPI tiles
    SELECT
        (SELECT COUNT(*) FROM dbo.FinancialTransactions WHERE TransactionDate >= DATEADD(DAY, -30, CAST(SYSUTCDATETIME() AS DATE))) AS TransactionsLast30Days,
        (SELECT COUNT(*) FROM dbo.ComplianceViolations WHERE Status IN ('Open','UnderReview')) AS OpenViolations,
        (SELECT COUNT(*) FROM dbo.ComplianceViolations WHERE Status IN ('Open','UnderReview') AND Severity = 'Critical') AS CriticalOpenViolations,
        (SELECT COUNT(*) FROM dbo.AuditEngagements WHERE Status IN ('Planned','InProgress')) AS ActiveAudits,
        (SELECT COUNT(*) FROM dbo.AuditFindings WHERE Status <> 'Closed') AS OpenFindings;

    -- Result set 2: violations by severity (for a chart)
    SELECT Severity, COUNT(*) AS Count
    FROM   dbo.ComplianceViolations
    WHERE  Status IN ('Open','UnderReview')
    GROUP BY Severity;

    -- Result set 3: recent activity
    EXEC dbo.usp_AuditLog_GetRecent @Top = 10;
END
GO

PRINT 'Audit log / exception log / reporting stored procedures created.';
GO
