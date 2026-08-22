using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using FCAMS.Web.Models;

namespace FCAMS.Web.DAL.Repositories
{
    public interface IAuditLogRepository
    {
        void Insert(AuditLogEntry entry);
        List<AuditLogEntry> GetRecent(int top);
        List<AuditLogEntry> GetByEntity(string entityName, string entityId);
        PagedResult<AuditLogEntry> GetPaged(int? userId, string actionType, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize);
    }

    public class AuditLogRepository : IAuditLogRepository
    {
        public void Insert(AuditLogEntry entry)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery("dbo.usp_AuditLog_Insert",
                    new SqlParameter("@UserId", (object)entry.UserId ?? DBNull.Value),
                    new SqlParameter("@Username", (object)entry.Username ?? DBNull.Value),
                    new SqlParameter("@ActionType", entry.ActionType),
                    new SqlParameter("@EntityName", entry.EntityName),
                    new SqlParameter("@EntityId", (object)entry.EntityId ?? DBNull.Value),
                    new SqlParameter("@OldValues", (object)entry.OldValues ?? DBNull.Value),
                    new SqlParameter("@NewValues", (object)entry.NewValues ?? DBNull.Value),
                    new SqlParameter("@IPAddress", (object)entry.IPAddress ?? DBNull.Value),
                    new SqlParameter("@UserAgent", (object)entry.UserAgent ?? DBNull.Value));
            }
        }

        public List<AuditLogEntry> GetRecent(int top)
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_AuditLog_GetRecent", reader => new AuditLogEntry
                {
                    AuditLogId = reader.GetInt64(reader.GetOrdinal("AuditLogId")),
                    UserId = reader.GetIntOrNull("UserId"),
                    Username = reader.GetStringOrNull("Username"),
                    ActionType = reader.GetString(reader.GetOrdinal("ActionType")),
                    EntityName = reader.GetString(reader.GetOrdinal("EntityName")),
                    EntityId = reader.GetStringOrNull("EntityId"),
                    Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp"))
                }, new SqlParameter("@Top", top));
            }
        }

        public List<AuditLogEntry> GetByEntity(string entityName, string entityId)
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_AuditLog_GetByEntity", reader => new AuditLogEntry
                {
                    AuditLogId = reader.GetInt64(reader.GetOrdinal("AuditLogId")),
                    UserId = reader.GetIntOrNull("UserId"),
                    Username = reader.GetStringOrNull("Username"),
                    ActionType = reader.GetString(reader.GetOrdinal("ActionType")),
                    OldValues = reader.GetStringOrNull("OldValues"),
                    NewValues = reader.GetStringOrNull("NewValues"),
                    IPAddress = reader.GetStringOrNull("IPAddress"),
                    Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp"))
                }, new SqlParameter("@EntityName", entityName), new SqlParameter("@EntityId", entityId));
            }
        }

        public PagedResult<AuditLogEntry> GetPaged(int? userId, string actionType, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize)
        {
            int totalRowCount = 0;
            List<AuditLogEntry> items;

            using (var db = new SqlDataAccess())
            {
                items = db.ExecuteReader("dbo.usp_AuditLog_GetPaged", reader =>
                {
                    totalRowCount = reader.GetInt32(reader.GetOrdinal("TotalRowCount"));
                    return new AuditLogEntry
                    {
                        AuditLogId = reader.GetInt64(reader.GetOrdinal("AuditLogId")),
                        UserId = reader.GetIntOrNull("UserId"),
                        Username = reader.GetStringOrNull("Username"),
                        ActionType = reader.GetString(reader.GetOrdinal("ActionType")),
                        EntityName = reader.GetString(reader.GetOrdinal("EntityName")),
                        EntityId = reader.GetStringOrNull("EntityId"),
                        IPAddress = reader.GetStringOrNull("IPAddress"),
                        Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp"))
                    };
                },
                new SqlParameter("@UserId", (object)userId ?? DBNull.Value),
                new SqlParameter("@ActionType", (object)actionType ?? DBNull.Value),
                new SqlParameter("@DateFrom", (object)dateFrom ?? DBNull.Value),
                new SqlParameter("@DateTo", (object)dateTo ?? DBNull.Value),
                new SqlParameter("@PageNumber", pageNumber),
                new SqlParameter("@PageSize", pageSize));
            }

            return new PagedResult<AuditLogEntry> { Items = items, TotalRowCount = totalRowCount, PageNumber = pageNumber, PageSize = pageSize };
        }
    }

    public interface IExceptionLogRepository
    {
        void Insert(ExceptionLogEntry entry);
        List<ExceptionLogEntry> GetRecent(int top, bool unresolvedOnly);
    }

    public class ExceptionLogRepository : IExceptionLogRepository
    {
        public void Insert(ExceptionLogEntry entry)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery("dbo.usp_ExceptionLog_Insert",
                    new SqlParameter("@Source", entry.Source),
                    new SqlParameter("@Message", Truncate(entry.Message, 2000)),
                    new SqlParameter("@ExceptionType", (object)entry.ExceptionType ?? DBNull.Value),
                    new SqlParameter("@StackTrace", (object)entry.StackTrace ?? DBNull.Value),
                    new SqlParameter("@InnerException", (object)entry.InnerException ?? DBNull.Value),
                    new SqlParameter("@Severity", entry.Severity ?? "Error"),
                    new SqlParameter("@RequestUrl", (object)entry.RequestUrl ?? DBNull.Value),
                    new SqlParameter("@UserId", (object)entry.UserId ?? DBNull.Value));
            }
        }

        public List<ExceptionLogEntry> GetRecent(int top, bool unresolvedOnly)
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_ExceptionLog_GetRecent", reader => new ExceptionLogEntry
                {
                    ExceptionLogId = reader.GetInt64(reader.GetOrdinal("ExceptionLogId")),
                    Source = reader.GetString(reader.GetOrdinal("Source")),
                    Message = reader.GetString(reader.GetOrdinal("Message")),
                    ExceptionType = reader.GetStringOrNull("ExceptionType"),
                    Severity = reader.GetString(reader.GetOrdinal("Severity")),
                    RequestUrl = reader.GetStringOrNull("RequestUrl"),
                    UserId = reader.GetIntOrNull("UserId"),
                    Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                    IsResolved = reader.GetBoolean(reader.GetOrdinal("IsResolved"))
                }, new SqlParameter("@Top", top), new SqlParameter("@UnresolvedOnly", unresolvedOnly));
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
            return value.Substring(0, maxLength);
        }
    }
}
