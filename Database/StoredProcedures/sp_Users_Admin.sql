USE FCAMS;
GO

IF OBJECT_ID('dbo.usp_User_GetById', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_User_GetById;
GO

CREATE PROCEDURE dbo.usp_User_GetById
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.Username,
        u.Email,
        u.FullName,
        u.PasswordHash,
        u.PasswordSalt,
        u.RoleId,
        r.RoleName,
        u.DepartmentId,
        d.DepartmentName,
        u.IsActive,
        u.FailedLoginCount,
        u.LockedOutUntil,
        u.CreatedDate,
        u.LastLoginDate
    FROM dbo.Users u
    INNER JOIN dbo.Roles r
        ON r.RoleId = u.RoleId
    LEFT JOIN dbo.Departments d
        ON d.DepartmentId = u.DepartmentId
    WHERE u.UserId = @UserId;
END
GO


IF OBJECT_ID('dbo.usp_User_Update', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_User_Update;
GO

CREATE PROCEDURE dbo.usp_User_Update
    @UserId INT,
    @Username NVARCHAR(50),
    @Email NVARCHAR(150),
    @FullName NVARCHAR(150),
    @RoleId INT,
    @DepartmentId INT = NULL,
    @IsActive BIT,
    @PasswordHash VARBINARY(64) = NULL,
    @PasswordSalt VARBINARY(32) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.Users
        WHERE UserId = @UserId
    )
    BEGIN
        RAISERROR('User was not found.', 16, 1);
        RETURN;
    END

    IF EXISTS (
        SELECT 1
        FROM dbo.Users
        WHERE Username = @Username
          AND UserId <> @UserId
    )
    BEGIN
        RAISERROR('Username is already taken.', 16, 1);
        RETURN;
    END

    IF EXISTS (
        SELECT 1
        FROM dbo.Users
        WHERE Email = @Email
          AND UserId <> @UserId
    )
    BEGIN
        RAISERROR('Email is already registered.', 16, 1);
        RETURN;
    END

    UPDATE dbo.Users
    SET
        Username = @Username,
        Email = @Email,
        FullName = @FullName,
        RoleId = @RoleId,
        DepartmentId = @DepartmentId,
        IsActive = @IsActive,
        PasswordHash =
            CASE
                WHEN @PasswordHash IS NULL THEN PasswordHash
                ELSE @PasswordHash
            END,
        PasswordSalt =
            CASE
                WHEN @PasswordSalt IS NULL THEN PasswordSalt
                ELSE @PasswordSalt
            END
    WHERE UserId = @UserId;
END
GO

PRINT 'Admin user procedures created successfully.';
GO
