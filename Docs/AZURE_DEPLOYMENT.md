# Deploying FCAMS to Azure

This deploys FCAMS.Web to an Azure App Service (Windows, .NET Framework 4.8) backed by Azure SQL
Database. Two paths are given: the Azure Portal (click-through, good for understanding each step) and
the Azure CLI (good for repeatability/scripting). Either gets you to the same place.

## Prerequisites
- An Azure subscription
- Azure CLI installed locally (`az --version` to confirm), or just the Azure Portal in a browser
- The FCAMS solution building successfully locally first (see main `README.md`)

## 1. Create the Azure SQL Database

### Portal
1. **Create a resource &rarr; SQL Database.**
2. Create a new SQL Server (logical server) if you don't have one: choose a unique server name, an
   admin login/password, and a region.
3. Database name: `FCAMS_DB`. Pricing tier: **Basic** or **Standard S0** is plenty for a portfolio
   demo (5 DTU / 10 DTU).
4. Under **Networking**, enable **"Allow Azure services and resources to access this server"** so the
   App Service can reach it, and add your local IP under firewall rules so you can run the setup
   scripts from SSMS.

### CLI
```bash
az group create --name fcams-rg --location eastus

az sql server create \
  --name fcams-sqlserver-<unique-suffix> \
  --resource-group fcams-rg \
  --location eastus \
  --admin-user fcamsadmin \
  --admin-password "<a-strong-password>"

az sql db create \
  --resource-group fcams-rg \
  --server fcams-sqlserver-<unique-suffix> \
  --name FCAMS_DB \
  --service-objective S0

az sql server firewall-rule create \
  --resource-group fcams-rg \
  --server fcams-sqlserver-<unique-suffix> \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0

az sql server firewall-rule create \
  --resource-group fcams-rg \
  --server fcams-sqlserver-<unique-suffix> \
  --name AllowMyIp \
  --start-ip-address <your-ip> --end-ip-address <your-ip>
```

## 2. Run the database scripts against Azure SQL

Connect with SSMS or Azure Data Studio to
`fcams-sqlserver-<unique-suffix>.database.windows.net`, database `FCAMS_DB`, using the admin
login/password from step 1. Run, in order:

```
Database/Tables/01_CreateTables.sql
Database/Indexes/02_Indexes.sql
Database/Seed/03_SeedData.sql
Database/StoredProcedures/sp_Users.sql
Database/StoredProcedures/sp_Transactions.sql
Database/StoredProcedures/sp_Compliance.sql
Database/StoredProcedures/sp_Audit.sql
Database/StoredProcedures/sp_Logging_Reports.sql
```

Azure SQL Database is wire-compatible with these scripts as written -- no changes needed (they avoid
any on-premises-only T-SQL features).

> **Change the seeded demo passwords** (`admin`/`Admin@123`, etc. from `03_SeedData.sql`) before
> treating any deployment as more than a local demo -- they are intentionally simple, documented,
> publicly-known credentials meant for local development only.

## 3. Create the App Service

### Portal
1. **Create a resource &rarr; Web App.**
2. Publish: **Code**. Runtime stack: **.NET Framework 4.8** (this is why the App Service must be
   **Windows**, not Linux -- .NET Framework, unlike .NET Core/5+, is Windows-only).
3. Choose the same resource group and region as the SQL Database above.
4. App Service Plan: **B1 (Basic)** is sufficient for a portfolio demo.

### CLI
```bash
az appservice plan create \
  --name fcams-plan \
  --resource-group fcams-rg \
  --sku B1 \
  --is-linux false

az webapp create \
  --name fcams-web-<unique-suffix> \
  --resource-group fcams-rg \
  --plan fcams-plan
```

## 4. Configure the connection string as an App Service setting

This is the key step that lets you keep `Web.config`'s local connection string pointed at
`.\SQLEXPRESS` for local dev while the deployed app transparently uses Azure SQL instead -- App
Service connection-string settings **override** the matching `<connectionStrings>` entry name in
`Web.config` at runtime; no code change or `Web.config` transform needed.

### Portal
**App Service &rarr; Configuration &rarr; Connection strings &rarr; New connection string:**
- Name: `FcamsDbContext` (must match `Web.config` exactly)
- Value:
  `Server=tcp:fcams-sqlserver-<unique-suffix>.database.windows.net,1433;Initial Catalog=FCAMS_DB;Persist Security Info=False;User ID=fcamsadmin;Password=<your-password>;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`
- Type: **SQLAzure**

### CLI
```bash
az webapp config connection-string set \
  --name fcams-web-<unique-suffix> \
  --resource-group fcams-rg \
  --connection-string-type SQLAzure \
  --settings FcamsDbContext="Server=tcp:fcams-sqlserver-<unique-suffix>.database.windows.net,1433;Initial Catalog=FCAMS_DB;Persist Security Info=False;User ID=fcamsadmin;Password=<your-password>;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

## 5. Turn on HTTPS Only and set the minimum TLS version

**App Service &rarr; TLS/SSL settings:** toggle **HTTPS Only** to On, set **Minimum TLS Version** to
1.2. (CLI: `az webapp update --name fcams-web-<unique-suffix> --resource-group fcams-rg --https-only true`.)

## 6. Publish the app

### From Visual Studio (simplest)
Right-click `FCAMS.Web` &rarr; **Publish** &rarr; **Azure** &rarr; **Azure App Service (Windows)** &rarr;
select the `fcams-web-<unique-suffix>` app created above &rarr; **Publish**. Visual Studio builds in
Release configuration and deploys via Web Deploy.

### From the CLI (e.g. for a CI/CD pipeline)
```bash
# Build a deployable package
msbuild FCAMS.Web\FCAMS.Web.csproj /p:Configuration=Release /p:DeployOnBuild=true /p:WebPublishMethod=Package /p:PackageAsSingleFile=true /p:PackageLocation=./publish

# Deploy the resulting zip
az webapp deployment source config-zip \
  --resource-group fcams-rg \
  --name fcams-web-<unique-suffix> \
  --src ./publish/FCAMS.Web.zip
```

## 7. Verify

Browse to `https://fcams-web-<unique-suffix>.azurewebsites.net`. You should land on the FCAMS login
page. Sign in with the (changed!) seeded admin account and confirm the dashboard loads real data from
Azure SQL.

## 8. App Service Application Settings worth reviewing

Under **Configuration &rarr; Application settings**, in addition to the connection string above:

| Setting | Recommended value | Why |
|---|---|---|
| `Fcams.ReportOutputPath` | `~/App_Data/GeneratedReports` (default) or a path pointing at an Azure Files share mounted to the App Service | The default writes generated PDF reports to local App Service storage, which is **not persistent** across scale-out/restarts on the default plan. For anything beyond a demo, mount Azure Files (`az webapp config storage-account add`) and point this setting there. |
| `WEBSITE_TIME_ZONE` | `Eastern Standard Time` (or your preference) | All application timestamps are stored as UTC (`SYSUTCDATETIME()`); this setting only affects how the App Service's own clock/logs display, not stored data. |

## Common deployment issues

- **"Could not load file or assembly 'System.Web.Mvc'"** &mdash; NuGet packages weren't restored
  before publishing. In Visual Studio: right-click the solution &rarr; **Restore NuGet Packages**.
- **Login page loads but signing in fails with a generic error** &mdash; almost always the connection
  string. Check **App Service &rarr; Configuration &rarr; Connection strings** matches the name
  `FcamsDbContext` exactly (case-sensitive) and that the SQL Server firewall allows Azure services.
- **500 error with no detail** &mdash; check **App Service &rarr; Diagnose and solve problems &rarr;
  Application Logs**, or query `dbo.ExceptionLogs` directly against the Azure SQL Database -- every
  unhandled exception in FCAMS is logged there regardless of environment (see
  `Infrastructure/GlobalExceptionFilter.cs`).
- **Crystal Reports features fail** &mdash; expected if you haven't followed
  `Docs/CRYSTAL_REPORTS_SETUP.md`'s note on Azure App Service's sandboxed hosting model not including
  the Crystal Reports runtime; the PDF reporting path (`ReportsController`'s default actions) works
  regardless.
