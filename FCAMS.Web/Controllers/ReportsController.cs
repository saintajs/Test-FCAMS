using System;
using System.Web.Mvc;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Infrastructure;
using FCAMS.Web.Models.ViewModels;
using FCAMS.Web.Services;

namespace FCAMS.Web.Controllers
{
    [AuthorizeRole("Administrator", "ComplianceOfficer", "Auditor")]
    public class ReportsController : Controller
    {
        private readonly IReportService _reportService;
        private readonly IReportRepository _reportRepository;

        public ReportsController() : this(new ReportService(), new ReportRepository()) { }

        public ReportsController(IReportService reportService, IReportRepository reportRepository)
        {
            _reportService = reportService;
            _reportRepository = reportRepository;
        }

        [AuditableAction("View", "Report")]
        public ActionResult Index()
        {
            return View(new ReportParametersViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuditableAction("Export", "ComplianceSummaryReport")]
        public ActionResult ComplianceSummary(ReportParametersViewModel model)
        {
            if (!ModelState.IsValid || model.DateTo < model.DateFrom)
            {
                TempData["Error"] = "Please provide a valid date range.";
                return RedirectToAction("Index");
            }

            var currentUser = Services.SessionUser.Current;
            byte[] pdfBytes = _reportService.GenerateComplianceSummaryPdf(model.DateFrom, model.DateTo, currentUser.UserId);

            string fileName = $"ComplianceSummary_{model.DateFrom:yyyyMMdd}_{model.DateTo:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpGet]
        public ActionResult AuditTrail()
        {
            return RedirectToAction("Index");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuditableAction("Export", "AuditTrailReport")]
        public ActionResult AuditTrail(ReportParametersViewModel model)
        {
            if (!ModelState.IsValid || model.DateTo < model.DateFrom)
            {
                TempData["Error"] = "Please provide a valid date range.";
                return RedirectToAction("Index");
            }

            var currentUser = Services.SessionUser.Current;
            byte[] pdfBytes = _reportService.GenerateAuditTrailPdf(model.DateFrom, model.DateTo, currentUser.UserId);

            string fileName = $"AuditTrail_{model.DateFrom:yyyyMMdd}_{model.DateTo:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}


