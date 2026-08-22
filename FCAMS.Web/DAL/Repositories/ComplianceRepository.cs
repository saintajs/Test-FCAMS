using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using FCAMS.Web.Models;

namespace FCAMS.Web.DAL.Repositories
{
    public interface IComplianceRepository
    {
        List<ComplianceRule> GetActiveRules();
        List<ComplianceRule> GetAllRules();
        void SetRuleActive(int ruleId, bool isActive);

        long CreateViolation(long transactionId, int ruleId, string severity, string detailMessage);
        List<ComplianceViolation> GetOpenViolations(string severity);
        void ResolveViolation(long violationId, string newStatus, int resolvedByUserId, string notes);
    }

    public class ComplianceRepository : IComplianceRepository
    {
        public List<ComplianceRule> GetActiveRules()
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_ComplianceRule_GetActive", MapRule);
            }
        }

        public List<ComplianceRule> GetAllRules()
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_ComplianceRule_GetAll", reader =>
                {
                    var r = MapRule(reader);
                    r.IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));
                    r.CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate"));
                    return r;
                });
            }
        }

        public void SetRuleActive(int ruleId, bool isActive)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery("dbo.usp_ComplianceRule_SetActive",
                    new SqlParameter("@RuleId", ruleId),
                    new SqlParameter("@IsActive", isActive));
            }
        }

        public long CreateViolation(long transactionId, int ruleId, string severity, string detailMessage)
        {
            using (var db = new SqlDataAccess())
            {
                var outputParam = new SqlParameter("@NewViolationId", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
                return db.ExecuteWithOutputParameter<long>("dbo.usp_ComplianceViolation_Create", "@NewViolationId",
                    new SqlParameter("@TransactionId", transactionId),
                    new SqlParameter("@RuleId", ruleId),
                    new SqlParameter("@Severity", severity),
                    new SqlParameter("@DetailMessage", detailMessage),
                    outputParam);
            }
        }

        public List<ComplianceViolation> GetOpenViolations(string severity)
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_ComplianceViolation_GetOpen", reader => new ComplianceViolation
                {
                    ViolationId = reader.GetInt64(reader.GetOrdinal("ViolationId")),
                    TransactionId = reader.GetInt64(reader.GetOrdinal("TransactionId")),
                    TransactionRef = reader.GetString(reader.GetOrdinal("TransactionRef")),
                    Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                    AccountCode = reader.GetString(reader.GetOrdinal("AccountCode")),
                    RuleCode = reader.GetString(reader.GetOrdinal("RuleCode")),
                    RuleName = reader.GetString(reader.GetOrdinal("RuleName")),
                    Severity = reader.GetString(reader.GetOrdinal("Severity")),
                    DetailMessage = reader.GetString(reader.GetOrdinal("DetailMessage")),
                    DetectedDate = reader.GetDateTime(reader.GetOrdinal("DetectedDate")),
                    Status = reader.GetString(reader.GetOrdinal("Status"))
                }, new SqlParameter("@Severity", (object)severity ?? System.DBNull.Value));
            }
        }

        public void ResolveViolation(long violationId, string newStatus, int resolvedByUserId, string notes)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery("dbo.usp_ComplianceViolation_Resolve",
                    new SqlParameter("@ViolationId", violationId),
                    new SqlParameter("@NewStatus", newStatus),
                    new SqlParameter("@ResolvedByUserId", resolvedByUserId),
                    new SqlParameter("@ResolutionNotes", notes));
            }
        }

        private static ComplianceRule MapRule(IDataReader reader)
        {
            return new ComplianceRule
            {
                RuleId = reader.GetInt32(reader.GetOrdinal("RuleId")),
                RuleCode = reader.GetString(reader.GetOrdinal("RuleCode")),
                RuleName = reader.GetString(reader.GetOrdinal("RuleName")),
                Description = reader.GetStringOrNull("Description"),
                RuleType = reader.GetString(reader.GetOrdinal("RuleType")),
                ThresholdValue = reader.IsDBNull(reader.GetOrdinal("ThresholdValue")) ? (decimal?)null : reader.GetDecimal(reader.GetOrdinal("ThresholdValue")),
                ThresholdWindowDays = reader.GetIntOrNull("ThresholdWindowDays"),
                Severity = reader.GetString(reader.GetOrdinal("Severity"))
            };
        }
    }
}
