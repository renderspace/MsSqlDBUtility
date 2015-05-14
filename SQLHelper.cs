//===============================================================================
// This file is based on the Microsoft Data Access Application Block for .NET
// For more information please go to 
// http://msdn.microsoft.com/library/en-us/dnbda/html/daab-rm.asp
//===============================================================================

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Collections;
using System.Security;
using System.Web.Caching;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using System.Data.Common;

// only include security rules stuff if in 4.0
#if _NET_L_T_4_0
#else
[assembly: SecurityRules(SecurityRuleSet.Level1)] 
#endif

namespace MsSqlDBUtility
{
    public abstract class SqlHelper
    {
        //Database connection strings
        public static readonly string ConnStringMain = ConfigurationManager.ConnectionStrings["MsSqlConnectionString"].ConnectionString;
        public static readonly string ConnStringMainCustom = ConfigurationManager.ConnectionStrings["MsSqlConnectionStringCustom"].ConnectionString;
		
		// Hashtable to store cached parameters
		private static readonly Hashtable parmCache = Hashtable.Synchronized(new Hashtable());


        public static string PrepareIntListForParameter(List<int> list)
        {
            string categoryIds = string.Empty;
            if (list != null)
                foreach (int cat in list)
                    categoryIds += cat + " ";

            return categoryIds;
        }

        public static SqlParameter GetNullable(string name, object value) 
        {
            if (value == null)
                return new SqlParameter(name, DBNull.Value);
            else if (value.GetType() == typeof(System.Net.IPAddress))
                return new SqlParameter(name, value.ToString());
            else if (value.GetType() == typeof(string) && string.IsNullOrEmpty((string)value))
                return new SqlParameter(name, DBNull.Value);
            else if (value.GetType() == typeof(bool?) && !((bool?)value).HasValue)
                return new SqlParameter(name, DBNull.Value);				
            else
                return new SqlParameter(name, value);
        }

        public static SqlParameter GetNullableSortFieldParameter(string value) 
        {
            return (value != null && value.Length > 1) ? new SqlParameter("@sortByColumn", value) : new SqlParameter("@sortByColumn", DBNull.Value);
        }

        /// <summary>
		/// Execute a SqlCommand (that returns no resultset) against the database specified in the connection string 
		/// using the provided parameters.
		/// </summary>
		/// <remarks>
		/// e.g.:  
		///  int result = ExecuteNonQuery(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
		/// </remarks>
        /// <param name="connString">a valid connection string for a SqlConnection</param>
        /// <param name="cmdType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="cmdText">the stored procedure name or T-SQL command</param>
        /// <param name="cmdParms">an array of SqlParamters used to execute the command</param>
		/// <returns>an int representing the number of rows affected by the command</returns>
		public static int ExecuteNonQuery(string connString, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms) 
        {
            // var retryStrategy = new Incremental(5, TimeSpan.FromSeconds(1),   TimeSpan.FromSeconds(2));
            // var retryPolicy = new RetryPolicy<SqlDatabaseTransientErrorDetectionStrategy>(retryStrategy);

            using (var conn = new SqlConnection(connString)) 
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                    int val = cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                    return val;
                }
			}
		}

		/// <summary>
		/// Execute a SqlCommand (that returns no resultset) against an existing database connection 
		/// using the provided parameters.
		/// </summary>
		/// <remarks>
		/// e.g.:  
		///  int result = ExecuteNonQuery(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
		/// </remarks>
		/// <param name="conn">an existing database connection</param>
        /// <param name="cmdType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="cmdText">the stored procedure name or T-SQL command</param>
        /// <param name="cmdParms">an array of SqlParamters used to execute the command</param>
		/// <returns>an int representing the number of rows affected by the command</returns>
		public static int ExecuteNonQuery(SqlConnection conn, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms) 
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                int val = cmd.ExecuteNonQuery();
                cmd.Parameters.Clear();
                return val;
            }
		}

		/// <summary>
		/// Execute a SqlCommand (that returns no resultset) using an existing SQL Transaction 
		/// using the provided parameters.
		/// </summary>
		/// <remarks>
		/// e.g.:  
		///  int result = ExecuteNonQuery(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
		/// </remarks>
		/// <param name="trans">an existing sql transaction</param>
        /// <param name="cmdType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="cmdText">the stored procedure name or T-SQL command</param>
        /// <param name="cmdParms">an array of SqlParamters used to execute the command</param>
		/// <returns>an int representing the number of rows affected by the command</returns>
		public static int ExecuteNonQuery(SqlTransaction trans, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms) 
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                PrepareCommand(cmd, trans.Connection, trans, cmdType, cmdText, cmdParms);
                int val = cmd.ExecuteNonQuery();
                cmd.Parameters.Clear();
                return val;
            }
		}

		/// <summary>
		/// Execute a SqlCommand that returns a resultset against the database specified in the connection string 
		/// using the provided parameters.
		/// </summary>
		/// <remarks>
		/// e.g.:  
		///  SqlDataReader r = ExecuteReader(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
		/// </remarks>
        /// <param name="connString">an existing database connection</param>
        /// <param name="cmdType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="cmdText">the stored procedure name or T-SQL command</param>
        /// <param name="cmdParms">an array of SqlParamters used to execute the command</param>
		/// <returns>A SqlDataReader containing the results</returns>
        public static SqlDataReader ExecuteReader(string connString, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            SqlConnection conn = new SqlConnection(connString);
            SqlCommand cmd = new SqlCommand();

            // we use a try/catch here because if the method throws an exception we want to 
            // close the connection throw code, because no datareader will exist, hence the 
            // commandBehaviour.CloseConnection will not work
            try
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                cmd.Parameters.Clear();
                return rdr;
            }
            catch
            {
                conn.Close();
                throw;
            }
        }

		/// <summary>
		/// Execute a SqlCommand that returns the first column of the first record against the database specified in the connection string 
		/// using the provided parameters.
		/// </summary>
		/// <remarks>
		/// e.g.:  
		///  Object obj = ExecuteScalar(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
		/// </remarks>
        /// <param name="connString">an existing database connection</param>
        /// <param name="cmdType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="cmdText">the stored procedure name or T-SQL command</param>
        /// <param name="cmdParms">an array of SqlParamters used to execute the command</param>
		/// <returns>An object that should be converted to the expected type using Convert.To{Type}</returns>
		public static object ExecuteScalar(string connString, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms) 
        {
			using (SqlConnection conn = new SqlConnection(connString)) 
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                    object val = cmd.ExecuteScalar();
                    cmd.Parameters.Clear();
                    return val;
                }
			}
		}

		/// <summary>
		/// Execute a SqlCommand that returns the first column of the first record against an existing database connection 
		/// using the provided parameters.
		/// </summary>
		/// <remarks>
		/// e.g.:  
		///  Object obj = ExecuteScalar(connString, CommandType.StoredProcedure, "PublishOrders", new SqlParameter("@prodid", 24));
		/// </remarks>
		/// <param name="conn">an existing database connection</param>
        /// <param name="cmdType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="cmdText">the stored procedure name or T-SQL command</param>
        /// <param name="cmdParms">an array of SqlParamters used to execute the command</param>
		/// <returns>An object that should be converted to the expected type using Convert.To{Type}</returns>
		public static object ExecuteScalar(SqlConnection conn, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms) 
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                PrepareCommand(cmd, conn, null, cmdType, cmdText, cmdParms);
                object val = cmd.ExecuteScalar();
                cmd.Parameters.Clear();
                return val;
            }
		}

        public static object ExecuteScalar(SqlTransaction trans, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                PrepareCommand(cmd, trans.Connection, trans, cmdType, cmdText, cmdParms);
                object val = cmd.ExecuteScalar();
                cmd.Parameters.Clear();
                return val;
            }
        }

        public static DataSet ExecuteDataset(string connString, CommandType commandType, string commandText, params SqlParameter[] commandParameters)
        {
            using (SqlConnection conn = new SqlConnection(connString))
            {
                using (var cmd = new SqlCommand())
                {
                    PrepareCommand(cmd, conn, null, commandType, commandText, commandParameters);

                    using (var da = new SqlDataAdapter(cmd))
                    {
                        var ds = new DataSet();
                        da.Fill(ds);
                        cmd.Parameters.Clear();
                        return ds;
                    }
                }
            }
        }

        /// <summary>
        /// Execute a SqlCommand (that returns a resultset) against the specified SqlConnection 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(conn, CommandType.StoredProcedure, "GetOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connection">A valid SqlConnection</param>
        /// <param name="commandType">The CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">The stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">An array of SqlParamters used to execute the command</param>
        /// <returns>A dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(SqlConnection connection, CommandType commandType, string commandText, params SqlParameter[] commandParameters)
        {
            if (connection == null) throw new ArgumentNullException("connection");

            using (var cmd = new SqlCommand())
            {
                PrepareCommand(cmd, connection, null, commandType, commandText, commandParameters);

                using (var da = new SqlDataAdapter(cmd))
                {
                    var ds = new DataSet();
                    da.Fill(ds);
                    cmd.Parameters.Clear();
                    return ds;
                }
            }
        }

        /// <summary>
        /// Execute a SqlCommand (that returns a resultset) against the specified SqlTransaction
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(trans, CommandType.StoredProcedure, "GetOrders", new SqlParameter("@prodid", 24));
        /// </remarks>
        /// <param name="transaction">A valid SqlTransaction</param>
        /// <param name="commandType">The CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">The stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">An array of SqlParamters used to execute the command</param>
        /// <returns>A dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(SqlTransaction transaction, CommandType commandType, string commandText, params SqlParameter[] commandParameters)
        {
            if (transaction == null) throw new ArgumentNullException("transaction");
            if (transaction != null && transaction.Connection == null) throw new ArgumentException("The transaction was rollbacked or commited, please provide an open transaction.", "transaction");

            // Create a command and prepare it for execution
            using (var cmd = new SqlCommand())
            {
                PrepareCommand(cmd, transaction.Connection, transaction, commandType, commandText, commandParameters);
                using (var da = new SqlDataAdapter(cmd))
                {
                    var ds = new DataSet();
                    da.Fill(ds);
                    cmd.Parameters.Clear();
                    return ds;
                }
            }
        }

		/// <summary>
		/// add parameter array to the cache
		/// </summary>
		/// <param name="cacheKey">Key to the parameter cache</param>
		/// <param name="cmdParms">an array of SqlParamters to be cached</param>
		public static void CacheParameters(string cacheKey, params SqlParameter[] cmdParms) {
			parmCache[cacheKey] = cmdParms;
		}

		/// <summary>
		/// Retrieve cached parameters
		/// </summary>
		/// <param name="cacheKey">key used to lookup parameters</param>
		/// <returns>Cached SqlParamters array</returns>
		public static SqlParameter[] GetCachedParameters(string cacheKey) {
			SqlParameter[] cachedParms = (SqlParameter[])parmCache[cacheKey];
			
			if (cachedParms == null)
				return null;
			
			SqlParameter[] clonedParms = new SqlParameter[cachedParms.Length];

			for (int i = 0, j = cachedParms.Length; i < j; i++)
				clonedParms[i] = (SqlParameter)((ICloneable)cachedParms[i]).Clone();

			return clonedParms;
		}

		/// <summary>
		/// Prepare a command for execution
		/// </summary>
		/// <param name="cmd">SqlCommand object</param>
		/// <param name="conn">SqlConnection object</param>
		/// <param name="trans">SqlTransaction object</param>
		/// <param name="cmdType">Cmd type e.g. stored procedure or text</param>
		/// <param name="cmdText">Command text, e.g. Select * from Products</param>
		/// <param name="cmdParms">SqlParameters to use in the command</param>
        private static void PrepareCommand(SqlCommand cmd, SqlConnection conn, SqlTransaction trans, CommandType cmdType, string cmdText, SqlParameter[] cmdParms) 
        {
			if (conn.State != ConnectionState.Open)
				conn.Open();

			cmd.Connection = conn;
			cmd.CommandText = cmdText;

			if (trans != null)
				cmd.Transaction = trans;

			cmd.CommandType = cmdType;

			if (cmdParms != null) {
                cmd.Parameters.Clear();
				foreach (SqlParameter parm in cmdParms)
					cmd.Parameters.Add(parm);
			}
		}

        private static void PrepareReliableCommand(SqlCommand cmd, SqlConnection conn, SqlTransaction trans, CommandType cmdType, string cmdText, SqlParameter[] cmdParms)
        {
            if (conn.State != ConnectionState.Open)
                conn.Open();

            cmd.Connection = conn;
            cmd.CommandText = cmdText;

            if (trans != null)
                cmd.Transaction = trans;

            cmd.CommandType = cmdType;

            if (cmdParms != null)
            {
                cmd.Parameters.Clear();
                foreach (SqlParameter parm in cmdParms)
                    cmd.Parameters.Add(parm);
            }
        }

        public static void RunScript(string connString, string sql)
        {
            string[] commands = sql.Split(new string[] { "GO\r\n", "GO ", "GO\t" }, StringSplitOptions.RemoveEmptyEntries);
            var count = 0;
            foreach (string c in commands)
            {
                try
                {
                    count++;
                    ExecuteNonQuery(connString, CommandType.Text, c);
                }
                catch (SqlException ex)
                {
                    throw new Exception(ex.Message + "; " + c, ex);
                }
            }
        }

        public static string BuildConnectionString(string serverName, string dbName, string dbUsername, string dbPassword)
        {
            var connBuilder = new SqlConnectionStringBuilder();

            connBuilder.UserID = dbUsername;
            connBuilder.Password = dbPassword;
            connBuilder.DataSource = serverName;
            connBuilder.InitialCatalog = dbName;
            connBuilder.Pooling = true;

            return connBuilder.ConnectionString;
        }

        public enum DbConnectivityResult { NotEmpty, Empty, DoesnExist, CantConnect };

        public static DbConnectivityResult CheckDbConnectivity(string connString)
        {
            var connBuilder = new SqlConnectionStringBuilder(connString);
            var database = connBuilder.InitialCatalog;
            connBuilder.InitialCatalog = "";
            try
            {
                var sql = @"SELECT COUNT(name) FROM [sys].[databases] WHERE name = @database";
                var databaseExists = ((int)SqlHelper.ExecuteScalar(connBuilder.ConnectionString, CommandType.Text, sql, new SqlParameter("@database", database)) > 0);
                if (!string.IsNullOrWhiteSpace(database) && databaseExists)
                {
                    sql = "SELECT COUNT(*) FROM [sysobjects] WHERE [type]  IN ('U', 'V', 'P')";
                    var isEmpty = ((int)SqlHelper.ExecuteScalar(connString, CommandType.Text, sql) == 0);
                    return isEmpty ? DbConnectivityResult.Empty : DbConnectivityResult.NotEmpty;
                }
                return DbConnectivityResult.DoesnExist;
            }
            catch (Exception ex)
            {
            }
            return DbConnectivityResult.CantConnect;
        }

        public static List<string> ListDatabases(string connString)
        {
            List<string> list = new List<string>();
            var sql = @"SELECT name
                        FROM [sys].[databases]
                        WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')";

            using (var reader = SqlHelper.ExecuteReader(connString, CommandType.Text, sql))
            {
                while (reader.Read())
                {
                    list.Add((string)reader["name"]);
                }
            }
            return list;
        }

        public static Type GetDbType(int dbTypeId)
        {
            switch (dbTypeId)
            {
                case -5:
                    return typeof(long);
                case -1:    // text
                    return typeof(string);
                case -7:    // bit
                    return typeof(bool);
                case -6:    // tinyint
                    return typeof(byte);
                case 3:     // decimal
                    return typeof(decimal);
                case 6:
                    return typeof(double);
                case 4:     // int
                    return typeof(int);
                case -9:
                    return typeof(DateTime);
                case 11:
                    return typeof(DateTime);
                case -10:
                    return typeof(string); // ntext 
                default:
                    return typeof(string);
            }
        }

        public static string GetTableQualifier(string tableIdentifier)
        {
            if (string.IsNullOrEmpty(tableIdentifier))
                return tableIdentifier;

            var temp = tableIdentifier.Split('.');

            if (temp.Count() > 1)
            {
                return temp[1].Trim(new[] { '[', ']' });
            }
            else if (temp.Count() == 1)
            {
                return temp[0].Trim(new[] { '[', ']' });
            }
            return "";
        }

        public static SqlParameter[] PreparePrimaryKeyParameters(IOrderedDictionary dataKeys, ref string sqlPart)
        {
            int primaryKeyOrdinal = 0;
            var parameters = new SqlParameter[dataKeys.Keys.Count];

            foreach (var partOfPrimaryKey in dataKeys.Keys)
            {
                sqlPart += " AND " + partOfPrimaryKey + "=@PK" + primaryKeyOrdinal;
                parameters[primaryKeyOrdinal] = new SqlParameter("@PK" + primaryKeyOrdinal, dataKeys[partOfPrimaryKey]);
                primaryKeyOrdinal++;
            }
            return parameters;
        }
	}
}