using System;
using System.Web;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Models;
using Newtonsoft.Json;

namespace FCAMS.Web.Services
{
    public interface IAuditLogService
    {
        void LogCreate(string entityName, string entityId, object newValues);
        void LogUpdate(string entityName, string entityId, object oldValues, object newValues);
        void LogDelete(string entityName, string entityId, object oldValues);
        void LogAction(string actionType, string entityName, string entityId = null, object oldValues = null, object newValues = null);
    }

    /// <summary>
    /// Writes to dbo.AuditLogs. Called explicitly by controllers/services at
    /// the point of a state change (rather than relying purely on a global
    /// filter) so that OldValues/NewValues can be captured with real domain
    /// objects, not raw HTTP form data. AuditActionFilter (Infrastructure/)
    /// additionally logs Login/Logout/View/Export actions generically for
    /// any action decorated with [AuditableAction].
    /// </summary>
    public class AuditLogService : IAuditLogService
    {
        private readonly IAuditLogRepository _repository;

        public AuditLogService() : this(new AuditLogRepository()) { }

        public AuditLogService(IAuditLogRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void LogCreate(string entityName, string entityId, object newValues)
            => LogAction("Create", entityName, entityId, null, newValues);

        public void LogUpdate(string entityName, string entityId, object oldValues, object newValues)
            => LogAction("Update", entityName, entityId, oldValues, newValues);

        public void LogDelete(string entityName, string entityId, object oldValues)
            => LogAction("Delete", entityName, entityId, oldValues, null);

        public void LogAction(string actionType, string entityName, string entityId = null, object oldValues = null, object newValues = null)
        {
            var entry = new AuditLogEntry
            {
                UserId = CurrentUserId(),
                Username = CurrentUsername(),
                ActionType = actionType,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues == null ? null : JsonConvert.SerializeObject(oldValues),
                NewValues = newValues == null ? null : JsonConvert.SerializeObject(newValues),
                IPAddress = CurrentIpAddress(),
                UserAgent = CurrentUserAgent()
            };
            _repository.Insert(entry);
        }

        private static int? CurrentUserId()
        {
            var identity = HttpContext.Current?.User?.Identity;
            if (identity == null || !identity.IsAuthenticated) return null;
            return SessionUser.Current?.UserId;
        }

        private static string CurrentUsername() => HttpContext.Current?.User?.Identity?.Name;

        private static string CurrentIpAddress() => HttpContext.Current?.Request?.UserHostAddress;

        private static string CurrentUserAgent()
        {
            var ua = HttpContext.Current?.Request?.UserAgent;
            if (string.IsNullOrEmpty(ua)) return null;
            return ua.Length > 300 ? ua.Substring(0, 300) : ua;
        }
    }

    public interface IExceptionLogService
    {
        void LogException(Exception exception, string source, string requestUrl, int? userId);
    }

    public class ExceptionLogService : IExceptionLogService
    {
        private readonly IExceptionLogRepository _repository;

        public ExceptionLogService() : this(new ExceptionLogRepository()) { }

        public ExceptionLogService(IExceptionLogRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void LogException(Exception exception, string source, string requestUrl, int? userId)
        {
            if (exception == null) return;

            var entry = new ExceptionLogEntry
            {
                Source = source ?? "Unknown",
                Message = exception.Message,
                ExceptionType = exception.GetType().FullName,
                StackTrace = exception.StackTrace,
                InnerException = exception.InnerException?.ToString(),
                Severity = ClassifySeverity(exception),
                RequestUrl = requestUrl,
                UserId = userId
            };
            _repository.Insert(entry);
        }

        private static string ClassifySeverity(Exception exception)
        {
            // Data-layer and out-of-memory conditions are treated as
            // Critical because they indicate the application may not be
            // able to serve *any* request, not just the current one.
            if (exception is System.Data.SqlClient.SqlException || exception is OutOfMemoryException)
                return "Critical";
            if (exception is UnauthorizedAccessException || exception is System.Security.SecurityException)
                return "Warning";
            return "Error";
        }
    }

    /// <summary>
    /// Lightweight accessor for the currently signed-in user's claims,
    /// populated by AccountController at login time into the Forms
    /// authentication ticket and re-hydrated per request by
    /// CurrentUserActionFilter. Avoids a database round trip on every
    /// single request just to know "who is this and what role are they".
    /// </summary>
    public static class SessionUser
    {
        public static CurrentUserInfo Current
        {
            get { return HttpContext.Current?.Items["FCAMS_CurrentUser"] as CurrentUserInfo; }
            set { if (HttpContext.Current != null) HttpContext.Current.Items["FCAMS_CurrentUser"] = value; }
        }
    }

    public class CurrentUserInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string RoleName { get; set; }
        public int? DepartmentId { get; set; }
    }
}
