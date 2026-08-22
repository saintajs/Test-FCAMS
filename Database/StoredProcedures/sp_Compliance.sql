/* ============================================================================
   Stored Procedures: ComplianceRules & ComplianceViolations
   ============================================================================ */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- usp_ComplianceRule_GetActive
-- Loaded once by ComplianceRuleEngine.cs at evaluation time.
-- =============================================
IF OBJECT_ID('dbo.usp_ComplianceRule_GetActive', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ComplianceRule_GetActive;
GO
CREATE PROCEDURE dbo.usp_ComplianceRule_GetActive
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RuleId, RuleCode, RuleName, Description, RuleType,
           ThresholdValue, ThresholdWindowDays, Severity
    FROM   dbo.ComplianceRules
    WHERE  IsActive = 1
    ORDER BY RuleCode;
END
GO

-- =============================================
-- usp_ComplianceRule_GetAll  (Admin rule-management screen)
-- =============================================
IF OBJECT_ID('dbo.usp_ComplianceRule_GetAll', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ComplianceRule_GetAll;
GO
CREATE PROCEDURE dbo.usp_ComplianceRule_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.ComplianceRules ORDER BY RuleCode;
END
GO

-- =============================================
-- usp_ComplianceRule_SetActive
-- Lets a Compliance Officer toggle a rule on/off without a code deploy.
-- =============================================
IF OBJECT_ID('dbo.usp_ComplianceRule_SetActive', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ComplianceRule_SetActive;
GO
CREATE PROCEDURE dbo.usp_ComplianceRule_SetActive
    @RuleId INT,
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ComplianceRules SET IsActive = @IsActive WHERE RuleId = @RuleId;
END
GO

-- =============================================
-- usp_ComplianceViolation_Create
-- Called by ComplianceRuleEngine.cs once per rule breach it detects.
-- =============================================
IF OBJECT_ID('dbo.usp_ComplianceViolation_Create', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ComplianceViolation_Create;
GO
CREATE PROCEDURE dbo.usp_ComplianceViolation_Create
    @TransactionId  BIGINT,
    @RuleId          INT,
    @Severity         NVARCHAR(20),
    @DetailMessage      NVARCHAR(1000),
    @NewViolationId       BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    INSERT INTO dbo.ComplianceViolations (TransactionId, RuleId, Severity, DetailMessage)
    VALUES (@TransactionId, @RuleId, @Severity, @DetailMessage);

    SET @NewViolationId = SCOPE_IDENTITY();

    -- A rule breach automatically flags the underlying transaction so it
    -- cannot silently proceed through an approval workflow untouched.
    UPDATE dbo.FinancialTransactions
    SET Status = 'Flagged', ModifiedDate = SYSUTCDATETIME()
    WHERE TransactionId = @TransactionId AND Status = 'Pending';
END
GO

-- =============================================
-- usp_ComplianceViolation_GetOpen
-- Uses the IX_Violations_Open filtered index.
-- =============================================
IF OBJECT_ID('dbo.usp_ComplianceViolation_GetOpen', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ComplianceViolation_GetOpen;
GO
CREATE PROCEDURE dbo.usp_ComplianceViolation_GetOpen
    @Severity NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  v.ViolationId, v.TransactionId, t.TransactionRef, t.Amount, t.AccountCode,
            r.RuleCode, r.RuleName, v.Severity, v.DetailMessage, v.DetectedDate, v.Status
    FROM    dbo.ComplianceViolations v
    INNER JOIN dbo.FinancialTransactions t ON t.TransactionId = v.TransactionId
    INNER JOIN dbo.ComplianceRules r ON r.RuleId = v.RuleId
    WHERE   v.Status IN ('Open','UnderReview')
      AND   (@Severity IS NULL OR v.Severity = @Severity)
    ORDER BY
        CASE v.Severity WHEN 'Critical' THEN 0 WHEN 'High' THEN 1 WHEN 'Medium' THEN 2 ELSE 3 END,
        v.DetectedDate DESC;
END
GO

-- =============================================
-- usp_ComplianceViolation_Resolve
-- =============================================
IF OBJECT_ID('dbo.usp_ComplianceViolation_Resolve', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ComplianceViolation_Resolve;
GO
CREATE PROCEDURE dbo.usp_ComplianceViolation_Resolve
    @ViolationId       BIGINT,
    @NewStatus          NVARCHAR(20),      -- 'Resolved' or 'FalsePositive'
    @ResolvedByUserId    INT,
    @ResolutionNotes       NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @NewStatus NOT IN ('Resolved','FalsePositive')
    BEGIN
        RAISERROR('NewStatus must be Resolved or FalsePositive.', 16, 1);
        RETURN;
    END

    UPDATE dbo.ComplianceViolations
    SET Status = @NewStatus,
        ResolvedByUserId = @ResolvedByUserId,
        ResolvedDate = SYSUTCDATETIME(),
        ResolutionNotes = @ResolutionNotes
    WHERE ViolationId = @ViolationId;
END
GO

PRINT 'Compliance stored procedures created.';
GO
