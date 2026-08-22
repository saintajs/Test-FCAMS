using System;
using System.Collections.Generic;

namespace FCAMS.Web.Models
{
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
    }

    public class Department
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string CostCenterCode { get; set; }
        public bool IsActive { get; set; }
    }

    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public bool IsActive { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LockedOutUntil { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }

    public class FinancialTransaction
    {
        public long TransactionId { get; set; }
        public string TransactionRef { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime PostedDate { get; set; }
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; }
        public string AccountCode { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string CounterpartyName { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class ComplianceRule
    {
        public int RuleId { get; set; }
        public string RuleCode { get; set; }
        public string RuleName { get; set; }
        public string Description { get; set; }
        public string RuleType { get; set; }
        public decimal? ThresholdValue { get; set; }
        public int? ThresholdWindowDays { get; set; }
        public string Severity { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class ComplianceViolation
    {
        public long ViolationId { get; set; }
        public long TransactionId { get; set; }
        public string TransactionRef { get; set; }
        public decimal Amount { get; set; }
        public string AccountCode { get; set; }
        public int RuleId { get; set; }
        public string RuleCode { get; set; }
        public string RuleName { get; set; }
        public DateTime DetectedDate { get; set; }
        public string Severity { get; set; }
        public string DetailMessage { get; set; }
        public string Status { get; set; }
        public int? ResolvedByUserId { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string ResolutionNotes { get; set; }
    }

    public class AuditEngagement
    {
        public int AuditId { get; set; }
        public string AuditName { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public int LeadAuditorUserId { get; set; }
        public string LeadAuditorName { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string Status { get; set; }
        public string Scope { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public int OpenFindingCount { get; set; }
    }

    public class AuditFinding
    {
        public long FindingId { get; set; }
        public int AuditId { get; set; }
        public long? TransactionId { get; set; }
        public long? ViolationId { get; set; }
        public string FindingTitle { get; set; }
        public string FindingDescription { get; set; }
        public string Severity { get; set; }
        public string Status { get; set; }
        public int RaisedByUserId { get; set; }
        public string RaisedByName { get; set; }
        public DateTime RaisedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? ClosedDate { get; set; }
    }

    public class AuditLogEntry
    {
        public long AuditLogId { get; set; }
        public int? UserId { get; set; }
        public string Username { get; set; }
        public string ActionType { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string OldValues { get; set; }
        public string NewValues { get; set; }
        public string IPAddress { get; set; }
        public string UserAgent { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ExceptionLogEntry
    {
        public long ExceptionLogId { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
        public string ExceptionType { get; set; }
        public string StackTrace { get; set; }
        public string InnerException { get; set; }
        public string Severity { get; set; }
        public string RequestUrl { get; set; }
        public int? UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsResolved { get; set; }
    }

    /// <summary>Generic paged result wrapper returned by any *_GetPaged repository method.</summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalRowCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalRowCount / (double)PageSize);
    }
}
