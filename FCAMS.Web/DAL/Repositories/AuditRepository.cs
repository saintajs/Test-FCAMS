using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using FCAMS.Web.Models;

namespace FCAMS.Web.DAL.Repositories
{
    public interface IAuditRepository
    {
        int CreateEngagement(AuditEngagement audit);
        List<AuditEngagement> GetAllEngagements();
        (AuditEngagement Audit, List<AuditFinding> Findings) GetEngagementById(int auditId);
        void UpdateEngagementStatus(int auditId, string newStatus);

        long CreateFinding(AuditFinding finding);
        void UpdateFindingStatus(long findingId, string newStatus);
    }

    public class AuditRepository : IAuditRepository
    {
        public int CreateEngagement(AuditEngagement audit)
        {
            using (var db = new SqlDataAccess())
            {
                var outputParam = new SqlParameter("@NewAuditId", SqlDbType.Int) { Direction = ParameterDirection.Output };
                return db.ExecuteWithOutputParameter<int>("dbo.usp_AuditEngagement_Create", "@NewAuditId",
                    new SqlParameter("@AuditName", audit.AuditName),
                    new SqlParameter("@DepartmentId", audit.DepartmentId),
                    new SqlParameter("@LeadAuditorUserId", audit.LeadAuditorUserId),
                    new SqlParameter("@PeriodStart", audit.PeriodStart),
                    new SqlParameter("@PeriodEnd", audit.PeriodEnd),
                    new SqlParameter("@Scope", (object)audit.Scope ?? DBNull.Value),
                    outputParam);
            }
        }

        public List<AuditEngagement> GetAllEngagements()
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_AuditEngagement_GetAll", reader => new AuditEngagement
                {
                    AuditId = reader.GetInt32(reader.GetOrdinal("AuditId")),
                    AuditName = reader.GetString(reader.GetOrdinal("AuditName")),
                    DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                    LeadAuditorName = reader.GetString(reader.GetOrdinal("LeadAuditorName")),
                    PeriodStart = reader.GetDateTime(reader.GetOrdinal("PeriodStart")),
                    PeriodEnd = reader.GetDateTime(reader.GetOrdinal("PeriodEnd")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                    OpenFindingCount = reader.GetInt32(reader.GetOrdinal("OpenFindingCount"))
                });
            }
        }

        public (AuditEngagement Audit, List<AuditFinding> Findings) GetEngagementById(int auditId)
        {
            AuditEngagement audit = null;
            var findings = new List<AuditFinding>();

            using (var db = new SqlDataAccess())
            {
                db.ExecuteMultiResultReader("dbo.usp_AuditEngagement_GetById",
                    new List<Action<IDataReader>>
                    {
                        reader =>
                        {
                            if (reader.Read())
                            {
                                audit = new AuditEngagement
                                {
                                    AuditId = reader.GetInt32(reader.GetOrdinal("AuditId")),
                                    AuditName = reader.GetString(reader.GetOrdinal("AuditName")),
                                    DepartmentId = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                                    DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                                    LeadAuditorUserId = reader.GetInt32(reader.GetOrdinal("LeadAuditorUserId")),
                                    LeadAuditorName = reader.GetString(reader.GetOrdinal("LeadAuditorName")),
                                    PeriodStart = reader.GetDateTime(reader.GetOrdinal("PeriodStart")),
                                    PeriodEnd = reader.GetDateTime(reader.GetOrdinal("PeriodEnd")),
                                    Status = reader.GetString(reader.GetOrdinal("Status")),
                                    Scope = reader.GetStringOrNull("Scope"),
                                    CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                    CompletedDate = reader.GetDateTimeOrNull("CompletedDate")
                                };
                            }
                        },
                        reader =>
                        {
                            while (reader.Read())
                            {
                                findings.Add(new AuditFinding
                                {
                                    FindingId = reader.GetInt64(reader.GetOrdinal("FindingId")),
                                    FindingTitle = reader.GetString(reader.GetOrdinal("FindingTitle")),
                                    FindingDescription = reader.GetString(reader.GetOrdinal("FindingDescription")),
                                    Severity = reader.GetString(reader.GetOrdinal("Severity")),
                                    Status = reader.GetString(reader.GetOrdinal("Status")),
                                    RaisedDate = reader.GetDateTime(reader.GetOrdinal("RaisedDate")),
                                    DueDate = reader.GetDateTimeOrNull("DueDate"),
                                    ClosedDate = reader.GetDateTimeOrNull("ClosedDate"),
                                    RaisedByName = reader.GetString(reader.GetOrdinal("RaisedByName")),
                                    TransactionId = reader.IsDBNull(reader.GetOrdinal("TransactionId")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("TransactionId")),
                                    ViolationId = reader.IsDBNull(reader.GetOrdinal("ViolationId")) ? (long?)null : reader.GetInt64(reader.GetOrdinal("ViolationId"))
                                });
                            }
                        }
                    },
                    new SqlParameter("@AuditId", auditId));
            }

            return (audit, findings);
        }

        public void UpdateEngagementStatus(int auditId, string newStatus)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery("dbo.usp_AuditEngagement_UpdateStatus",
                    new SqlParameter("@AuditId", auditId),
                    new SqlParameter("@NewStatus", newStatus));
            }
        }

        public long CreateFinding(AuditFinding finding)
        {
            using (var db = new SqlDataAccess())
            {
                var outputParam = new SqlParameter("@NewFindingId", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
                return db.ExecuteWithOutputParameter<long>("dbo.usp_AuditFinding_Create", "@NewFindingId",
                    new SqlParameter("@AuditId", finding.AuditId),
                    new SqlParameter("@TransactionId", (object)finding.TransactionId ?? DBNull.Value),
                    new SqlParameter("@ViolationId", (object)finding.ViolationId ?? DBNull.Value),
                    new SqlParameter("@FindingTitle", finding.FindingTitle),
                    new SqlParameter("@FindingDescription", finding.FindingDescription),
                    new SqlParameter("@Severity", finding.Severity),
                    new SqlParameter("@RaisedByUserId", finding.RaisedByUserId),
                    new SqlParameter("@DueDate", (object)finding.DueDate ?? DBNull.Value),
                    outputParam);
            }
        }

        public void UpdateFindingStatus(long findingId, string newStatus)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery("dbo.usp_AuditFinding_UpdateStatus",
                    new SqlParameter("@FindingId", findingId),
                    new SqlParameter("@NewStatus", newStatus));
            }
        }
    }

    public interface IDepartmentRepository
    {
        List<Department> GetAll();
    }

    public class DepartmentRepository : IDepartmentRepository
    {
        public List<Department> GetAll()
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_Department_GetActive", reader => new Department
                {
                    DepartmentId = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                    DepartmentName = reader.GetString(reader.GetOrdinal("DepartmentName")),
                    CostCenterCode = reader.GetString(reader.GetOrdinal("CostCenterCode")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
        }
    }
}
