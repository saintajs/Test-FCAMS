/* ============================================================================
   Stored Procedures: AuditEngagements & AuditFindings
   ============================================================================ */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.usp_AuditEngagement_Create', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_AuditEngagement_Create;
GO
CREATE PROCEDURE dbo.usp_AuditEngagement_Create
    @AuditName          NVARCHAR(200),
    @DepartmentId        INT,
    @LeadAuditorUserId    INT,
    @PeriodStart            DATE,
    @PeriodEnd               DATE,
    @Scope                     NVARCHAR(1000) = NULL,
    @NewAuditId                  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @PeriodEnd < @PeriodStart
    BEGIN
        RAISERROR('PeriodEnd cannot be before PeriodStart.', 16, 1);
        RETURN;
    END

    INSERT INTO dbo.AuditEngagements (AuditName, DepartmentId, LeadAuditorUserId, PeriodStart, PeriodEnd, Scope, Status)
    VALUES (@AuditName, @DepartmentId, @LeadAuditorUserId, @PeriodStart, @PeriodEnd, @Scope, 'Planned');

    SET @NewAuditId = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('dbo.usp_AuditEngagement_GetAll', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_AuditEngagement_GetAll;
GO
CREATE PROCEDURE dbo.usp_AuditEngagement_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  a.AuditId, a.AuditName, d.DepartmentName, u.FullName AS LeadAuditorName,
            a.PeriodStart, a.PeriodEnd, a.Status, a.CreatedDate,
            (SELECT COUNT(*) FROM dbo.AuditFindings f WHERE f.AuditId = a.AuditId AND f.Status <> 'Closed') AS OpenFindingCount
    FROM    dbo.AuditEngagements a
    INNER JOIN dbo.Departments d ON d.DepartmentId = a.DepartmentId
    INNER JOIN dbo.Users u ON u.UserId = a.LeadAuditorUserId
    ORDER BY a.CreatedDate DESC;
END
GO

IF OBJECT_ID('dbo.usp_AuditEngagement_GetById', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_AuditEngagement_GetById;
GO
CREATE PROCEDURE dbo.usp_AuditEngagement_GetById
    @AuditId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  a.*, d.DepartmentName, u.FullName AS LeadAuditorName
    FROM    dbo.AuditEngagements a
    INNER JOIN dbo.Departments d ON d.DepartmentId = a.DepartmentId
    INNER JOIN dbo.Users u ON u.UserId = a.LeadAuditorUserId
    WHERE   a.AuditId = @AuditId;

    SELECT  f.FindingId, f.FindingTitle, f.FindingDescription, f.Severity, f.Status,
            f.RaisedDate, f.DueDate, f.ClosedDate, ru.FullName AS RaisedByName,
            f.TransactionId, f.ViolationId
    FROM    dbo.AuditFindings f
    INNER JOIN dbo.Users ru ON ru.UserId = f.RaisedByUserId
    WHERE   f.AuditId = @AuditId
    ORDER BY CASE f.Severity WHEN 'Critical' THEN 0 WHEN 'High' THEN 1 WHEN 'Medium' THEN 2 ELSE 3 END, f.RaisedDate DESC;
END
GO

IF OBJECT_ID('dbo.usp_AuditEngagement_UpdateStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_AuditEngagement_UpdateStatus;
GO
CREATE PROCEDURE dbo.usp_AuditEngagement_UpdateStatus
    @AuditId INT,
    @NewStatus NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AuditEngagements
    SET Status = @NewStatus,
        CompletedDate = CASE WHEN @NewStatus IN ('Completed','Closed') THEN SYSUTCDATETIME() ELSE CompletedDate END
    WHERE AuditId = @AuditId;
END
GO

IF OBJECT_ID('dbo.usp_AuditFinding_Create', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_AuditFinding_Create;
GO
CREATE PROCEDURE dbo.usp_AuditFinding_Create
    @AuditId              INT,
    @TransactionId          BIGINT = NULL,
    @ViolationId              BIGINT = NULL,
    @FindingTitle               NVARCHAR(200),
    @FindingDescription           NVARCHAR(2000),
    @Severity                       NVARCHAR(20),
    @RaisedByUserId                   INT,
    @DueDate                           DATE = NULL,
    @NewFindingId                        BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    INSERT INTO dbo.AuditFindings
        (AuditId, TransactionId, ViolationId, FindingTitle, FindingDescription, Severity, RaisedByUserId, DueDate)
    VALUES
        (@AuditId, @TransactionId, @ViolationId, @FindingTitle, @FindingDescription, @Severity, @RaisedByUserId, @DueDate);

    SET @NewFindingId = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('dbo.usp_AuditFinding_UpdateStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_AuditFinding_UpdateStatus;
GO
CREATE PROCEDURE dbo.usp_AuditFinding_UpdateStatus
    @FindingId INT,
    @NewStatus NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AuditFindings
    SET Status = @NewStatus,
        ClosedDate = CASE WHEN @NewStatus = 'Closed' THEN SYSUTCDATETIME() ELSE ClosedDate END
    WHERE FindingId = @FindingId;
END
GO

PRINT 'Audit stored procedures created.';
GO
