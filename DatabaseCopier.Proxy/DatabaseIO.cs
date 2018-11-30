using DatabaseCopier.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace DatabaseCopier.Proxy
{
    public class DatabaseIO
    {
        private readonly string _sourceConnectionString;
        private readonly string _targetConnectionString;

        private const int ObjectName = 0;
        private const int ObjectId = 1;
        private const int SchemaId = 3;
        private const int ParentObjectId = 4;
        private const int ReferenceObjectId = 12;



        public DatabaseIO(string sourceConnectionString, string targetConnectionString)
        {
            _sourceConnectionString = sourceConnectionString;
            _targetConnectionString = targetConnectionString;
        }

        public Dictionary<int, TableSchema> GetSchemas()
        {
            var schemas = new Dictionary<int, TableSchema>();

            using (var connection =
                new SqlConnection(
                    _sourceConnectionString)
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
                    _sourceConnectionString)
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

            using (var connection = new SqlConnection(_sourceConnectionString))
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
            using (var connection = new SqlConnection(_sourceConnectionString))
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
                        new SqlBulkCopy(_targetConnectionString, SqlBulkCopyOptions.KeepIdentity))
                    {
                        bulkCopy.BulkCopyTimeout = 60 * 20;
                        bulkCopy.DestinationTableName = table.FullTableName;
                        bulkCopy.BatchSize = 30000;
                        bulkCopy.SqlRowsCopied += BulkCopy_SqlRowsCopied;
                        bulkCopy.WriteToServer(reader);
                        bulkCopy.SqlRowsCopied -= BulkCopy_SqlRowsCopied;
                    }

                }
            }
        }

        public int GetRows(TableNode table)
        {
            int rows = -1;
            using (var connection = new SqlConnection(_sourceConnectionString))
            {
                var count = new SqlCommand()
                {
                    CommandText = "SELECT Count(*) FROM " + table.FullTableName,
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                connection.Open();

                using (var cReader = count.ExecuteReader())
                {
                    cReader.Read();
                    rows = cReader.GetInt32(0);
                }
            }

            return rows;
        }

        private void BulkCopy_SqlRowsCopied(object sender, SqlRowsCopiedEventArgs e)
        {
            Console.WriteLine(e.RowsCopied);
        }
    }
}
