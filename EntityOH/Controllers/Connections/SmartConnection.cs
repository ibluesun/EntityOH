﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Data.Common;

namespace EntityOH.Controllers.Connections
{
    public class SmartConnection : IDisposable
    {

        /// <summary>
        /// Default key that will create the connection.
        /// </summary>
        public static string DefaultConnectionKey { get; set; }


        /// <summary>
        /// 
        /// </summary>
        static SmartConnection()
        {
            if (ConfigurationManager.ConnectionStrings.Count > 0)
                DefaultConnectionKey = ConfigurationManager.ConnectionStrings[0].Name;

        }

        /// <summary>
        /// Create a connection based on the default connection key.
        /// </summary>
        /// <returns></returns>
        public static SmartConnection GetSmartConnection()
        {
            return new SmartConnection(ConfigurationManager.ConnectionStrings[DefaultConnectionKey].ConnectionString);
        }


        public static SmartConnection GetSmartConnection(string connectionKey)
        {
            return new SmartConnection(ConfigurationManager.ConnectionStrings[connectionKey].ConnectionString);
        }

        public string ConnectionString
        {
            get;
            private set;
        }

        SqlConnection _InternalConnection;

        private SmartConnection(string connectionString)
        {

            ConnectionString = connectionString;

            _InternalConnection = new SqlConnection(ConnectionString);

        }

        public IDataReader ExecuteReader(string text)
        {
            SqlCommand cmd = new SqlCommand(text, _InternalConnection);

            if (_InternalConnection.State == ConnectionState.Closed) _InternalConnection.Open();

            return cmd.ExecuteReader(CommandBehavior.CloseConnection);
        }

        public IDataReader ExecuteReader(DbCommand command)
        {
            if (_InternalConnection.State == ConnectionState.Closed) _InternalConnection.Open();

            command.Connection = _InternalConnection;

            return command.ExecuteReader(CommandBehavior.CloseConnection);
        }

        public object ExecuteScalar(DbCommand command)
        {
            if (_InternalConnection.State == ConnectionState.Closed) _InternalConnection.Open();

            command.Connection = _InternalConnection;
            object result = command.ExecuteScalar();

            _InternalConnection.Close();

            return result;
        }


        public void ExecuteNonQuery(DbCommand command)
        {
            if (_InternalConnection.State == ConnectionState.Closed) _InternalConnection.Open();

            command.Connection = _InternalConnection;
            
            command.ExecuteNonQuery();

            _InternalConnection.Close();

        }


        /// <summary>
        /// Make insert command of the entity.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="identityExist"></param>
        /// <returns></returns>
        internal DbCommand GetInsertCommand<Entity>(out EntityFieldRuntime identityFieldRuntime)
        {
            List<string> Fields = new List<string>();

            identityFieldRuntime = null;

            foreach (var fr in EntityRuntime<Entity>.FieldsRuntime)
            {
                if (!fr.Value.Identity)
                {
                    Fields.Add(fr.Value.PhysicalName);
                }
                else
                {
                    identityFieldRuntime = fr.Value;
                }
            }

            string FinalParameters = string.Empty;
            string FinalFields = string.Empty;
            foreach (var f in Fields)
            {
                FinalFields += f;
                FinalFields += ",";

                FinalParameters += "@" + f;
                FinalParameters += ",";

            }

            FinalFields = FinalFields.TrimEnd(',');
            FinalParameters = FinalParameters.TrimEnd(',');

            string InsertStatementTemplate = "INSERT INTO {0} ({1}) VALUES ({2})";

            string InsertStatement = string.Format(InsertStatementTemplate, EntityRuntime<Entity>.PhysicalName, FinalFields, FinalParameters);

            if (identityFieldRuntime != null)
            {

                InsertStatement += "; SELECT @@IDENTITY";
                
            }


            DbCommand command = new SqlCommand(InsertStatement);


            return command;
        }

        internal DbParameter GetParameter(string parameterName, object value)
        {
            SqlParameter sp = new SqlParameter("@" + parameterName, value);
            return sp;
        }

        internal DbCommand GetSelectCommand<Entity>()
        {
            string SelectTemplate = "SELECT * FROM " + EntityRuntime<Entity>.PhysicalName + " WHERE {0}";

            string Conditions = string.Empty;

            foreach (var fr in EntityRuntime<Entity>.FieldsRuntime)
            {
                if (fr.Value.Primary)
                {
                    Conditions += fr.Value.PhysicalName + " = @" + fr.Value.PhysicalName + ",";
                }
            }

            if (string.IsNullOrEmpty(Conditions)) throw new NotImplementedException("Selecting entity without primary field is not implemented\nPlease consider adding decorating your entity fields with one or more primary ids.");

            Conditions = Conditions.TrimEnd(',');

            var finalSelect = string.Format(SelectTemplate, Conditions);

            return new SqlCommand(finalSelect);
        }


        internal DbCommand GetCountCommand<Entity>()
        {
            string SelectTemplate = "SELECT COUNT(*) FROM " + EntityRuntime<Entity>.PhysicalName;

            var finalSelect = string.Format(SelectTemplate);

            return new SqlCommand(finalSelect);
        }

        internal DbCommand GetAggregateFunctionCommand<Entity>(string aggregateFunction, string field)
        {
            string SelectTemplate = "SELECT {0}({1}) FROM " + EntityRuntime<Entity>.PhysicalName;

            var finalSelect = string.Format(SelectTemplate, aggregateFunction, field);

            return new SqlCommand(finalSelect);
        }

        internal DbCommand GetDeleteCommand<Entity>()
        {
            string DeleteTemplate = "DELETE " + EntityRuntime<Entity>.PhysicalName + " WHERE {0}";

            string Conditions = string.Empty;

            foreach (var fr in EntityRuntime<Entity>.FieldsRuntime)
            {
                if (fr.Value.Primary)
                {
                    Conditions += fr.Value.PhysicalName + " = @" + fr.Value.PhysicalName + ",";
                }
            }

            if (string.IsNullOrEmpty(Conditions)) throw new NotImplementedException("Selecting entity without primary field is not implemented\nPlease consider adding decorating your entity fields with one or more primary ids.");

            Conditions = Conditions.TrimEnd(',');

            var finalDelete = string.Format(DeleteTemplate, Conditions);

            return new SqlCommand(finalDelete);
        }


        internal DbCommand GetUpdateCommand<Entity>()
        {
            string UpdateTemplate = "UPDATE " + EntityRuntime<Entity>.PhysicalName + " SET {0} WHERE {1}";

            string Conditions = string.Empty;
            string updatelist = string.Empty;


            foreach (var fr in EntityRuntime<Entity>.FieldsRuntime)
            {
                if (fr.Value.Primary)
                {
                    Conditions += fr.Value.PhysicalName + " = @" + fr.Value.PhysicalName + ",";
                }
                else
                {
                    // normal field
                    if (!fr.Value.Identity)
                    {
                        updatelist += fr.Value.PhysicalName + " = @" + fr.Value.PhysicalName + ",";
                    }
                }
            }

            if (string.IsNullOrEmpty(Conditions)) throw new NotImplementedException("Selecting entity without primary field is not implemented\nPlease consider adding decorating your entity fields with one or more primary ids.");

            Conditions = Conditions.TrimEnd(',');
            updatelist = updatelist.TrimEnd(',');

            var UpdateSelect = string.Format(UpdateTemplate, updatelist, Conditions);

            return new SqlCommand(UpdateSelect);


        }


        internal DbCommand GetStoredProcedureCommand<Entity>(string procName)
        {
            var sq = new SqlCommand(procName);
            sq.CommandType = CommandType.StoredProcedure;

            return sq;
        }


        #region IDisposable Members

        public void Dispose()
        {
            if(_InternalConnection.State == ConnectionState.Open) _InternalConnection.Close();
            _InternalConnection.Dispose();
        }

        #endregion
    }
}
