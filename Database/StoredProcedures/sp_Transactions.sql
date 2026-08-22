/* ============================================================================
   Stored Procedures: FinancialTransactions
   ============================================================================ */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- usp_Transaction_Create
-- Inserts a transaction and returns its new Id. The compliance rule engine
-- is deliberately NOT invoked from inside this procedure -- rule evaluation
-- is orchestrated from the application layer (ComplianceRuleEngine.cs) so
-- that: (a) business rule logic lives in one place and is unit-testable
-- outside of SQL, and (b) new rule types can be added without touching the
-- database. The proc's only job is a fast, validated insert.
-- =============================================
IF OBJECT_ID('dbo.usp_Transaction_Create', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Transaction_Create;
GO
CREATE PROCEDURE dbo.usp_Transaction_Create
    @TransactionRef     NVARCHAR(30),
    @TransactionDate    DATE,
    @Amount              DECIMAL(18,2),
    @CurrencyCode        CHAR(3),
    @AccountCode          NVARCHAR(20),
    @DepartmentId          INT,
    @CounterpartyName       NVARCHAR(200) = NULL,
    @Description             NVARCHAR(500) = NULL,
    @CreatedByUserId          INT,
    @NewTransactionId          BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Amount = 0
    BEGIN
        RAISERROR('Transaction amount cannot be zero.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentId = @DepartmentId AND IsActive = 1)
    BEGIN
        RAISERROR('Department %d is not a valid, active department.', 16, 1, @DepartmentId);
        RETURN;
    END

    INSERT INTO dbo.FinancialTransactions
        (TransactionRef, TransactionDate, Amount, CurrencyCode, AccountCode,
         DepartmentId, CounterpartyName, Description, CreatedByUserId, Status)
    VALUES
        (@TransactionRef, @TransactionDate, @Amount, @CurrencyCode, @AccountCode,
         @DepartmentId, @CounterpartyName, @Description, @CreatedByUserId, 'Pending');

    SET @NewTransactionId = SCOPE_IDENTITY();
END
GO

-- =============================================
-- usp_Transaction_UpdateStatus
-- =============================================
IF OBJECT_ID('dbo.usp_Transaction_UpdateStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Transaction_UpdateStatus;
GO
CREATE PROCEDURE dbo.usp_Transaction_UpdateStatus
    @TransactionId INT,
    @NewStatus     NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.FinancialTransactions
    SET Status = @NewStatus,
        ModifiedDate = SYSUTCDATETIME()
    WHERE TransactionId = @TransactionId;
END
GO

-- =============================================
-- usp_Transaction_GetPaged
-- Server-side paging + filtering for the main Transactions grid.
-- Uses OFFSET/FETCH (SQL Server 2012+) which, combined with the
-- IX_FinTxn_Department_Status_Date covering index, lets the engine seek
-- directly to the requested page instead of scanning the whole table.
-- Returns total row count alongside the page via a second result set so
-- the UI can render pager controls without a second round trip.
-- =============================================
IF OBJECT_ID('dbo.usp_Transaction_GetPaged', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Transaction_GetPaged;
GO
CREATE PROCEDURE dbo.usp_Transaction_GetPaged
    @DepartmentId   INT             = NULL,
    @Status          NVARCHAR(20)   = NULL,
    @DateFrom         DATE          = NULL,
    @DateTo            DATE         = NULL,
    @PageNumber         INT         = 1,
    @PageSize             INT       = 25
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize NOT BETWEEN 1 AND 200 SET @PageSize = 25;

    ;WITH Filtered AS
    (
        SELECT  t.TransactionId, t.TransactionRef, t.TransactionDate, t.Amount,
                t.CurrencyCode, t.AccountCode, t.Status, t.CounterpartyName,
                t.Description, d.DepartmentName, u.FullName AS CreatedByName,
                t.CreatedDate
        FROM    dbo.FinancialTransactions t
        INNER JOIN dbo.Departments d ON d.DepartmentId = t.DepartmentId
        INNER JOIN dbo.Users u ON u.UserId = t.CreatedByUserId
        WHERE   (@DepartmentId IS NULL OR t.DepartmentId = @DepartmentId)
          AND   (@Status IS NULL OR t.Status = @Status)
          AND   (@DateFrom IS NULL OR t.TransactionDate >= @DateFrom)
          AND   (@DateTo IS NULL OR t.TransactionDate <= @DateTo)
    )
    SELECT  *, COUNT(*) OVER() AS TotalRowCount
    FROM    Filtered
    ORDER BY TransactionDate DESC, TransactionId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- =============================================
-- usp_Transaction_GetById
-- =============================================
IF OBJECT_ID('dbo.usp_Transaction_GetById', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Transaction_GetById;
GO
CREATE PROCEDURE dbo.usp_Transaction_GetById
    @TransactionId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  t.*, d.DepartmentName, u.FullName AS CreatedByName
    FROM    dbo.FinancialTransactions t
    INNER JOIN dbo.Departments d ON d.DepartmentId = t.DepartmentId
    INNER JOIN dbo.Users u ON u.UserId = t.CreatedByUserId
    WHERE   t.TransactionId = @TransactionId;
END
GO

-- =============================================
-- usp_Transaction_FindDuplicateCandidates
-- Supports the DuplicateTransaction compliance rule: any other transaction
-- on the same account, for the same amount, posted within @WindowDays.
-- =============================================
IF OBJECT_ID('dbo.usp_Transaction_FindDuplicateCandidates', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Transaction_FindDuplicateCandidates;
GO
CREATE PROCEDURE dbo.usp_Transaction_FindDuplicateCandidates
    @TransactionId  BIGINT,
    @AccountCode     NVARCHAR(20),
    @Amount           DECIMAL(18,2),
    @TransactionDate   DATE,
    @WindowDays          INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TransactionId, TransactionRef, TransactionDate, Amount
    FROM   dbo.FinancialTransactions
    WHERE  AccountCode = @AccountCode
      AND  Amount = @Amount
      AND  TransactionId <> @TransactionId
      AND  TransactionDate BETWEEN DATEADD(DAY, -@WindowDays, @TransactionDate)
                                AND DATEADD(DAY, @WindowDays, @TransactionDate);
END
GO

-- =============================================
-- usp_Transaction_CountRecentByAccount
-- Supports the VelocityCheck compliance rule.
-- =============================================
IF OBJECT_ID('dbo.usp_Transaction_CountRecentByAccount', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Transaction_CountRecentByAccount;
GO
CREATE PROCEDURE dbo.usp_Transaction_CountRecentByAccount
    @AccountCode     NVARCHAR(20),
    @TransactionDate  DATE,
    @WindowDays        INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) AS RecentCount
    FROM   dbo.FinancialTransactions
    WHERE  AccountCode = @AccountCode
      AND  TransactionDate BETWEEN DATEADD(DAY, -@WindowDays, @TransactionDate) AND @TransactionDate;
END
GO

PRINT 'Transaction stored procedures created.';
GO
