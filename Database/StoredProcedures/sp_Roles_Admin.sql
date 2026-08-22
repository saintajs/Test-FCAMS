USE FCAMS;
GO

IF OBJECT_ID('dbo.usp_Role_GetAll', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Role_GetAll;
GO

CREATE PROCEDURE dbo.usp_Role_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        RoleId,
        RoleName,
        Description
    FROM dbo.Roles
    ORDER BY RoleId;
END
GO

PRINT 'Role stored procedure created successfully.';
GO
