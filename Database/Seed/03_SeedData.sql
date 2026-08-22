/* ============================================================================
   Script: 03_SeedData.sql
   Purpose: Reference data + one working admin login + sample compliance
            rules so the app is usable immediately after deployment.

   IMPORTANT (password handling):
   The admin password below is 'Admin@123'. It is NOT stored in plain text --
   this script computes SHA-256(password + salt) using SQL Server's native
   HASHBYTES function so the seeded row is created exactly the same way
   FCAMS.Web's PasswordHasher.cs creates one at signup. See Docs/SECURITY.md
   for why SHA-256+salt was chosen for a student project vs. bcrypt/PBKDF2
   in production.
   ============================================================================ */

SET NOCOUNT ON;
GO

/* ---- Roles ------------------------------------------------------------ */
INSERT INTO dbo.Roles (RoleName, Description) VALUES
    ('Administrator', 'Full system access: user management, rule configuration, all data'),
    ('ComplianceOfficer', 'Reviews and resolves compliance violations, configures rules'),
    ('Auditor', 'Creates audit engagements, raises and closes audit findings'),
    ('Viewer', 'Read-only access to transactions, violations and reports');
GO

/* ---- Departments -------------------------------------------------------*/
INSERT INTO dbo.Departments (DepartmentName, CostCenterCode) VALUES
    ('Finance & Treasury', 'CC-100'),
    ('Procurement', 'CC-200'),
    ('Human Resources', 'CC-300'),
    ('Information Technology', 'CC-400'),
    ('Sales & Marketing', 'CC-500');
GO

/* ---- Admin user ---------------------------------------------------------
   Salt: fixed 16-byte demo salt (0x01..0x10). A real deployment generates a
   unique random salt per user at signup time -- see PasswordHasher.cs.
--------------------------------------------------------------------------- */
DECLARE @Salt VARBINARY(32) = 0x000102030405060708090A0B0C0D0E0F;
DECLARE @Password NVARCHAR(50) = 'Admin@123';
DECLARE @Hash VARBINARY(64) = HASHBYTES('SHA2_256', CAST(@Password AS VARBINARY(50)) + @Salt);

INSERT INTO dbo.Users (Username, Email, FullName, PasswordHash, PasswordSalt, RoleId, DepartmentId, IsActive)
VALUES ('admin', 'admin@fcams.local', 'System Administrator', @Hash, @Salt,
        (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Administrator'),
        (SELECT DepartmentId FROM dbo.Departments WHERE CostCenterCode = 'CC-100'), 1);

-- Compliance officer demo account: username 'compliance', password 'Comply@123'
DECLARE @Salt2 VARBINARY(32) = 0x101112131415161718191A1B1C1D1E1F;
DECLARE @Hash2 VARBINARY(64) = HASHBYTES('SHA2_256', CAST(N'Comply@123' AS VARBINARY(50)) + @Salt2);
INSERT INTO dbo.Users (Username, Email, FullName, PasswordHash, PasswordSalt, RoleId, DepartmentId, IsActive)
VALUES ('compliance', 'compliance@fcams.local', 'Casey Compliance', @Hash2, @Salt2,
        (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'ComplianceOfficer'),
        (SELECT DepartmentId FROM dbo.Departments WHERE CostCenterCode = 'CC-100'), 1);

-- Auditor demo account: username 'auditor', password 'Audit@123'
DECLARE @Salt3 VARBINARY(32) = 0x202122232425262728292A2B2C2D2E2F;
DECLARE @Hash3 VARBINARY(64) = HASHBYTES('SHA2_256', CAST(N'Audit@123' AS VARBINARY(50)) + @Salt3);
INSERT INTO dbo.Users (Username, Email, FullName, PasswordHash, PasswordSalt, RoleId, DepartmentId, IsActive)
VALUES ('auditor', 'auditor@fcams.local', 'Alex Auditor', @Hash3, @Salt3,
        (SELECT RoleId FROM dbo.Roles WHERE RoleName = 'Auditor'),
        (SELECT DepartmentId FROM dbo.Departments WHERE CostCenterCode = 'CC-100'), 1);
GO

/* ---- Compliance Rules (data-driven; edit via Admin > Rules screen) ------*/
INSERT INTO dbo.ComplianceRules (RuleCode, RuleName, Description, RuleType, ThresholdValue, ThresholdWindowDays, Severity) VALUES
('RULE-001', 'Large Transaction Threshold',
 'Flags any single transaction whose absolute amount exceeds the configured threshold, requiring secondary approval.',
 'AmountThreshold', 10000.00, NULL, 'High'),

('RULE-002', 'Duplicate Transaction Detection',
 'Flags transactions on the same account, for the same amount, posted within the configured window -- a classic sign of duplicate payment or data entry error.',
 'DuplicateTransaction', NULL, 3, 'Medium'),

('RULE-003', 'Round-Dollar Amount Pattern',
 'Flags suspiciously round amounts (e.g. exactly $5,000.00) above a minimum size, a common indicator of estimated/fabricated entries.',
 'RoundDollar', 1000.00, NULL, 'Low'),

('RULE-004', 'Weekend / Non-Business-Day Posting',
 'Flags transactions posted on a Saturday or Sunday, which fall outside normal processing and may indicate bypassed controls.',
 'WeekendPosting', NULL, NULL, 'Medium'),

('RULE-005', 'Missing Approval on High-Value Item',
 'Flags transactions above the threshold that remain in "Pending" status for longer than the allowed window without approval.',
 'MissingApproval', 25000.00, 2, 'Critical'),

('RULE-006', 'Transaction Velocity Check',
 'Flags an account that has an unusually high number of transactions within a short rolling window, a possible sign of structuring.',
 'VelocityCheck', 5.00, 1, 'High');
GO

PRINT 'FCAMS seed data inserted. Login as admin / Admin@123 (or compliance / Comply@123, auditor / Audit@123).';
GO
