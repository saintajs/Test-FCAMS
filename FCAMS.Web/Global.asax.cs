using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using FCAMS.Web.App_Start;
using FCAMS.Web.Services;

namespace FCAMS.Web
{
    /// <summary>
    /// Application entry point. Wires up MVC routing/filters and installs a
    /// last-chance Application_Error handler so that even exceptions which
    /// occur *outside* the MVC pipeline (e.g. in routing itself, or in a
    /// module) still get written to ExceptionLogs instead of vanishing into
    /// the IIS event log only.
    /// </summary>
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(System.Web.Optimization.BundleTable.Bundles);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var exception = Server.GetLastError();
            if (exception == null) return;

            try
            {
                var logger = new ExceptionLogService();
                logger.LogException(exception, source: "Global.Application_Error",
                    requestUrl: Request?.Url?.ToString(), userId: null);
            }
            catch
            {
                // Logging must never itself crash the error pipeline. If the
                // database is unreachable, the exception simply falls
                // through to IIS's own event log as a last resort.
            }
        }
    }
}
