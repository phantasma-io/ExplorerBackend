using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using System.Data;
using Serilog;

namespace GhostDevs
{
    public class PostgreSQLConnector
    {
        #region DATABASE INIT/DEINIT
        private readonly string ConnectionString;

        private NpgsqlConnection Connection;

        public PostgreSQLConnector(string ConnectionString)
        {
            this.ConnectionString = ConnectionString;

            Connection = new NpgsqlConnection(this.ConnectionString);
            Connection.Open();
        }

        ~PostgreSQLConnector()
        {
            Connection.Close();
        }
        #endregion

        #region DATABASE API: EXECUTION
        public object ExecQuery(string query, List<Tuple<string, object>> parameters = null)
        {
            var cmd = new NpgsqlCommand(query, Connection);

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    cmd.Parameters.AddWithValue(parameter.Item1, parameter.Item2);
                }
            }

            object result;
            try
            {
                result = cmd.ExecuteScalar();
            }
            catch (Exception e)
            {
                throw new Exception($"Error executing '{query}' statement:\n{e.Message}");
            }

            cmd.Dispose();

            return result;
        }
        public bool ExecQueryB(string query, List<Tuple<string, object>> parameters = null)
        {
            object result = ExecQuery(query, parameters);

            if (result == null)
            {
                return false;
            }

            bool iResult = (bool)result;

            return iResult;
        }
        public long? ExecQueryI(string query, List<Tuple<string, object>> parameters = null)
        {
            object result = ExecQuery(query, parameters);

            if (result == null)
            {
                return null;
            }

            return (long)result;
        }
        public Int32? ExecQueryInt32(string query, List<Tuple<string, object>> parameters = null)
        {
            object result = ExecQuery(query, parameters);

            if (result == null)
            {
                return null;
            }

            return (Int32)result;
        }
        public string ExecQueryS(string query, List<Tuple<string, object>> parameters = null)
        {
            object result = ExecQuery(query, parameters);

            if (result == null)
            {
                return "";
            }

            var sResult = result.ToString();

            return sResult;
        }
        public List<Dictionary<string, object>> ExecQueryDNex(bool forceQueryLogging, string query, List<Tuple<string, object>> parameters = null)
        {
            if(forceQueryLogging && !Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                Log.Information($"DB: ExecQueryDN(): Query: {query}");
            else
                Log.Debug($"DB: ExecQueryDN(): Query: {query}");

            var cmd = new NpgsqlCommand(query, Connection);

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    cmd.Parameters.AddWithValue(parameter.Item1, parameter.Item2);
                }
            }

            DateTime executeStartTime = DateTime.Now;
            NpgsqlDataReader dr;
            try
            {
                dr = cmd.ExecuteReader();
            }
            catch (Exception e)
            {
                throw new Exception($"Error executing '{query}' statement:\n{e.Message}");
            }
            TimeSpan executeTime = DateTime.Now - executeStartTime;
            Log.Debug($"DB: ExecQueryDN(): Query executed in {Math.Round(executeTime.TotalSeconds, 3)} sec");

            DateTime resultStartTime = DateTime.Now;
            var result = new List<Dictionary<string, object>>();
            while (dr.Read())
            {
                var row = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);
                result.Add(row);
                for (var i = 0; i < dr.FieldCount; i++)
                {
                    Type arrayType = dr[i].GetType();
                    Type elementType = arrayType.GetElementType();
                    TypeCode elementTypeCode = Type.GetTypeCode(elementType);

                    if(elementTypeCode == TypeCode.Byte)
                        row.Add(dr.GetName(i).ToLower(), System.Text.Encoding.Default.GetString((byte[])dr[i]));
                    else if (!String.IsNullOrEmpty(dr[i].ToString()))
                        row.Add(dr.GetName(i).ToLower(), dr[i]);
                }
            }
            dr.Close();
            dr.Dispose();
            TimeSpan resultTime = DateTime.Now - resultStartTime;
            Log.Debug($"DB: ExecQueryDN(): Result generated in {Math.Round(resultTime.TotalSeconds, 3)} sec");

            cmd.Dispose();

            return result;
        }
        public List<Dictionary<string, object>> ExecQueryDN(string query, List<Tuple<string, object>> parameters = null)
        {
            return ExecQueryDNex(false, query, parameters);
        }
        public static void LogResult(List<Dictionary<string, object>> result)
        {
            foreach(var row in result)
            {
                var rowSerialized = "";
                foreach(var column in row)
                {
                    rowSerialized += $" {column.Key} {column.Value}";
                }
                Log.Information(rowSerialized);
            }
        }
        public int Exec(string command, List<Tuple<string, object>> parameters = null)
        {
            int rowsAffected = 0;
            var cmd = new NpgsqlCommand();
            cmd.Connection = Connection;

            cmd.CommandText = command;

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    cmd.Parameters.AddWithValue(parameter.Item1, parameter.Item2);
                }
            }

            try
            {
                rowsAffected = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                throw new Exception($"Error executing '{cmd.CommandText}' statement:\n{e.Message}");
            }

            cmd.Dispose();

            return rowsAffected;
        }
        public int Insert(string tableName, Dictionary<string, object> valuePairs, string upsertTarget = "", string upsertAction = "")
        {
            int rowsAffected = 0;
            var cmd = new NpgsqlCommand();
            cmd.Connection = Connection;

            cmd.CommandText = $"INSERT INTO {tableName}({string.Join(", ", valuePairs.Keys)}) VALUES({string.Join(", ", valuePairs.Keys.Select(key => "@" + key))})";
            if(!String.IsNullOrEmpty(upsertTarget))
            {
                cmd.CommandText += $" ON CONFLICT {upsertTarget} DO {(String.IsNullOrEmpty(upsertAction) ? "NOTHING" : upsertAction)}";
            }
            foreach(var value in valuePairs)
            {
                cmd.Parameters.AddWithValue("@" + value.Key, value.Value);
            }

            try
            {
                rowsAffected = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                throw new Exception($"Error executing '{cmd.CommandText}' statement with values:\n({string.Join(", ", valuePairs.Select(x => string.Format("{0}: {1}", x.Key, x.Value)))}):\n{e.Message}");
            }

            cmd.Dispose();

            return rowsAffected;
        }
        public int InsertAndGetId(string tableName, string idColumnName, Dictionary<string, object> valuePairs, string upsertTarget = "", string upsertAction = "")
        {
            var cmd = new NpgsqlCommand();
            cmd.Connection = Connection;

            cmd.CommandText = $"INSERT INTO {tableName}({string.Join(", ", valuePairs.Keys)}) VALUES({string.Join(", ", valuePairs.Keys.Select(key => "@" + key))})";
            if (!String.IsNullOrEmpty(upsertTarget))
            {
                cmd.CommandText += $" ON CONFLICT {upsertTarget} DO {(String.IsNullOrEmpty(upsertAction) ? "NOTHING" : upsertAction)}";
            }
            cmd.CommandText += $" RETURNING {idColumnName}";

            foreach (var value in valuePairs)
            {
                cmd.Parameters.AddWithValue("@" + value.Key, value.Value);
            }

            object result;
            try
            {
                result = cmd.ExecuteScalar();
            }
            catch(Exception e)
            {
                throw new Exception($"Error executing '{cmd.CommandText}' statement with values:\n({string.Join(", ", valuePairs.Select(x => string.Format("{0}: {1}", x.Key, x.Value)))}):\n{e.Message}");
            }
            cmd.Dispose();

            int rowId = (int)result;

            return rowId;
        }
        #endregion

        #region DATABASE API: TRANSACTIONS
        NpgsqlTransaction transaction = null;
        public void TransactionStart()
        {
            if (transaction != null)
                throw new Exception("Transaction already started.");

            transaction = Connection.BeginTransaction();
        }
        public void TransactionCommit()
        {
            if (transaction == null)
                throw new Exception("Transaction not opened.");

            transaction.Commit();
            transaction = null;
        }
        public void TransactionRollback(bool throwExceptionIfTransactionNotOpened = true)
        {
            if (transaction == null)
            {
                if (throwExceptionIfTransactionNotOpened)
                    throw new Exception("Transaction not opened.");
                else
                    return;
            }

            transaction.Rollback();
            transaction = null;
        }
        #endregion

        #region SERVICE METHODS
        public string GetVersion()
        {
            return ExecQueryS("SELECT version()");
        }
        #endregion
    }
}
