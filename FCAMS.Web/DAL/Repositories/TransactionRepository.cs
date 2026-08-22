using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using FCAMS.Web.Models;

namespace FCAMS.Web.DAL.Repositories
{
    public interface ITransactionRepository
    {
        long Create(FinancialTransaction txn);
        FinancialTransaction GetById(long transactionId);
        PagedResult<FinancialTransaction> GetPaged(int? departmentId, string status, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize);
        List<(long TransactionId, string TransactionRef, DateTime TransactionDate, decimal Amount)> FindDuplicateCandidates(long transactionId, string accountCode, decimal amount, DateTime transactionDate, int windowDays);
        int CountRecentByAccount(string accountCode, DateTime transactionDate, int windowDays);
        void UpdateStatus(long transactionId, string newStatus);
    }

    public class TransactionRepository : ITransactionRepository
    {
        public long Create(FinancialTransaction txn)
        {
            using (var db = new SqlDataAccess())
            {
                var outputParam = new SqlParameter("@NewTransactionId", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
                return db.ExecuteWithOutputParameter<long>("dbo.usp_Transaction_Create", "@NewTransactionId",
                    new SqlParameter("@TransactionRef", txn.TransactionRef),
                    new SqlParameter("@TransactionDate", txn.TransactionDate),
                    new SqlParameter("@Amount", txn.Amount),
                    new SqlParameter("@CurrencyCode", txn.CurrencyCode),
                    new SqlParameter("@AccountCode", txn.AccountCode),
                    new SqlParameter("@DepartmentId", txn.DepartmentId),
                    new SqlParameter("@CounterpartyName", (object)txn.CounterpartyName ?? DBNull.Value),
                    new SqlParameter("@Description", (object)txn.Description ?? DBNull.Value),
                    new SqlParameter("@CreatedByUserId", txn.CreatedByUserId),
                    outputParam);
            }
        }

        public FinancialTransaction GetById(long transactionId)
        {
            using (var db = new SqlDataAccess())
            {
                var results = db.ExecuteReader("dbo.usp_Transaction_GetById", MapTransaction,
                    new SqlParameter("@TransactionId", transactionId));
                return results.Count > 0 ? results[0] : null;
            }
        }

        public PagedResult<FinancialTransaction> GetPaged(int? departmentId, string status, DateTime? dateFrom, DateTime? dateTo, int pageNumber, int pageSize)
        {
            var items = new List<FinancialTransaction>();
            int totalRowCount = 0;

            using (var db = new SqlDataAccess())
            {
                items = db.ExecuteReader("dbo.usp_Transaction_GetPaged", reader =>
                {
                    var t = MapTransaction(reader);
                    totalRowCount = reader.GetInt32(reader.GetOrdinal("TotalRowCount"));
                    return t;
                },
                new SqlParameter("@DepartmentId", (object)departmentId ?? DBNull.Value),
                new SqlParameter("@Status", (object)status ?? DBNull.Value),
                new SqlParameter("@DateFrom", (object)dateFrom ?? DBNull.Value),
                new SqlParameter("@DateTo", (object)dateTo ?? DBNull.Value),
                new SqlParameter("@PageNumber", pageNumber),
                new SqlParameter("@PageSize", pageSize));
            }

            return new PagedResult<FinancialTransaction>
            {
                Items = items,
                TotalRowCount = totalRowCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public List<(long TransactionId, string TransactionRef, DateTime TransactionDate, decimal Amount)> FindDuplicateCandidates(
            long transactionId, string accountCode, decimal amount, DateTime transactionDate, int windowDays)
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteReader("dbo.usp_Transaction_FindDuplicateCandidates", reader =>
                    (
                        reader.GetInt64(reader.GetOrdinal("TransactionId")),
                        reader.GetString(reader.GetOrdinal("TransactionRef")),
                        reader.GetDateTime(reader.GetOrdinal("TransactionDate")),
                        reader.GetDecimal(reader.GetOrdinal("Amount"))
                    ),
                    new SqlParameter("@TransactionId", transactionId),
                    new SqlParameter("@AccountCode", accountCode),
                    new SqlParameter("@Amount", amount),
                    new SqlParameter("@TransactionDate", transactionDate),
                    new SqlParameter("@WindowDays", windowDays));
            }
        }

        public int CountRecentByAccount(string accountCode, DateTime transactionDate, int windowDays)
        {
            using (var db = new SqlDataAccess())
            {
                return db.ExecuteScalar<int>("dbo.usp_Transaction_CountRecentByAccount",
                    new SqlParameter("@AccountCode", accountCode),
                    new SqlParameter("@TransactionDate", transactionDate),
                    new SqlParameter("@WindowDays", windowDays));
            }
        }

        public void UpdateStatus(long transactionId, string newStatus)
        {
            using (var db = new SqlDataAccess())
            {
                db.ExecuteNonQuery("dbo.usp_Transaction_UpdateStatus",
                    new SqlParameter("@TransactionId", transactionId),
                    new SqlParameter("@NewStatus", newStatus));
            }
        }

        private static FinancialTransaction MapTransaction(IDataReader reader)
        {
            return new FinancialTransaction
            {
                TransactionId = reader.GetInt64(reader.GetOrdinal("TransactionId")),
                TransactionRef = reader.GetString(reader.GetOrdinal("TransactionRef")),
                TransactionDate = reader.GetDateTime(reader.GetOrdinal("TransactionDate")),
                Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                CurrencyCode = reader.GetString(reader.GetOrdinal("CurrencyCode")),
                AccountCode = reader.GetString(reader.GetOrdinal("AccountCode")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                CounterpartyName = reader.GetStringOrNull("CounterpartyName"),
                Description = reader.GetStringOrNull("Description"),
                DepartmentName = HasColumn(reader, "DepartmentName") ? reader.GetStringOrNull("DepartmentName") : null,
                CreatedByName = HasColumn(reader, "CreatedByName") ? reader.GetStringOrNull("CreatedByName") : null,
                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate"))
            };
        }

        private static bool HasColumn(IDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
