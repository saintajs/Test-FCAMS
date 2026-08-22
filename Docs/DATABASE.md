# FCAMS Database Design

## Entity-relationship overview

```
Roles ----------------< Users >------------------ Departments
                          |                             |
                          | CreatedByUserId              | DepartmentId
                          v                             v
                  FinancialTransactions <---------------+
                          |
                          | TransactionId
                          v
                  ComplianceViolations >------------ ComplianceRules
                          |
                          | ViolationId (optional)
                          v
                  AuditFindings <----------------- AuditEngagements
                          ^                               |
                          | TransactionId (optional)       | LeadAuditorUserId, DepartmentId
                          +-------------------------------+

  AuditLogs        (independent -- references UserId loosely via a denormalized Username snapshot)
  ExceptionLogs     (independent -- references UserId loosely)
  GeneratedReports  (references GeneratedByUserId)
```

## Table-by-table notes

### `Roles`, `Departments`
Small static/slow-changing reference tables. Four roles ship seeded: `Administrator`,
`ComplianceOfficer`, `Auditor`, `Viewer` (the Viewer role exists in the schema and rule set but has no
dedicated screens in this version -- a natural next feature).

### `Users`
`PasswordHash`/`PasswordSalt` are `VARBINARY`, never `NVARCHAR` -- there is no code path that ever
treats a password hash as text. `FailedLoginCount` / `LockedOutUntil` implement account lockout
entirely in the database via `usp_User_RecordLoginFailure`, so the lockout state survives an app pool
recycle.

### `FinancialTransactions`
The transactional core. `Status` is constrained (`CK_FinTxn_Status`) to
`Pending | Approved | Rejected | Flagged | Reversed`. `RowVersion` (a `ROWVERSION`/`timestamp` column)
is present for future optimistic-concurrency use (e.g., two compliance officers editing the same
transaction) even though the current UI doesn't yet expose an edit screen that needs it.

### `ComplianceRules`
The rule engine's configuration table -- see `Docs/ARCHITECTURE.md` for the config/logic split.
`RuleType` is constrained to the six types `ComplianceRuleEngine.cs` knows how to evaluate; adding a
seventh requires both a C# code change *and* a `CK_ComplianceRules_RuleType` constraint update, which
is intentional friction (see architecture doc).

### `ComplianceViolations`
One row per rule breach per transaction (a transaction can accumulate multiple violations from
multiple rules, e.g., both `AmountThreshold` and `RoundDollar` on the same transaction).
`Status` moves `Open -> UnderReview -> Resolved` or `Open -> FalsePositive`, both terminal states
requiring `ResolvedByUserId`/`ResolutionNotes` (enforced at the application layer in
`usp_ComplianceViolation_Resolve`, not by a database constraint, since "notes required" is a softer
business rule than a hard data-integrity rule).

### `AuditEngagements`, `AuditFindings`
The audit *process* tables -- see the architecture doc's "two different audit concepts" section.
`AuditFindings.TransactionId` and `.ViolationId` are both nullable FKs: a finding can stand alone
(e.g., a process/control-design finding with no single transaction behind it), reference one
transaction, or point directly at a violation the rule engine already raised.

### `AuditLogs`
Deliberately denormalized: `Username` is stored as a snapshot string alongside the `UserId` FK-less
integer column, so the audit trail for a user who is later deactivated or deleted still reads
correctly (an audit log that says "(unknown user #47)" six months after the fact is a bug, not a
feature). `OldValues`/`NewValues` are `NVARCHAR(MAX)` JSON blobs produced by `AuditLogService` via
`Newtonsoft.Json.JsonConvert.SerializeObject` on small anonymous objects -- deliberately *not* a full
entity dump, to keep the trail readable and avoid ever accidentally logging a password hash.

### `ExceptionLogs`
`Severity` is classified in code (`ExceptionLogService.ClassifySeverity`) -- `SqlException` and
`OutOfMemoryException` are `Critical`, auth-related exceptions are `Warning`, everything else is
`Error`. `IsResolved` supports an "acknowledge and dismiss" workflow an Admin could add later (the
current `Admin/ExceptionLog` view only reads it as a filter).

## Indexing strategy (see `Database/Indexes/02_Indexes.sql` for full rationale comments)

| Index | Purpose |
|---|---|
| `IX_FinTxn_Department_Status_Date` | Covering index for the primary Transactions grid query (`usp_Transaction_GetPaged`): filters by department + status, sorts by date, includes every column the grid displays so no key lookup is needed. |
| `IX_FinTxn_Account_Amount_Date` | Backs the `DuplicateTransaction` and `VelocityCheck` rule queries, which look up other transactions by account+amount or account+date range. |
| `IX_Violations_Open` (filtered) | Only indexes `Open`/`UnderReview` rows -- the dashboard and worklist queries never care about historical resolved violations, so this index stays a fraction of the table's full size indefinitely. |
| `IX_AuditLogs_Timestamp`, `IX_AuditLogs_Entity` | Almost every audit-log read is either "most recent N" or "history for this specific record" -- both get a dedicated, sorted index rather than relying on a table scan + sort. |
| `IX_ExceptionLogs_Timestamp_Severity` (filtered) | Only indexes unresolved exceptions, since that's what the Admin screen shows by default. |

## Query optimization techniques used

1. **Server-side paging with `OFFSET/FETCH`** (`usp_Transaction_GetPaged`, `usp_AuditLog_GetPaged`) --
   the database only computes and returns the requested page, and `COUNT(*) OVER()` in the same query
   returns the total row count for pager UI without a second round trip.
2. **Covering indexes with `INCLUDE`** -- see `IX_FinTxn_Department_Status_Date` above; the goal is
   for the query plan to satisfy the grid entirely from the nonclustered index (an index seek) rather
   than seeking the index then bookmark-looking-up the clustered index per row.
3. **Filtered indexes** on `Status IN ('Open','UnderReview')` and `IsResolved = 0` -- indexing only
   the "hot" subset of rows that dashboards/worklists actually query keeps those indexes small and
   fast even as the historical/resolved row count grows into the millions.
4. **Pre-aggregation in T-SQL for reports** (`usp_Report_ComplianceSummary`) -- `GROUP BY`/`SUM` happen
   in the database, so report generation scales with the database engine, not with memory in the IIS
   worker process pulling every row across the wire to group client-side.
5. **`SET NOCOUNT ON`** in every stored procedure -- suppresses the "N rows affected" message SQL
   Server sends after each statement, which is pure network chatter ADO.NET otherwise has to parse
   and discard.
6. **Multiple result sets in one round trip** (`usp_Dashboard_GetSummary`,
   `usp_AuditEngagement_GetById`) -- the dashboard needs KPI tiles + a severity breakdown + recent
   activity; rather than three separate connections/round trips, one stored procedure returns three
   result sets and `SqlDataAccess.ExecuteMultiResultReader` reads them in order with `reader.NextResult()`.

## Running the scripts

Scripts are numbered and must run in order (tables before indexes before seed data; seed data before
any stored procedure that reads `Roles`/`Departments` at execution time -- though none currently do at
creation time, since procedures don't execute their body until called). See the main `README.md`
"Getting started" section for the exact command sequence.
