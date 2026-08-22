using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using FCAMS.Web.Models;

namespace FCAMS.Web.DAL.Repositories
{
    public interface IUserRepository
    {
        User GetForAuthentication(string username);
        void RecordLoginSuccess(int userId);
        void RecordLoginFailure(int userId);

        int Create(User user);
        List<User> GetAll();

        User GetById(int userId);
        void Update(User user);
    }

    public class UserRepository : IUserRepository
    {
        public User GetForAuthentication(string username)
        {
            using (var db = new SqlDataAccess())
            {
                var results = db.ExecuteReader(
                    "dbo.usp_User_GetForAuthentication",
                    MapUser,
                    new SqlParameter("@Username", username));

                return results.Count > 0 ? results[0] : null;
            }
        }

        public void RecordLoginSuccess(int userId)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery(
                    "dbo.usp_User_RecordLoginSuccess",
                    new SqlParameter("@UserId", userId));
            }
        }

        public void RecordLoginFailure(int userId)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery(
                    "dbo.usp_User_RecordLoginFailure",
                    new SqlParameter("@UserId", userId));
            }
        }

        public int Create(User user)
        {
            using (var db = new SqlDataAccess())
            {
                var outputParam = new SqlParameter(
                    "@NewUserId",
                    SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };

                return db.ExecuteWithOutputParameter<int>(
                    "dbo.usp_User_Create",
                    "@NewUserId",
                    new SqlParameter("@Username", user.Username),
                    new SqlParameter("@Email", user.Email),
                    new SqlParameter("@FullName", user.FullName),
                    new SqlParameter("@PasswordHash", user.PasswordHash),
                    new SqlParameter("@PasswordSalt", user.PasswordSalt),
                    new SqlParameter("@RoleId", user.RoleId),
                    new SqlParameter(
                        "@DepartmentId",
                        (object)user.DepartmentId ?? DBNull.Value),
                    outputParam);
            }
        }

        public List<User> GetAll()
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader(
                    "dbo.usp_User_GetAll",
                    reader => new User
                    {
                        UserId = reader.GetInt32(
                            reader.GetOrdinal("UserId")),

                        Username = reader.GetString(
                            reader.GetOrdinal("Username")),

                        Email = reader.GetString(
                            reader.GetOrdinal("Email")),

                        FullName = reader.GetString(
                            reader.GetOrdinal("FullName")),

                        RoleName = reader.GetString(
                            reader.GetOrdinal("RoleName")),

                        DepartmentName = reader.GetStringOrNull(
                            "DepartmentName"),

                        IsActive = reader.GetBoolean(
                            reader.GetOrdinal("IsActive")),

                        CreatedDate = reader.GetDateTime(
                            reader.GetOrdinal("CreatedDate")),

                        LastLoginDate = reader.GetDateTimeOrNull(
                            "LastLoginDate")
                    });
            }
        }

        public User GetById(int userId)
        {
            using (var db = new SqlDataAccess())
            {
                var results = db.ExecuteReader(
                    "dbo.usp_User_GetById",
                    MapAdminUser,
                    new SqlParameter("@UserId", userId));

                return results.Count > 0 ? results[0] : null;
            }
        }

        public void Update(User user)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery(
                    "dbo.usp_User_Update",

                    new SqlParameter(
                        "@UserId",
                        user.UserId),

                    new SqlParameter(
                        "@Username",
                        user.Username),

                    new SqlParameter(
                        "@Email",
                        user.Email),

                    new SqlParameter(
                        "@FullName",
                        user.FullName),

                    new SqlParameter(
                        "@RoleId",
                        user.RoleId),

                    new SqlParameter(
                        "@DepartmentId",
                        (object)user.DepartmentId ?? DBNull.Value),

                    new SqlParameter(
                        "@IsActive",
                        user.IsActive),

                    new SqlParameter(
                        "@PasswordHash",
                        SqlDbType.VarBinary,
                        64)
                    {
                        Value = (object)user.PasswordHash
                            ?? DBNull.Value
                    },

                    new SqlParameter(
                        "@PasswordSalt",
                        SqlDbType.VarBinary,
                        32)
                    {
                        Value = (object)user.PasswordSalt
                            ?? DBNull.Value
                    });
            }
        }

        private static User MapUser(IDataReader reader)
        {
            return new User
            {
                UserId = reader.GetInt32(
                    reader.GetOrdinal("UserId")),

                Username = reader.GetString(
                    reader.GetOrdinal("Username")),

                Email = reader.GetString(
                    reader.GetOrdinal("Email")),

                FullName = reader.GetString(
                    reader.GetOrdinal("FullName")),

                PasswordHash = (byte[])reader["PasswordHash"],

                PasswordSalt = (byte[])reader["PasswordSalt"],

                RoleId = reader.GetInt32(
                    reader.GetOrdinal("RoleId")),

                RoleName = reader.GetString(
                    reader.GetOrdinal("RoleName")),

                DepartmentId = reader.GetIntOrNull(
                    "DepartmentId"),

                IsActive = reader.GetBoolean(
                    reader.GetOrdinal("IsActive")),

                FailedLoginCount = reader.GetInt32(
                    reader.GetOrdinal("FailedLoginCount")),

                LockedOutUntil = reader.GetDateTimeOrNull(
                    "LockedOutUntil")
            };
        }

        private static User MapAdminUser(IDataReader reader)
        {
            return new User
            {
                UserId = reader.GetInt32(
                    reader.GetOrdinal("UserId")),

                Username = reader.GetString(
                    reader.GetOrdinal("Username")),

                Email = reader.GetString(
                    reader.GetOrdinal("Email")),

                FullName = reader.GetString(
                    reader.GetOrdinal("FullName")),

                PasswordHash = reader["PasswordHash"] == DBNull.Value
                    ? null
                    : (byte[])reader["PasswordHash"],

                PasswordSalt = reader["PasswordSalt"] == DBNull.Value
                    ? null
                    : (byte[])reader["PasswordSalt"],

                RoleId = reader.GetInt32(
                    reader.GetOrdinal("RoleId")),

                RoleName = reader.GetString(
                    reader.GetOrdinal("RoleName")),

                DepartmentId = reader.GetIntOrNull(
                    "DepartmentId"),

                DepartmentName = reader.GetStringOrNull(
                    "DepartmentName"),

                IsActive = reader.GetBoolean(
                    reader.GetOrdinal("IsActive")),

                FailedLoginCount = reader.GetInt32(
                    reader.GetOrdinal("FailedLoginCount")),

                LockedOutUntil = reader.GetDateTimeOrNull(
                    "LockedOutUntil"),

                CreatedDate = reader.GetDateTime(
                    reader.GetOrdinal("CreatedDate")),

                LastLoginDate = reader.GetDateTimeOrNull(
                    "LastLoginDate")
            };
        }
    }
}