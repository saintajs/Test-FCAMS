using System;
using System.Web.Mvc;
using FCAMS.Web.Services;

namespace FCAMS.Web.Infrastructure
{
    /// <summary>
    /// Catches any unhandled exception from an MVC action, logs it to
    /// dbo.ExceptionLogs via ExceptionLogService, and renders a friendly
    /// error view instead of a stack trace. AJAX requests get a JSON error
    /// payload instead of an HTML page, since a redirect/full page render
    /// makes no sense inside an XHR call.
    /// </summary>
    public class GlobalExceptionFilter : HandleErrorAttribute
    {
        public override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.ExceptionHandled) return;

            var exception = filterContext.Exception;
            string source = $"{filterContext.RouteData.Values["controller"]}." +
                             $"{filterContext.RouteData.Values["action"]}";
            string requestUrl = filterContext.HttpContext.Request?.Url?.ToString();
            int? userId = null; try { userId = Services.SessionUser.Current?.UserId; } catch { }

            try
            {
                new ExceptionLogService().LogException(exception, source, requestUrl, userId);
            }
            catch
            {
                // Never let logging failure mask the original exception handling.
            }

            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                filterContext.Result = new JsonResult
                {
                    Data = new { success = false, message = "An unexpected error occurred. The issue has been logged." },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
                filterContext.HttpContext.Response.StatusCode = 500;
                filterContext.ExceptionHandled = true;
                return;
            }

            var controllerName = (string)filterContext.RouteData.Values["controller"];
            var actionName = (string)filterContext.RouteData.Values["action"];

            filterContext.Result = new ContentResult
            {
                Content = "<pre>" + exception.ToString() + "</pre>"
            };
            filterContext.HttpContext.Response.Clear();
            filterContext.HttpContext.Response.StatusCode = 500;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.ExceptionHandled = true;
        }
    }
}



