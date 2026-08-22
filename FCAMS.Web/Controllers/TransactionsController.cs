using System;
using System.Linq;
using System.Web.Mvc;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Infrastructure;
using FCAMS.Web.Models;
using FCAMS.Web.Models.ViewModels;
using FCAMS.Web.Services;

namespace FCAMS.Web.Controllers
{
    public class TransactionsController : Controller
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IComplianceRuleEngine _ruleEngine;
        private readonly IAuditLogService _auditLog;

        public TransactionsController()
            : this(new TransactionRepository(), new DepartmentRepository(),
                   new ComplianceRuleEngine(new ComplianceRepository(), new TransactionRepository()),
                   new AuditLogService())
        {
        }

        public TransactionsController(ITransactionRepository transactionRepository, IDepartmentRepository departmentRepository,
            IComplianceRuleEngine ruleEngine, IAuditLogService auditLog)
        {
            _transactionRepository = transactionRepository;
            _departmentRepository = departmentRepository;
            _ruleEngine = ruleEngine;
            _auditLog = auditLog;
        }

        // GET: Transactions?departmentId=&status=&dateFrom=&dateTo=&pageNumber=
        [AuditableAction("View", "FinancialTransaction")]
        public ActionResult Index(TransactionListFilterViewModel filter)
        {
            filter = filter ?? new TransactionListFilterViewModel();
            if (filter.PageSize <= 0) filter.PageSize = 25;
            if (filter.PageNumber <= 0) filter.PageNumber = 1;

            filter.Departments = _departmentRepository.GetAll();
            filter.Results = _transactionRepository.GetPaged(
                filter.DepartmentId, filter.Status, filter.DateFrom, filter.DateTo, filter.PageNumber, filter.PageSize);

            return View(filter);
        }

        [AuthorizeRole("Administrator", "ComplianceOfficer", "Auditor")]
        public ActionResult Details(long id)
        {
            var transaction = _transactionRepository.GetById(id);
            if (transaction == null) return HttpNotFound();
            return View(transaction);
        }

        [AuthorizeRole("Administrator", "ComplianceOfficer")]
        public ActionResult Create()
        {
            return View(new TransactionCreateViewModel { Departments = _departmentRepository.GetAll() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRole("Administrator", "ComplianceOfficer")]
        public ActionResult Create(TransactionCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Departments = _departmentRepository.GetAll();
                return View(model);
            }

            var currentUser = SessionUser.Current;
            var transaction = new FinancialTransaction
            {
                TransactionRef = model.TransactionRef.Trim(),
                TransactionDate = model.TransactionDate,
                Amount = model.Amount,
                CurrencyCode = model.CurrencyCode.ToUpperInvariant(),
                AccountCode = model.AccountCode.Trim(),
                DepartmentId = model.DepartmentId,
                CounterpartyName = model.CounterpartyName,
                Description = model.Description,
                CreatedByUserId = currentUser.UserId
            };

            long newId;
            try
            {
                newId = _transactionRepository.Create(transaction);
            }
            catch (System.Data.SqlClient.SqlException ex) when (ex.Message.Contains("already taken") || ex.Number == 2627)
            {
                ModelState.AddModelError(nameof(model.TransactionRef), "This transaction reference already exists.");
                model.Departments = _departmentRepository.GetAll();
                return View(model);
            }

            transaction.TransactionId = newId;
            transaction.CreatedByUserId = currentUser.UserId;
            transaction.CreatedDate = DateTime.UtcNow;
            transaction.Status = "Pending";

            _auditLog.LogCreate("FinancialTransaction", newId.ToString(), new
            {
                transaction.TransactionRef,
                transaction.Amount,
                transaction.AccountCode,
                transaction.DepartmentId
            });

            // Immediately run the compliance rule engine against the new
            // transaction so violations surface the moment it's recorded,
            // rather than waiting for a batch job.
            var evaluationResults = _ruleEngine.EvaluateTransaction(transaction);
            int violationCount = evaluationResults.Count(r => r.IsViolation);

            TempData["Message"] = violationCount > 0
                ? $"Transaction {transaction.TransactionRef} was recorded and flagged for {violationCount} compliance violation(s). It has been routed to Compliance for review."
                : $"Transaction {transaction.TransactionRef} was recorded successfully with no compliance violations detected.";

            return RedirectToAction("Details", new { id = newId });
        }
    }
}
