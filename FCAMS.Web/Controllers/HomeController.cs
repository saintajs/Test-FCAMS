using System.Web.Mvc;
using FCAMS.Web.DAL.Repositories;

namespace FCAMS.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IReportRepository _reportRepository;

        public HomeController() : this(new ReportRepository()) { }

        public HomeController(IReportRepository reportRepository)
        {
            _reportRepository = reportRepository;
        }

        public ActionResult Index()
        {
            var model = _reportRepository.GetDashboardSummary();
            return View(model);
        }

        public ActionResult About()
        {
            return View();
        }
    }
}
