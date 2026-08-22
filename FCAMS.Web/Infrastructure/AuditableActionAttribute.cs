using System;
using System.Web.Mvc;
using System.Web.Security;
using FCAMS.Web.Services;
using Newtonsoft.Json;

namespace FCAMS.Web.Infrastructure
{
    /// <summary>
    /// Decorate an action with [AuditableAction("View")] or
    /// [AuditableAction("Export")] to have generic, non-mutating activity
    /// logged automatically (Create/Update/Delete are logged explicitly by
    /// the calling service instead, since those need real before/after
    /// values that a filter can't see).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AuditableActionAttribute : ActionFilterAttribute
    {
        private readonly string _actionType;
        private readonly string _entityName;

        public AuditableActionAttribute(string actionType, string entityName = null)
        {
            _actionType = actionType;
            _entityName = entityName;
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            if (filterContext.Exception != null && !filterContext.ExceptionHandled) return;

            string entity = _entityName ?? (string)filterContext.RouteData.Values["controller"];
            string entityId = filterContext.RouteData.Values["id"]?.ToString();

            try
            {
                new AuditLogService().LogAction(_actionType, entity, entityId);
            }
            catch
            {
                // Auditing must never break the user-facing request.
            }
        }
    }

    /// <summary>
    /// Runs on every request for an authenticated user: rehydrates
    /// SessionUser.Current from the Forms authentication ticket's
    /// UserData (a small JSON blob set at login, see AccountController),
    /// so downstream code can read "who is this" without a DB round trip
    /// per request.
    /// </summary>
   public class CurrentUserActionFilter : FilterAttribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationContext filterContext)
    {
        var identity = filterContext.HttpContext.User?.Identity as FormsIdentity;

        if (identity?.Ticket != null &&
            !string.IsNullOrEmpty(identity.Ticket.UserData))
        {
            try
            {
                var info = JsonConvert.DeserializeObject<CurrentUserInfo>(
                    identity.Ticket.UserData);

                SessionUser.Current = info;
            }
            catch
            {
                SessionUser.Current = null;
            }
        }
        else
        {
            SessionUser.Current = null;
        }
    }
}

}
