using System.Linq;
using System.Web.Mvc;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Infrastructure;
using FCAMS.Web.Models;
using FCAMS.Web.Models.ViewModels;
using FCAMS.Web.Services;

namespace FCAMS.Web.Controllers
{
    [AuthorizeRole("Administrator", "Auditor", "ComplianceOfficer")]
    public class AuditController : Controller
    {
        private readonly IAuditRepository _auditRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAuditLogService _auditLog;

        public AuditController()
            : this(new AuditRepository(), new DepartmentRepository(), new UserRepository(), new AuditLogService())
        {
        }

        public AuditController(IAuditRepository auditRepository, IDepartmentRepository departmentRepository,
            IUserRepository userRepository, IAuditLogService auditLog)
        {
            _auditRepository = auditRepository;
            _departmentRepository = departmentRepository;
            _userRepository = userRepository;
            _auditLog = auditLog;
        }

        [AuditableAction("View", "AuditEngagement")]
        public ActionResult Index()
        {
            return View(_auditRepository.GetAllEngagements());
        }

        public ActionResult Details(int id)
        {
            var (audit, findings) = _auditRepository.GetEngagementById(id);
            if (audit == null) return HttpNotFound();

            ViewBag.Findings = findings;
            return View(audit);
        }

        [AuthorizeRole("Administrator", "Auditor")]
        public ActionResult Create()
        {
            return View(new AuditEngagementCreateViewModel
            {
                Departments = _departmentRepository.GetAll(),
                Auditors = _userRepository.GetAll().Where(u => u.RoleName == "Auditor" || u.RoleName == "Administrator").ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrator", "Auditor")]
        public ActionResult Create(AuditEngagementCreateViewModel model)
        {
            if (model.PeriodEnd < model.PeriodStart)
            {
                ModelState.AddModelError(nameof(model.PeriodEnd), "Period end cannot be before period start.");
            }

            if (!ModelState.IsValid)
            {
                model.Departments = _departmentRepository.GetAll();
                model.Auditors = _userRepository.GetAll().Where(u => u.RoleName == "Auditor" || u.RoleName == "Administrator").ToList();
                return View(model);
            }

            var audit = new AuditEngagement
            {
                AuditName = model.AuditName,
                DepartmentId = model.DepartmentId,
                LeadAuditorUserId = model.LeadAuditorUserId,
                PeriodStart = model.PeriodStart,
                PeriodEnd = model.PeriodEnd,
                Scope = model.Scope
            };

            int newId = _auditRepository.CreateEngagement(audit);
            _auditLog.LogCreate("AuditEngagement", newId.ToString(), new { model.AuditName, model.DepartmentId, model.PeriodStart, model.PeriodEnd });

            TempData["Message"] = $"Audit engagement \"{model.AuditName}\" created.";
            return RedirectToAction("Details", new { id = newId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrator", "Auditor")]
        public ActionResult UpdateStatus(int auditId, string newStatus)
        {
            _auditRepository.UpdateEngagementStatus(auditId, newStatus);
            _auditLog.LogUpdate("AuditEngagement", auditId.ToString(), null, new { Status = newStatus });
            return RedirectToAction("Details", new { id = auditId });
        }

        [AuthorizeRole("Administrator", "Auditor")]
        public ActionResult CreateFinding(int auditId)
        {
            return View(new AuditFindingCreateViewModel { AuditId = auditId, Severity = "Medium" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrator", "Auditor")]
        public ActionResult CreateFinding(AuditFindingCreateViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var currentUser = SessionUser.Current;
            var finding = new AuditFinding
            {
                AuditId = model.AuditId,
                TransactionId = model.TransactionId,
                ViolationId = model.ViolationId,
                FindingTitle = model.FindingTitle,
                FindingDescription = model.FindingDescription,
                Severity = model.Severity,
                RaisedByUserId = currentUser.UserId,
                DueDate = model.DueDate
            };

            long newId = _auditRepository.CreateFinding(finding);
            _auditLog.LogCreate("AuditFinding", newId.ToString(), new { model.FindingTitle, model.Severity, model.AuditId });

            return RedirectToAction("Details", new { id = model.AuditId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrator", "Auditor")]
        public ActionResult UpdateFindingStatus(long findingId, int auditId, string newStatus)
        {
            _auditRepository.UpdateFindingStatus(findingId, newStatus);
            _auditLog.LogUpdate("AuditFinding", findingId.ToString(), null, new { Status = newStatus });
            return RedirectToAction("Details", new { id = auditId });
        }
    }
}
