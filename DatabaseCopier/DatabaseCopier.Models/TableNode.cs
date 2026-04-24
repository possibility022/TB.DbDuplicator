using System.Collections.Generic;

namespace DatabaseCopier.Models
{
    public class TableNode
    {
        public string TableName { get; private set; }
        public string FullTableName { get; private set; }
        public int TableId { get; private set; }
        public TableSchema Schema { get; set; }
        public ICollection<TableNode> Childrens { get; set; } = new List<TableNode>();

        public ICollection<TableNode> Parents { get; set; } = new List<TableNode>();

        /// <summary>
        /// Set when this table is the main side of a system-versioned temporal relationship.
        /// Points to the corresponding history table node.
        /// </summary>
        public TableNode HistoryTableNode { get; set; }

        /// <summary>
        /// Set when this table is a history table for a temporal relationship.
        /// Points to the main (temporal) table node.
        /// </summary>
        public TableNode MainTemporalTableNode { get; set; }

        /// <summary>
        /// History table ID from sys.tables (used before linking nodes).
        /// </summary>
        public int? HistoryTableId { get; set; }


        public TableNode(int tableId, string tableName, TableSchema schema)
        {
            TableName = tableName;
            TableId = tableId;
            Schema = schema;
            FullTableName = $"[{Schema.Name}].[{TableName}]";
        }

        public override string ToString() => FullTableName;
    }
}
