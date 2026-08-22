/* ============================================================================
   Script: 02_Indexes.sql
   Purpose: Query-optimization indexes for FCAMS.
   Design notes (explainable in a portfolio/interview context):

   1. Every FK column that is regularly filtered/joined gets a nonclustered
      index -- SQL Server does NOT auto-index foreign keys the way primary
      keys are auto-indexed by the PK constraint.
   2. The Transactions grid (paged list, filterable by department/date/status)
      is the highest-traffic query in the app, so it gets a *covering* index:
      the key columns match the WHERE/ORDER BY clause and frequently
      requested columns are added via INCLUDE so the query can be satisfied
      entirely from the index (no key lookup back to the clustered index).
   3. Open violations and open findings are queried far more often than
      resolved/closed ones (dashboards, worklists) -- filtered indexes keep
      those indexes small and fast instead of indexing the whole table.
   4. AuditLogs and ExceptionLogs are insert-heavy, read-occasionally tables.
      They get a single supporting index on (Timestamp DESC) since almost
      every read is "show me the most recent N", plus one for entity lookups.
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
GO
/* ---- Users ---------------------------------------------------------------*/
CREATE NONCLUSTERED INDEX IX_Users_RoleId
    ON dbo.Users (RoleId) INCLUDE (Username, FullName, IsActive);
GO
CREATE NONCLUSTERED INDEX IX_Users_DepartmentId
    ON dbo.Users (DepartmentId) WHERE DepartmentId IS NOT NULL;
GO

/* ---- FinancialTransactions -------------------------------------------- */
-- Covering index for the primary "browse transactions" screen:
-- filter by Department + Status, sorted by TransactionDate DESC.
CREATE NONCLUSTERED INDEX IX_FinTxn_Department_Status_Date
    ON dbo.FinancialTransactions (DepartmentId, Status, TransactionDate DESC)
    INCLUDE (Amount, AccountCode, TransactionRef, CreatedByUserId);
GO

-- Supports duplicate-detection and velocity-check rules in the compliance
-- engine, which look for other transactions with the same account/amount
-- within a rolling window.
CREATE NONCLUSTERED INDEX IX_FinTxn_Account_Amount_Date
    ON dbo.FinancialTransactions (AccountCode, Amount, TransactionDate);
GO

CREATE NONCLUSTERED INDEX IX_FinTxn_CreatedByUserId
    ON dbo.FinancialTransactions (CreatedByUserId);
GO

/* ---- ComplianceViolations -----------------------------------------------*/
-- Filtered index: only "Open"/"UnderReview" rows, which is what every
-- dashboard widget and the reviewer worklist query. Keeps this index a
-- fraction of the size of a full index once the table accumulates history.
CREATE NONCLUSTERED INDEX IX_Violations_Open
    ON dbo.ComplianceViolations (DetectedDate DESC, Severity)
    INCLUDE (TransactionId, RuleId, DetailMessage)
    WHERE Status IN ('Open','UnderReview');
GO

CREATE NONCLUSTERED INDEX IX_Violations_TransactionId
    ON dbo.ComplianceViolations (TransactionId);
GO

CREATE NONCLUSTERED INDEX IX_Violations_RuleId
    ON dbo.ComplianceViolations (RuleId);
GO

/* ---- AuditEngagements / AuditFindings ------------------------------------*/
CREATE NONCLUSTERED INDEX IX_Audit_Department_Status
    ON dbo.AuditEngagements (DepartmentId, Status);
GO

CREATE NONCLUSTERED INDEX IX_Findings_AuditId_Status
    ON dbo.AuditFindings (AuditId, Status)
    INCLUDE (Severity, DueDate);
GO

/* ---- AuditLogs (technical trail) -----------------------------------------*/
-- Almost every read is "most recent activity", or "history for this entity".
CREATE NONCLUSTERED INDEX IX_AuditLogs_Timestamp
    ON dbo.AuditLogs (Timestamp DESC);
GO

CREATE NONCLUSTERED INDEX IX_AuditLogs_Entity
    ON dbo.AuditLogs (EntityName, EntityId, Timestamp DESC);
GO

CREATE NONCLUSTERED INDEX IX_AuditLogs_UserId
    ON dbo.AuditLogs (UserId, Timestamp DESC) WHERE UserId IS NOT NULL;
GO

/* ---- ExceptionLogs ---------------------------------------------------- */
CREATE NONCLUSTERED INDEX IX_ExceptionLogs_Timestamp_Severity
    ON dbo.ExceptionLogs (Timestamp DESC, Severity)
    WHERE IsResolved = 0;
GO

PRINT 'FCAMS indexes created successfully.';
GO
