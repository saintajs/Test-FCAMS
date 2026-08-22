/* ============================================================================
   Stored Procedures: Users
   ============================================================================ */
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- usp_User_GetForAuthentication
-- Returns the hash/salt for a username so the application layer can perform
-- the comparison. The password is NEVER compared in T-SQL with a plain-text
-- parameter; only the pre-computed hash the app builds from the salt is
-- compared, and even that comparison happens in C# (see AuthenticationService).
-- =============================================
IF OBJECT_ID('dbo.usp_User_GetForAuthentication', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_User_GetForAuthentication;
GO
CREATE PROCEDURE dbo.usp_User_GetForAuthentication
    @Username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  u.UserId, u.Username, u.Email, u.FullName, u.PasswordHash, u.PasswordSalt,
            u.RoleId, r.RoleName, u.DepartmentId, u.IsActive,
            u.FailedLoginCount, u.LockedOutUntil
    FROM    dbo.Users u
    INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
    WHERE   u.Username = @Username;
END
GO

-- =============================================
-- usp_User_RecordLoginSuccess
-- =============================================
IF OBJECT_ID('dbo.usp_User_RecordLoginSuccess', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_User_RecordLoginSuccess;
GO
CREATE PROCEDURE dbo.usp_User_RecordLoginSuccess
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users
    SET LastLoginDate = SYSUTCDATETIME(),
        FailedLoginCount = 0,
        LockedOutUntil = NULL
    WHERE UserId = @UserId;
END
GO

-- =============================================
-- usp_User_RecordLoginFailure
-- Locks the account for 15 minutes after 5 consecutive failures.
-- =============================================
IF OBJECT_ID('dbo.usp_User_RecordLoginFailure', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_User_RecordLoginFailure;
GO
CREATE PROCEDURE dbo.usp_User_RecordLoginFailure
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Users
    SET FailedLoginCount = FailedLoginCount + 1,
        LockedOutUntil = CASE WHEN FailedLoginCount + 1 >= 5
                               THEN DATEADD(MINUTE, 15, SYSUTCDATETIME())
                               ELSE LockedOutUntil END
    WHERE UserId = @UserId;
END
GO

-- =============================================
-- usp_User_Create
-- =============================================
IF OBJECT_ID('dbo.usp_User_Create', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_User_Create;
GO
CREATE PROCEDURE dbo.usp_User_Create
    @Username       NVARCHAR(50),
    @Email          NVARCHAR(150),
    @FullName       NVARCHAR(150),
    @PasswordHash   VARBINARY(64),
    @PasswordSalt   VARBINARY(32),
    @RoleId         INT,
    @DepartmentId   INT = NULL,
    @NewUserId      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @Username)
    BEGIN
        RAISERROR('Username "%s" is already taken.', 16, 1, @Username);
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email)
    BEGIN
        RAISERROR('Email "%s" is already registered.', 16, 1, @Email);
        RETURN;
    END

    INSERT INTO dbo.Users (Username, Email, FullName, PasswordHash, PasswordSalt, RoleId, DepartmentId)
    VALUES (@Username, @Email, @FullName, @PasswordHash, @PasswordSalt, @RoleId, @DepartmentId);

    SET @NewUserId = SCOPE_IDENTITY();
END
GO

-- =============================================
-- usp_User_GetAll  (for the Admin > Users screen)
-- =============================================
IF OBJECT_ID('dbo.usp_User_GetAll', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_User_GetAll;
GO
CREATE PROCEDURE dbo.usp_User_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  u.UserId, u.Username, u.Email, u.FullName, r.RoleName, d.DepartmentName,
            u.IsActive, u.CreatedDate, u.LastLoginDate
    FROM    dbo.Users u
    INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
    LEFT JOIN  dbo.Departments d ON d.DepartmentId = u.DepartmentId
    ORDER BY u.FullName;
END
GO

-- =============================================
-- usp_Department_GetActive
-- =============================================
IF OBJECT_ID('dbo.usp_Department_GetActive', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Department_GetActive;
GO
CREATE PROCEDURE dbo.usp_Department_GetActive
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DepartmentId, DepartmentName, CostCenterCode, IsActive
    FROM   dbo.Departments
    WHERE  IsActive = 1
    ORDER BY DepartmentName;
END
GO

PRINT 'User stored procedures created.';
GO
