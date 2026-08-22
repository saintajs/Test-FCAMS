using System.Web.Mvc;
using System.Web.Routing;

namespace FCAMS.Web.App_Start
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("favicon.ico");

        routes.MapRoute(
            name: "Reports",
            url: "Reports/{action}/{id}",
            defaults: new { controller = "Reports", action = "Index", id = UrlParameter.Optional }
        );
            routes.MapRoute(
                name: "AuditTrail",
                url: "AuditTrail/{id}",
                defaults: new
                {
                    controller = "Reports",
                    action = "AuditTrail",
                    id = UrlParameter.Optional
                }
            );


            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}







