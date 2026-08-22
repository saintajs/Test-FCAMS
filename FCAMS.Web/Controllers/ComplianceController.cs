using System.Web.Mvc;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Infrastructure;
using FCAMS.Web.Models.ViewModels;
using FCAMS.Web.Services;

namespace FCAMS.Web.Controllers
{
    [AuthorizeRole("Administrator", "ComplianceOfficer", "Auditor")]
    public class ComplianceController : Controller
    {
        private readonly IComplianceRepository _complianceRepository;
        private readonly IAuditLogService _auditLog;

        public ComplianceController() : this(new ComplianceRepository(), new AuditLogService()) { }

        public ComplianceController(IComplianceRepository complianceRepository, IAuditLogService auditLog)
        {
            _complianceRepository = complianceRepository;
            _auditLog = auditLog;
        }

        [AuditableAction("View", "ComplianceViolation")]
        public ActionResult Violations(string severity)
        {
            ViewBag.SelectedSeverity = severity;
            var violations = _complianceRepository.GetOpenViolations(severity);
            return View(violations);
        }

        [AuthorizeRole("Administrator", "ComplianceOfficer")]
        public ActionResult Resolve(long id)
        {
            var violation = _complianceRepository.GetOpenViolations(null).Find(v => v.ViolationId == id);
            if (violation == null) return HttpNotFound();

            return View(new ViolationResolveViewModel
            {
                ViolationId = violation.ViolationId,
                TransactionRef = violation.TransactionRef,
                RuleName = violation.RuleName,
                DetailMessage = violation.DetailMessage
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrator", "ComplianceOfficer")]
        public ActionResult Resolve(ViolationResolveViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var currentUser = SessionUser.Current;
            _complianceRepository.ResolveViolation(model.ViolationId, model.NewStatus, currentUser.UserId, model.ResolutionNotes);

            _auditLog.LogUpdate("ComplianceViolation", model.ViolationId.ToString(),
                oldValues: new { Status = "Open" },
                newValues: new { Status = model.NewStatus, model.ResolutionNotes });

            TempData["Message"] = $"Violation on {model.TransactionRef} marked as {model.NewStatus}.";
            return RedirectToAction("Violations");
        }

        [AuthorizeRole("Administrator", "ComplianceOfficer")]
        public ActionResult Rules()
        {
            return View(_complianceRepository.GetAllRules());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrator", "ComplianceOfficer")]
        public ActionResult ToggleRule(int ruleId, bool isActive)
        {
            _complianceRepository.SetRuleActive(ruleId, isActive);
            _auditLog.LogUpdate("ComplianceRule", ruleId.ToString(), new { IsActive = !isActive }, new { IsActive = isActive });
            return RedirectToAction("Rules");
        }
    }
}
