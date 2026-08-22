using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using FCAMS.Web.Services;
using Newtonsoft.Json;

namespace FCAMS.Web.Infrastructure
{
    /// <summary>
    /// Restricts an action/controller to one or more FCAMS roles.
    /// </summary>
    public class AuthorizeRoleAttribute : AuthorizeAttribute
    {
        private readonly string[] _allowedRoles;

        public AuthorizeRoleAttribute(params string[] allowedRoles)
        {
            _allowedRoles = allowedRoles ?? new string[0];
        }

        protected override bool AuthorizeCore(System.Web.HttpContextBase httpContext)
        {
            // Check normal Forms Authentication first.
            if (!base.AuthorizeCore(httpContext))
                return false;

            // Read the authenticated user's Forms Authentication ticket.
            var identity = httpContext.User?.Identity as FormsIdentity;

            if (identity?.Ticket == null ||
                string.IsNullOrEmpty(identity.Ticket.UserData))
            {
                return false;
            }

            CurrentUserInfo currentUser;

            try
            {
                currentUser =
                    JsonConvert.DeserializeObject<CurrentUserInfo>(
                        identity.Ticket.UserData);
            }
            catch
            {
                return false;
            }

            if (currentUser == null ||
                string.IsNullOrWhiteSpace(currentUser.RoleName))
            {
                return false;
            }

            // Make the user available to the rest of the request.
            SessionUser.Current = currentUser;

            // Check whether this user's role is allowed.
            return _allowedRoles.Any(role =>
                string.Equals(
                    role,
                    currentUser.RoleName,
                    StringComparison.OrdinalIgnoreCase));
        }

        protected override void HandleUnauthorizedRequest(
            AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.Request.IsAuthenticated)
            {
                filterContext.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/Forbidden.cshtml"
                };

                filterContext.HttpContext.Response.StatusCode = 403;
            }
            else
            {
                base.HandleUnauthorizedRequest(filterContext);
            }
        }
    }
}
