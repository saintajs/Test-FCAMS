# FCAMS Architecture

## Layering

```
Views (Razor)
    |
Controllers (thin -- model binding, calling services/repositories, mapping to ViewModels)
    |
Services (business logic: ComplianceRuleEngine, AuthenticationService, AuditLogService,
          ExceptionLogService, ReportService)
    |
DAL/Repositories (one interface + implementation per entity; the only classes that know
                   stored procedure names)
    |
DAL/SqlDataAccess (the only class that opens a SqlConnection)
    |
SQL Server (stored procedures only -- no ad hoc SQL from the application, except one
             trivial reference-table lookup that still goes through a stored procedure
             for consistency)
```

Every repository is exposed as an interface (`IUserRepository`, `ITransactionRepository`, ...) and
every service that depends on one takes it through constructor injection, with a parameterless
constructor in each controller/service that new()s up the concrete implementation for the "just run
it" path. This is deliberately *not* a full DI container (Ninject/Autofac/Unity) -- for a project
this size, a container adds a layer of configuration to explain without adding much testability
benefit over plain constructor injection, but the seam is real: `FCAMS.Tests` mocks every repository
interface with Moq and never touches a database.

## Why ADO.NET + stored procedures instead of Entity Framework

1. **Explicit query plans.** Every screen's data access is a named, individually tunable stored
   procedure. "Why is the Transactions grid slow" has a concrete answer:
   `dbo.usp_Transaction_GetPaged`, and a concrete fix: look at its execution plan against
   `IX_FinTxn_Department_Status_Date`.
2. **No N+1 surprises.** LINQ-to-Entities makes it easy to accidentally lazy-load a collection inside
   a loop. Every multi-table read here is a single stored procedure with explicit `JOIN`s.
3. **A clean boundary for SQL injection safety.** `SqlDataAccess` only ever executes
   `CommandType.StoredProcedure` with `SqlParameter` objects -- there is no code path anywhere in the
   application that concatenates user input into a SQL string.
4. **It's honestly explainable.** In an interview, "I hand-wrote the data access layer and can show
   you exactly what SQL runs for any given screen" is a stronger answer than "Entity Framework
   generated it."

The trade-off, stated plainly: this is more boilerplate per entity (a mapper method per repository)
than EF would need, and schema changes require updating both the stored procedure and the C# mapper.
For a system of this size that trade-off is worth it for the transparency; for a much larger CRUD-heavy
system, EF Core with compiled queries would be a reasonable alternative.

## The compliance rule engine

`Services/ComplianceRuleEngine.cs` is the heart of the system. Design:

- `ComplianceRules` (table) holds *configuration*: which rule types are active, and their thresholds
  (`ThresholdValue`, `ThresholdWindowDays`), per row. An Admin/Compliance Officer can flip
  `IsActive` or change a threshold with zero code changes.
- `ComplianceRuleEngine.EvaluateSingleRule()` (code) holds *logic*: what it actually means to check
  "AmountThreshold" or "VelocityCheck" against a transaction. Six rule types are implemented today
  (`AmountThreshold`, `DuplicateTransaction`, `RoundDollar`, `WeekendPosting`, `MissingApproval`,
  `VelocityCheck`); adding a seventh means adding a `case` and a private method, plus tests --
  intentionally a code change, because "what counts as suspicious" deserves code review and a test,
  not a live database edit.
- `EvaluateTransaction()` is called synchronously from `TransactionsController.Create()` immediately
  after a transaction is inserted, so violations surface the moment a transaction is recorded, not on
  the next batch run.
- Every violation raised also auto-flags the parent transaction (`usp_ComplianceViolation_Create`
  sets `Status = 'Flagged'`), so a flagged transaction can never silently continue through an approval
  workflow without a human seeing it in Compliance &gt; Violations.

### The "missing approval" rule and the case for a batch sweep

`MissingApproval` checks "is this transaction high-value, still Pending, and older than N days" --
but a transaction is evaluated *once*, at creation time, when it's brand new and can't possibly be
"older than N days" yet. In a production deployment this rule is meant to be re-evaluated by a
periodic sweep (e.g., a SQL Server Agent job or an Azure WebJob/Function running
`SELECT TransactionId FROM FinancialTransactions WHERE Status = 'Pending'` on a schedule, then calling
`ComplianceRuleEngine.EvaluateTransaction()` again for each). That scheduler is intentionally *not*
included here -- it's an infrastructure concern orthogonal to the rule logic itself, which is what's
under test. The rule is still exercised and unit tested using its "already overdue at evaluation
time" edge case (see `ComplianceRuleEngineTests.MissingApproval_HighValuePendingPastWindow_RaisesViolation`).

## Authentication & session model

FCAMS uses ASP.NET Forms Authentication with its own `Users` table rather than ASP.NET Identity. The
login flow (`AccountController.Login`):

1. `AuthenticationService.Authenticate()` -- pure business logic, no `HttpContext` dependency, fully
   unit tested -- checks the account is active, not locked out, and that
   `PasswordHasher.VerifyPassword()` (constant-time comparison) matches.
2. On success, a `FormsAuthenticationTicket` is issued with a small JSON payload
   (`CurrentUserInfo`: UserId, Username, FullName, RoleName, DepartmentId) in its `UserData`.
3. `Infrastructure/CurrentUserActionFilter` runs on every subsequent request, decrypts that ticket,
   and populates `SessionUser.Current` -- so controllers/services know "who is this and what role"
   without a database round trip per request.
4. `Infrastructure/AuthorizeRoleAttribute` extends the standard `[Authorize]` to also check
   `SessionUser.Current.RoleName` against an allow-list, e.g. `[AuthorizeRole("Administrator")]`.

## Audit logging vs. audit engagements -- two different "audit" concepts

This is a common point of confusion worth stating explicitly:

- **`AuditLogs`** (technical trail): every Create/Update/Delete/Login/Logout/View/Export action the
  *application* performs, written by `AuditLogService`. This is what a security reviewer means by
  "audit log."
- **`AuditEngagements` / `AuditFindings`** (business process): the actual *work* of an internal
  auditor -- "we're reviewing Procurement's Q3 transactions" -- with findings raised, tracked, and
  closed. This is what a Big-4-style auditor means by "an audit."

Both matter, they're deliberately separate tables/controllers/views, and a finding can optionally
link back to a specific transaction and/or compliance violation (`AuditFindings.TransactionId`,
`AuditFindings.ViolationId`) so an auditor's work product references the underlying evidence.

## Exception handling

`Infrastructure/GlobalExceptionFilter` (an MVC `IExceptionFilter`, registered globally in
`FilterConfig`) catches any unhandled exception from an action, logs it via `ExceptionLogService` to
`dbo.ExceptionLogs`, and renders a generic error view (or a JSON error payload for AJAX requests) --
never a raw stack trace to the browser. `Global.asax.cs`'s `Application_Error` is a second,
last-chance handler for exceptions that occur *outside* the MVC pipeline entirely (e.g. in routing).
`Web.config`'s `<customErrors>` handles the IIS-level redirect for anything that somehow still
reaches it.

## Testing strategy

`FCAMS.Tests` is an MSTest project with 25 unit tests, no database required:

- `ComplianceRuleEngineTests` (18 tests): every rule type's triggering and non-triggering case, plus
  cross-cutting behavior (multiple active rules evaluated independently, unknown rule type doesn't
  throw, null transaction throws `ArgumentNullException`, no active rules returns an empty list).
- `AuthenticationServiceTests` (7 tests): success, wrong password, unknown user, inactive account,
  locked-out account, expired lockout, empty username short-circuit.
- `PasswordHasherTests` (5 tests): round-trip verification, wrong password rejection, salt uniqueness,
  determinism.
- `PagedResultTests` (4 tests): the `TotalPages` calculation, including the zero-page-size guard.

All repository dependencies are mocked with Moq against the repository *interfaces*
(`IComplianceRepository`, `ITransactionRepository`, `IUserRepository`), which is what makes testing
the rule engine and the authentication flow possible without a live SQL Server instance -- a direct
payoff of the constructor-injection seam described above.
