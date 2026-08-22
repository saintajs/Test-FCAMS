using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FCAMS.Web.Models;

namespace FCAMS.Web.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class TransactionCreateViewModel
    {
        [Required, StringLength(30)]
        [Display(Name = "Transaction Reference")]
        public string TransactionRef { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Transaction Date")]
        public DateTime TransactionDate { get; set; } = DateTime.Today;

        [Required]
        [Range(typeof(decimal), "-999999999.99", "999999999.99", ErrorMessage = "Enter a valid, non-zero amount.")]
        public decimal Amount { get; set; }

        [Required, StringLength(3, MinimumLength = 3)]
        [Display(Name = "Currency")]
        public string CurrencyCode { get; set; } = "USD";

        [Required, StringLength(20)]
        [Display(Name = "GL Account Code")]
        public string AccountCode { get; set; }

        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [StringLength(200)]
        [Display(Name = "Counterparty")]
        public string CounterpartyName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public List<Department> Departments { get; set; } = new List<Department>();
    }

    public class TransactionListFilterViewModel
    {
        public int? DepartmentId { get; set; }
        public string Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;

        public List<Department> Departments { get; set; } = new List<Department>();
        public PagedResult<FinancialTransaction> Results { get; set; }
    }

    public class ViolationResolveViewModel
    {
        public long ViolationId { get; set; }
        public string TransactionRef { get; set; }
        public string RuleName { get; set; }
        public string DetailMessage { get; set; }

        [Required(ErrorMessage = "Choose a resolution outcome.")]
        [Display(Name = "Resolution")]
        public string NewStatus { get; set; } // Resolved | FalsePositive

        [Required(ErrorMessage = "Resolution notes are required for the audit trail.")]
        [StringLength(1000)]
        [Display(Name = "Notes")]
        public string ResolutionNotes { get; set; }
    }

    public class AuditEngagementCreateViewModel
    {
        [Required, StringLength(200)]
        [Display(Name = "Audit Name")]
        public string AuditName { get; set; }

        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [Required]
        [Display(Name = "Lead Auditor")]
        public int LeadAuditorUserId { get; set; }

        [Required, DataType(DataType.Date)]
        [Display(Name = "Period Start")]
        public DateTime PeriodStart { get; set; }

        [Required, DataType(DataType.Date)]
        [Display(Name = "Period End")]
        public DateTime PeriodEnd { get; set; }

        [StringLength(1000)]
        public string Scope { get; set; }

        public List<Department> Departments { get; set; } = new List<Department>();
        public List<User> Auditors { get; set; } = new List<User>();
    }

    public class AuditFindingCreateViewModel
    {
        public int AuditId { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "Finding Title")]
        public string FindingTitle { get; set; }

        [Required, StringLength(2000)]
        [Display(Name = "Description")]
        public string FindingDescription { get; set; }

        [Required]
        public string Severity { get; set; }

        public long? TransactionId { get; set; }
        public long? ViolationId { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }
    }

    public class DashboardViewModel
    {
        public int TransactionsLast30Days { get; set; }
        public int OpenViolations { get; set; }
        public int CriticalOpenViolations { get; set; }
        public int ActiveAudits { get; set; }
        public int OpenFindings { get; set; }
        public Dictionary<string, int> ViolationsBySeverity { get; set; } = new Dictionary<string, int>();
        public List<AuditLogEntry> RecentActivity { get; set; } = new List<AuditLogEntry>();
    }

    public class ReportParametersViewModel
    {
        [Required, DataType(DataType.Date)]
        [Display(Name = "From")]
        public DateTime DateFrom { get; set; } = DateTime.Today.AddDays(-30);

        [Required, DataType(DataType.Date)]
        [Display(Name = "To")]
        public DateTime DateTo { get; set; } = DateTime.Today;
    }

    public class UserCreateViewModel
    {
        [Required, StringLength(50)]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required, StringLength(150), EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required, StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required]
        [Display(Name = "Role")]
        public int RoleId { get; set; }

        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }

        public List<Department> Departments { get; set; } = new List<Department>();
    }
    
    public class UserEditViewModel
    {
        public int UserId { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required, EmailAddress, StringLength(150)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string Password { get; set; }

        [Required]
        [Display(Name = "Role")]
        public int RoleId { get; set; }

        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        public List<Department> Departments { get; set; }
            = new List<Department>();
    }
}

