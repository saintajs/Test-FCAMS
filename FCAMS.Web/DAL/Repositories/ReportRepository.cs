using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using FCAMS.Web.Models;
using FCAMS.Web.Models.ViewModels;

namespace FCAMS.Web.DAL.Repositories
{
    public class ComplianceSummaryRow
    {
        public string RuleCode { get; set; }
        public string RuleName { get; set; }
        public string Severity { get; set; }
        public int ViolationCount { get; set; }
        public decimal TotalAmountAtRisk { get; set; }
        public int OpenCount { get; set; }
        public int ResolvedCount { get; set; }
        public int FalsePositiveCount { get; set; }
    }

    public class AuditTrailRow
    {
        public string Username { get; set; }
        public string ActionType { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string IPAddress { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public interface IReportRepository
    {
        DashboardViewModel GetDashboardSummary();
        List<ComplianceSummaryRow> GetComplianceSummary(DateTime dateFrom, DateTime dateTo);
        List<AuditTrailRow> GetAuditTrail(DateTime dateFrom, DateTime dateTo);
        void LogGeneratedReport(string reportName, string reportType, string parametersJson, int generatedByUserId, string outputFormat, string filePath);
    }

    public class ReportRepository : IReportRepository
    {
        public DashboardViewModel GetDashboardSummary()
        {
            var model = new DashboardViewModel();

            using (var db = new SqlDataAccess())
            {
                db.ExecuteMultiResultReader("dbo.usp_Dashboard_GetSummary", new List<Action<IDataReader>>
                {
                    reader =>
                    {
                        if (reader.Read())
                        {
                            model.TransactionsLast30Days = reader.GetInt32(reader.GetOrdinal("TransactionsLast30Days"));
                            model.OpenViolations = reader.GetInt32(reader.GetOrdinal("OpenViolations"));
                            model.CriticalOpenViolations = reader.GetInt32(reader.GetOrdinal("CriticalOpenViolations"));
                            model.ActiveAudits = reader.GetInt32(reader.GetOrdinal("ActiveAudits"));
                            model.OpenFindings = reader.GetInt32(reader.GetOrdinal("OpenFindings"));
                        }
                    },
                    reader =>
                    {
                        while (reader.Read())
                        {
                            model.ViolationsBySeverity[reader.GetString(reader.GetOrdinal("Severity"))] =
                                reader.GetInt32(reader.GetOrdinal("Count"));
                        }
                    },
                    reader =>
                    {
                        while (reader.Read())
                        {
                            model.RecentActivity.Add(new AuditLogEntry
                            {
                                AuditLogId = reader.GetInt64(reader.GetOrdinal("AuditLogId")),
                                Username = reader.GetStringOrNull("Username"),
                                ActionType = reader.GetString(reader.GetOrdinal("ActionType")),
                                EntityName = reader.GetString(reader.GetOrdinal("EntityName")),
                                EntityId = reader.GetStringOrNull("EntityId"),
                                Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp"))
                            });
                        }
                    }
                });
            }

            return model;
        }

        public List<ComplianceSummaryRow> GetComplianceSummary(DateTime dateFrom, DateTime dateTo)
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_Report_ComplianceSummary", reader => new ComplianceSummaryRow
                {
                    RuleCode = reader.GetString(reader.GetOrdinal("RuleCode")),
                    RuleName = reader.GetString(reader.GetOrdinal("RuleName")),
                    Severity = reader.GetString(reader.GetOrdinal("Severity")),
                    ViolationCount = reader.GetInt32(reader.GetOrdinal("ViolationCount")),
                    TotalAmountAtRisk = reader.GetDecimal(reader.GetOrdinal("TotalAmountAtRisk")),
                    OpenCount = reader.GetInt32(reader.GetOrdinal("OpenCount")),
                    ResolvedCount = reader.GetInt32(reader.GetOrdinal("ResolvedCount")),
                    FalsePositiveCount = reader.GetInt32(reader.GetOrdinal("FalsePositiveCount"))
                }, new SqlParameter("@DateFrom", dateFrom), new SqlParameter("@DateTo", dateTo));
            }
        }

        public List<AuditTrailRow> GetAuditTrail(DateTime dateFrom, DateTime dateTo)
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_Report_AuditTrail", reader => new AuditTrailRow
                {
                    Username = reader.GetStringOrNull("Username"),
                    ActionType = reader.GetString(reader.GetOrdinal("ActionType")),
                    EntityName = reader.GetString(reader.GetOrdinal("EntityName")),
                    EntityId = reader.GetStringOrNull("EntityId"),
                    IPAddress = reader.GetStringOrNull("IPAddress"),
                    Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp"))
                }, new SqlParameter("@DateFrom", dateFrom), new SqlParameter("@DateTo", dateTo));
            }
        }

        public void LogGeneratedReport(string reportName, string reportType, string parametersJson, int generatedByUserId, string outputFormat, string filePath)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery("dbo.usp_Report_LogGenerated",
                    new SqlParameter("@ReportName", reportName),
                    new SqlParameter("@ReportType", reportType),
                    new SqlParameter("@ParametersJson", (object)parametersJson ?? DBNull.Value),
                    new SqlParameter("@GeneratedByUserId", generatedByUserId),
                    new SqlParameter("@OutputFormat", outputFormat),
                    new SqlParameter("@FilePath", (object)filePath ?? DBNull.Value));
            }
        }
    }
}
