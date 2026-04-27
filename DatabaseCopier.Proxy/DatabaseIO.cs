using DatabaseCopier.Models;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading;

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
                    // Join with sys.partitions to get row counts (no VIEW DATABASE STATE permission required)
                    CommandText = @"
                        SELECT t.name, t.object_id, t.schema_id, t.temporal_type, t.history_table_id,
                               ISNULL(SUM(ps.rows), 0) AS row_count
                        FROM sys.tables t
                        LEFT JOIN sys.partitions ps 
                            ON t.object_id = ps.object_id AND ps.index_id IN (0, 1)
                        GROUP BY t.name, t.object_id, t.schema_id, t.temporal_type, t.history_table_id",
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                connection.Open();

                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var tableName = reader.GetString(0);
                    if (ignoreTables == null || !ignoreTables.Contains(tableName))
                    {
                        var tableId = reader.GetInt32(1);
                        var schemaId = reader.GetInt32(2);
                        var temporalType = reader.GetByte(3);
                        var historyTableId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                        var rowCount = reader.GetInt64(5);

                        var node = new TableNode(tableId, tableName, schemas[schemaId]);
                        node.RowCount = rowCount;
                        // temporal_type = 2 means system-versioned temporal table
                        if (temporalType == 2 && historyTableId.HasValue)
                            node.HistoryTableId = historyTableId;

                        tables.Add(tableId, node);
                    }
                }
            }

            // Link temporal tables to their history tables
            foreach (var table in tables.Values)
            {
                if (table.HistoryTableId.HasValue && tables.TryGetValue(table.HistoryTableId.Value, out var historyTable))
                {
                    table.HistoryTableNode = historyTable;
                    historyTable.MainTemporalTableNode = table;
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

        public void CreateTableIfNotExists(TableNode table)
        {
            var columnDefs = new System.Text.StringBuilder();

            using (var sourceConnection = new SqlConnection(SourceConnectionString))
            {
                sourceConnection.Open();

                var cmd = new SqlCommand
                {
                    CommandText = @"
                        SELECT
                            c.name,
                            tp.name,
                            c.max_length,
                            c.precision,
                            c.scale,
                            c.is_nullable
                        FROM sys.columns c
                        INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                        INNER JOIN sys.tables t ON c.object_id = t.object_id
                        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                        WHERE s.name = @schema AND t.name = @table
                        ORDER BY c.column_id",
                    CommandType = CommandType.Text,
                    Connection = sourceConnection
                };

                cmd.Parameters.AddWithValue("@schema", table.Schema.Name);
                cmd.Parameters.AddWithValue("@table", table.TableName);

                using (var reader = cmd.ExecuteReader())
                {
                    bool first = true;
                    while (reader.Read())
                    {
                        if (!first) columnDefs.Append(",\n    ");
                        first = false;

                        var colName   = reader.GetString(0);
                        var typeName  = reader.GetString(1);
                        var maxLength = reader.GetInt16(2);
                        var precision = reader.GetByte(3);
                        var scale     = reader.GetByte(4);
                        var nullable  = reader.GetBoolean(5);

                        string typeDecl;
                        switch (typeName.ToLower())
                        {
                            case "varchar":
                            case "char":
                            case "varbinary":
                            case "binary":
                                typeDecl = maxLength == -1 ? typeName + "(MAX)" : typeName + "(" + maxLength + ")";
                                break;
                            case "nvarchar":
                            case "nchar":
                                typeDecl = maxLength == -1 ? typeName + "(MAX)" : typeName + "(" + (maxLength / 2) + ")";
                                break;
                            case "decimal":
                            case "numeric":
                                typeDecl = typeName + "(" + precision + "," + scale + ")";
                                break;
                            default:
                                typeDecl = typeName;
                                break;
                        }

                        columnDefs.Append("[" + colName + "] " + typeDecl + " " + (nullable ? "NULL" : "NOT NULL"));
                    }
                }
            }

            if (columnDefs.Length == 0)
                throw new InvalidOperationException("No columns found for table " + table.FullTableName + " in source database.");

            var createSchemaSql = "IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '" + table.Schema.Name + "')" +
                                  " EXEC('CREATE SCHEMA [" + table.Schema.Name + "]')";

            var createTableSql = "IF NOT EXISTS (" +
                                 "SELECT 1 FROM sys.tables t " +
                                 "INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
                                 "WHERE s.name = '" + table.Schema.Name + "' AND t.name = '" + table.TableName + "')" +
                                 " CREATE TABLE " + table.FullTableName + " (\n    " + columnDefs + "\n)";

            using (var targetConnection = new SqlConnection(TargetConnectionString))
            {
                targetConnection.Open();

                using (var cmd = new SqlCommand(createSchemaSql, targetConnection))
                    cmd.ExecuteNonQuery();

                using (var cmd = new SqlCommand(createTableSql, targetConnection))
                    cmd.ExecuteNonQuery();
            }
        }

        private class TemporalTableInfo
        {
            public string PeriodStartColumn { get; set; }
            public string PeriodEndColumn { get; set; }
            public bool PeriodStartHidden { get; set; }
            public bool PeriodEndHidden { get; set; }
            public string HistoryTableFullName { get; set; }
        }

        private bool IsSystemVersioningEnabled(TableNode table, SqlConnection targetConnection)
        {
            var cmd = new SqlCommand
            {
                CommandText = @"
                    SELECT t.temporal_type
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = @schema AND t.name = @table",
                CommandType = CommandType.Text,
                Connection = targetConnection
            };

            cmd.Parameters.AddWithValue("@schema", table.Schema.Name);
            cmd.Parameters.AddWithValue("@table", table.TableName);

            var result = cmd.ExecuteScalar();
            // temporal_type = 2 means SYSTEM_VERSIONED_TEMPORAL_TABLE (versioning is ON)
            return result != null && result != DBNull.Value && (byte)result == 2;
        }

        private TemporalTableInfo GetTemporalInfo(TableNode table)
        {
            using (var connection = new SqlConnection(SourceConnectionString))
            {
                connection.Open();

                var cmd = new SqlCommand
                {
                    CommandText = @"
                        SELECT
                            pc_start.name,
                            pc_end.name,
                            pc_start.is_hidden,
                            pc_end.is_hidden,
                            hs.name,
                            ht.name
                        FROM sys.tables t
                        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                        INNER JOIN sys.periods p ON t.object_id = p.object_id
                        INNER JOIN sys.columns pc_start ON p.start_column_id = pc_start.column_id AND t.object_id = pc_start.object_id
                        INNER JOIN sys.columns pc_end ON p.end_column_id = pc_end.column_id AND t.object_id = pc_end.object_id
                        LEFT JOIN sys.tables ht ON t.history_table_id = ht.object_id
                        LEFT JOIN sys.schemas hs ON ht.schema_id = hs.schema_id
                        WHERE s.name = @schema AND t.name = @table AND t.temporal_type = 2",
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                cmd.Parameters.AddWithValue("@schema", table.Schema.Name);
                cmd.Parameters.AddWithValue("@table", table.TableName);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var historySchema = reader.IsDBNull(4) ? null : reader.GetString(4);
                        var historyTable  = reader.IsDBNull(5) ? null : reader.GetString(5);
                        return new TemporalTableInfo
                        {
                            PeriodStartColumn   = reader.GetString(0),
                            PeriodEndColumn     = reader.GetString(1),
                            PeriodStartHidden   = reader.GetBoolean(2),
                            PeriodEndHidden     = reader.GetBoolean(3),
                            HistoryTableFullName = (historySchema != null && historyTable != null)
                                ? $"[{historySchema}].[{historyTable}]"
                                : null
                        };
                    }
                }
            }

            return null;
        }

        public void CopyTable(TableNode table, CancellationToken cancellationToken = default)
        {
            var temporalInfo = GetTemporalInfo(table);

            using (var connection = new SqlConnection(SourceConnectionString))
            {
                connection.Open();

                // SELECT * excludes hidden period columns on temporal tables, so append them explicitly.
                string selectSql = "SELECT *";
                if (temporalInfo != null)
                {
                    if (temporalInfo.PeriodStartHidden)
                        selectSql += $", [{temporalInfo.PeriodStartColumn}]";
                    if (temporalInfo.PeriodEndHidden)
                        selectSql += $", [{temporalInfo.PeriodEndColumn}]";
                }
                selectSql += " FROM " + table.FullTableName;

                var cmd = new SqlCommand()
                {
                    CommandText = selectSql,
                    CommandType = CommandType.Text,
                    Connection = connection
                };

                using (var reader = cmd.ExecuteReader())
                {
                    using (var targetConnection = new SqlConnection(TargetConnectionString))
                    {
                        targetConnection.Open();

                        bool disabledVersioning = false;
                        if (temporalInfo != null && IsSystemVersioningEnabled(table, targetConnection))
                        {
                            // Disable system versioning so the period columns become writable.
                            using (var alterCmd = new SqlCommand(
                                $"ALTER TABLE {table.FullTableName} SET (SYSTEM_VERSIONING = OFF)",
                                targetConnection))
                                alterCmd.ExecuteNonQuery();
                            disabledVersioning = true;
                        }

                        bool copyCompleted = false;
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            using (var bulkCopy = new SqlBulkCopy(targetConnection, SqlBulkCopyOptions.KeepIdentity, null))
                            {
                                bulkCopy.BulkCopyTimeout = TimeOut;
                                bulkCopy.DestinationTableName = table.FullTableName;
                                bulkCopy.BatchSize = BatchSize;
                                bulkCopy.NotifyAfter = 1000;
                                if (ProgressEvent != null)
                                    bulkCopy.SqlRowsCopied += ProgressEvent.Invoke;

                                // Map columns by name so ordinal differences (e.g. appended hidden columns) are handled correctly.
                                for (int i = 0; i < reader.FieldCount; i++)
                                    bulkCopy.ColumnMappings.Add(reader.GetName(i), reader.GetName(i));

                                bulkCopy.WriteToServer(reader);
                                copyCompleted = true;

                                if (ProgressEvent != null)
                                    bulkCopy.SqlRowsCopied -= ProgressEvent.Invoke;
                            }
                        }
                        finally
                        {
                            // Re-enable system versioning only if we explicitly disabled it on the target
                            if (copyCompleted && disabledVersioning)
                            {
                                var historyRef = table.HistoryTableNode != null
                                    ? $"HISTORY_TABLE = {table.HistoryTableNode.FullTableName}, "
                                    : (temporalInfo?.HistoryTableFullName != null
                                        ? $"HISTORY_TABLE = {temporalInfo.HistoryTableFullName}, "
                                        : "");

                                using (var alterCmd = new SqlCommand(
                                    $"ALTER TABLE {table.FullTableName} SET (SYSTEM_VERSIONING = ON ({historyRef}DATA_CONSISTENCY_CHECK = OFF))",
                                    targetConnection))
                                    alterCmd.ExecuteNonQuery();
                            }
                        }
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
                    Connection = connection,
                    CommandTimeout = 200
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
