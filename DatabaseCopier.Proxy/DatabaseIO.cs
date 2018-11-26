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
        private const int ParentObjectId = 4;
        private const int ReferenceObjectId = 12;



        public DatabaseIO(string sourceConnectionString, string targetConnectionString)
        {
            _sourceConnectionString = sourceConnectionString;
            _targetConnectionString = targetConnectionString;
        }

        public Dictionary<int, TableNode> GetTables(HashSet<string> ignoreTables = null)
        {
            var tables = new Dictionary<int, TableNode>();

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
                        tables.Add(tableId, new TableNode(tableName, tableId));
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

                var count = new SqlCommand()
                {
                    CommandText = "SELECT Count(*) FROM " + table.TableName,
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                int rows = -1;

                using (var cReader = count.ExecuteReader())
                {
                    cReader.Read();
                    rows = cReader.GetInt32(0);
                }

                Console.WriteLine($"{table.TableName} Rows: {rows}");

                var cmd = new SqlCommand()
                {
                    CommandText = "SELECT * from " + table.TableName,
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                using (var reader = cmd.ExecuteReader())
                {

                    using (var bulkCopy =
                        new SqlBulkCopy(_targetConnectionString, SqlBulkCopyOptions.KeepIdentity))
                    {
                        bulkCopy.BulkCopyTimeout = 60 * 20;
                        bulkCopy.DestinationTableName = table.TableName;
                        bulkCopy.BatchSize = 30000;
                        bulkCopy.SqlRowsCopied += BulkCopy_SqlRowsCopied;
                        bulkCopy.WriteToServer(reader);
                        bulkCopy.SqlRowsCopied -= BulkCopy_SqlRowsCopied;
                    }

                }
            }
        }

        private void BulkCopy_SqlRowsCopied(object sender, SqlRowsCopiedEventArgs e)
        {
            Console.WriteLine(e.RowsCopied);
        }
    }
}
