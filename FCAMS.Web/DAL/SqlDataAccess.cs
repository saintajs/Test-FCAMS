using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace FCAMS.Web.DAL
{
    /// <summary>
    /// Thin, explicit ADO.NET wrapper used by every repository in the app.
    /// FCAMS deliberately does not use an ORM: every database call maps to a
    /// named stored procedure, connections are opened, used, and disposed
    /// within a single method (never cached across requests), and
    /// parameters are always passed as SqlParameter objects -- never
    /// string-concatenated -- which is what makes the whole codebase immune
    /// to SQL injection by construction rather than by convention.
    /// </summary>
    public class SqlDataAccess : IDisposable
    {
        private readonly string _connectionString;

        public SqlDataAccess()
            : this(ConfigurationManager.ConnectionStrings["FcamsDbContext"].ConnectionString)
        {
        }

        public SqlDataAccess(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("FcamsDbContext connection string is not configured.");
            _connectionString = connectionString;
        }

        /// <summary>
        /// Executes a stored procedure that returns no result set (INSERT/UPDATE/DELETE)
        /// and returns the number of rows affected.
        /// </summary>
        public int ExecuteNonQuery(string storedProcedure, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = CreateCommand(connection, storedProcedure, parameters))
            {
                connection.Open();
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes a stored procedure and maps each row of the first result
        /// set into a T via the supplied mapper delegate.
        /// </summary>
        public List<T> ExecuteReader<T>(string storedProcedure, Func<IDataReader, T> map, params SqlParameter[] parameters)
        {
            var results = new List<T>();
            using (var connection = new SqlConnection(_connectionString))
            using (var command = CreateCommand(connection, storedProcedure, parameters))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(map(reader));
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Executes a stored procedure that returns multiple result sets
        /// (e.g. usp_Dashboard_GetSummary, usp_AuditEngagement_GetById),
        /// invoking one mapper delegate per result set, in order.
        /// </summary>
        public void ExecuteMultiResultReader(string storedProcedure, IEnumerable<Action<IDataReader>> resultSetHandlers, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = CreateCommand(connection, storedProcedure, parameters))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    foreach (var handler in resultSetHandlers)
                    {
                        handler(reader);
                        reader.NextResult();
                    }
                }
            }
        }

        /// <summary>
        /// Executes a stored procedure and returns a single scalar value
        /// (used for lookups like a COUNT or a single computed value).
        /// </summary>
        public T ExecuteScalar<T>(string storedProcedure, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = CreateCommand(connection, storedProcedure, parameters))
            {
                connection.Open();
                var result = command.ExecuteScalar();
                if (result == null || result == DBNull.Value) return default(T);
                return (T)Convert.ChangeType(result, typeof(T));
            }
        }

        /// <summary>
        /// Executes a stored procedure that has one or more OUTPUT
        /// parameters (e.g. usp_Transaction_Create's @NewTransactionId) and
        /// returns the requested output parameter's value.
        /// </summary>
        public T ExecuteWithOutputParameter<T>(string storedProcedure, string outputParameterName, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = CreateCommand(connection, storedProcedure, parameters))
            {
                connection.Open();
                command.ExecuteNonQuery();
                var outParam = command.Parameters[outputParameterName];
                if (outParam.Value == null || outParam.Value == DBNull.Value) return default(T);
                return (T)Convert.ChangeType(outParam.Value, typeof(T));
            }
        }

        private static SqlCommand CreateCommand(SqlConnection connection, string storedProcedure, SqlParameter[] parameters)
        {
            var command = new SqlCommand(storedProcedure, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 120
            };
            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }
            return command;
        }

        public void Dispose()
        {
            // No persistent state to release; connections are scoped per
            // call above. Present for symmetry / future pooling hooks.
        }
    }

    /// <summary>Small helpers for reading nullable columns from an IDataReader without boilerplate at every call site.</summary>
    public static class DataReaderExtensions
    {
        public static T GetValueOrDefault<T>(this IDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal)) return default(T);
            return (T)reader.GetValue(ordinal);
        }

        public static string GetStringOrNull(this IDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        public static DateTime? GetDateTimeOrNull(this IDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? (DateTime?)null : reader.GetDateTime(ordinal);
        }

        public static int? GetIntOrNull(this IDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? (int?)null : reader.GetInt32(ordinal);
        }
    }
}
