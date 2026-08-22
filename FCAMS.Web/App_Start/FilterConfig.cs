using System.Web.Mvc;
using FCAMS.Web.Infrastructure;

namespace FCAMS.Web.App_Start
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // Order matters: the exception filter must run for every action,
            // and AuthorizeAttribute enforces that only authenticated users
            // reach anything other than [AllowAnonymous] actions.
            // filters.Add(new GlobalExceptionFilter());
            filters.Add(new CurrentUserActionFilter());
            filters.Add(new AuthorizeAttribute());
        }
    }
}
