using DatabaseCopier.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DatabaseCopier.Proxy
{
    public class DatabaseIO
    {
        public readonly string SourceConnectionString;
        public readonly string TargetConnectionString;

        private const int ObjectName = 0;
        private const int ObjectId = 1;
        private const int SchemaId = 3;
        private const int ParentObjectId = 4;
        private const int ReferenceObjectId = 12;

        public Action<object, SqlRowsCopiedEventArgs> ProgressEvent = null;
        
        public int BatchSize = 30000;
        public int TimeOut = 60 * 20;


        public DatabaseIO(string sourceConnectionString, string targetConnectionString)
        {
            SourceConnectionString = sourceConnectionString;
            TargetConnectionString = targetConnectionString;
        }

        public Dictionary<int, TableSchema> GetSchemas()
        {
            var schemas = new Dictionary<int, TableSchema>();

            using (var connection =
                new SqlConnection(
                    SourceConnectionString)
            )
            {
                var cmd = new SqlCommand
                {
                    CommandText = "SELECT * FROM sys.schemas",
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                connection.Open();

                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var schemaName = reader.GetString(ObjectName);
                    var schemaId = reader.GetInt32(ObjectId);
                    schemas.Add(schemaId, new TableSchema(schemaId, schemaName));
                }
            }

            return schemas;
        }

        public Dictionary<int, TableNode> GetTables(HashSet<string> ignoreTables = null)
        {
            var tables = new Dictionary<int, TableNode>();

            var schemas = GetSchemas();

            using (var connection =
                new SqlConnection(
                    SourceConnectionString)
            )
            {
                var cmd = new SqlCommand
                {
                    CommandText = "SELECT * FROM sys.tables",
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                connection.Open();

                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var tableName = reader.GetString(ObjectName);
                    if (ignoreTables == null || !ignoreTables.Contains(tableName))
                    {
                        var tableId = reader.GetInt32(ObjectId);
                        var schemaId = reader.GetInt32(SchemaId);
                        tables.Add(tableId, new TableNode(tableId, tableName, schemas[schemaId]));
                    }
                }
            }

            return tables;
        }

        public List<ForeignKey> GetForeignKeys()
        {

            var keys = new List<ForeignKey>();

            using (var connection = new SqlConnection(SourceConnectionString))
            {
                connection.Open();

                var cmd = new SqlCommand()
                {
                    CommandText = "SELECT * from sys.foreign_keys",
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                using (var reader = cmd.ExecuteReader())
                {

                    while (reader.Read())
                    {
                        keys.Add(new ForeignKey(reader.GetInt32(ReferenceObjectId), reader.GetInt32(ParentObjectId)));
                    }
                }
            }

            return keys;
        }

        public void CopyTable(TableNode table)
        {
            using (var connection = new SqlConnection(SourceConnectionString))
            {
                connection.Open();

                var cmd = new SqlCommand()
                {
                    CommandText = $"SELECT * from " + table.FullTableName,
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                using (var reader = cmd.ExecuteReader())
                {

                    using (var bulkCopy =
                        new SqlBulkCopy(TargetConnectionString, SqlBulkCopyOptions.KeepIdentity))
                    {
                        bulkCopy.BulkCopyTimeout = TimeOut;
                        bulkCopy.DestinationTableName = table.FullTableName;
                        bulkCopy.BatchSize = BatchSize;
                        bulkCopy.NotifyAfter = 1000;
                        if (ProgressEvent != null)
                            bulkCopy.SqlRowsCopied += ProgressEvent.Invoke;

                        bulkCopy.WriteToServer(reader);

                        if (ProgressEvent != null)
                            bulkCopy.SqlRowsCopied -= ProgressEvent.Invoke;
                    }
                }
            }
        }

        public long GetRows(TableNode table)
        {
            long rows = -1;
            using (var connection = new SqlConnection(SourceConnectionString))
            {
                var count = new SqlCommand()
                {
                    CommandText = "SELECT Count_BIG(*) FROM " + table.FullTableName,
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                connection.Open();

                using (var cReader = count.ExecuteReader())
                {
                    cReader.Read();
                    rows = cReader.GetInt64(0);
                }
            }

            return rows;
        }
    }
}
