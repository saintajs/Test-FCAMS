# FCAMS Security Notes

Honest documentation of what this project does, why, and where a production system would need to go
further. Written to be defensible in a portfolio review / interview setting.

## Password storage

**What's implemented:** salted SHA-256 (`Services/PasswordHasher.cs`) -- a random 32-byte salt per
user (a CSPRNG via `RandomNumberGenerator`, not `System.Random`), combined with the password and
hashed once, compared with a constant-time loop to avoid timing side-channels.

**Why not bcrypt/PBKDF2/Argon2:** those are the correct choice for production because they are
deliberately slow and/or memory-hard, which makes brute-forcing a stolen hash database
computationally expensive even with GPU/ASIC acceleration. A single SHA-256 pass is fast -- great for
throughput, bad for resisting offline brute force. This project uses SHA-256+salt specifically so the
mechanism needs no extra dependency beyond `System.Security.Cryptography` (already in .NET Framework)
and is fully inspectable in a few dozen lines of code for a portfolio review. **A real deployment
should switch to `Rfc2898DeriveBytes` (PBKDF2, built into .NET Framework, zero new dependencies) at
minimum, or BCrypt.Net-Next / Argon2.**

## SQL injection

Every single database call in the application goes through `DAL/SqlDataAccess.cs`, which only ever
executes `CommandType.StoredProcedure` with parameters passed as typed `SqlParameter` objects. There
is no string concatenation of user input into SQL anywhere in the codebase -- this is a structural
guarantee (there's no `ExecuteRawSql(string sql)` method to misuse), not a coding convention that
could be forgotten on one screen.

## Cross-site request forgery (CSRF)

Every state-changing `[HttpPost]` action is decorated with `[ValidateAntiForgeryToken]`, and every
form that posts to one includes `@Html.AntiForgeryToken()`. Read-only `[HttpGet]` actions never mutate
state, so they're intentionally not CSRF-protected (that's what the token is *for*).

## Cross-site scripting (XSS)

Razor HTML-encodes all output by default (`@Model.Whatever`) unless explicitly opted out with
`Html.Raw()`, which this codebase never uses on user-supplied content. `Web.config`'s
`validateRequest="true"` (the ASP.NET default) additionally rejects obviously malicious request input
before it reaches an action at all.

## Authentication & session

- Forms Authentication cookie is issued `HttpOnly` (not readable from JavaScript) and `Secure` when
  the request arrived over HTTPS (`AccountController.SignIn`).
- Session/ticket timeout is 30 minutes, configurable via `Web.config` `<forms timeout="30">` and the
  `Fcams.SessionTimeoutMinutes` app setting.
- Account lockout: 5 consecutive failed attempts locks the account for 15 minutes
  (`usp_User_RecordLoginFailure`), enforced in the database so it survives an app pool recycle and
  can't be bypassed by hitting a second web server in a load-balanced deployment.
- The login failure response is identical whether the username doesn't exist or the password is
  wrong (`AuthenticationService.Authenticate`), so the endpoint can't be used to enumerate valid
  usernames.

## Authorization

Role checks happen server-side on every protected action via `[AuthorizeRole(...)]`
(`Infrastructure/AuthorizeRoleAttribute.cs`), never trusted from hidden form fields or client-side
UI hiding alone. The nav bar hides the Admin menu for non-administrators as a UX convenience, but the
`AdminController` actions themselves are independently protected -- hiding a link is not a security
control in this codebase.

## Sensitive data in logs

`AuditLogService` serializes small, explicitly-constructed anonymous objects (e.g.
`new { transaction.TransactionRef, transaction.Amount }`), never a full entity dump -- so a
`PasswordHash`/`PasswordSalt` byte array can never accidentally end up in `dbo.AuditLogs.NewValues`
just because someone changed a repository's `SELECT *`.

## Things intentionally out of scope for this portfolio build

- **HTTPS enforcement / HSTS** -- left to the hosting environment (Azure App Service's "HTTPS Only"
  toggle, covered in `Docs/AZURE_DEPLOYMENT.md`), rather than hardcoded, so the app still runs over
  plain HTTP for local development without extra configuration.
- **Multi-factor authentication.**
- **Password complexity/rotation policy enforcement** -- the seed data uses reasonably strong demo
  passwords, but there's no self-service password change screen yet.
- **Rate limiting / IP-based throttling** beyond the per-account lockout described above.
- **Secrets management** -- the local connection string is in `Web.config` as plain text, which is
  fine for a student project and standard local-dev practice, but `Docs/AZURE_DEPLOYMENT.md` covers
  moving it to an Azure App Service connection-string application setting (encrypted at rest,
  injected at runtime) for the deployed environment, and notes Azure Key Vault as the next step up.
