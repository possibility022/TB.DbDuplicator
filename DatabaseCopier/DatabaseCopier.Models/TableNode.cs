using System.Collections.Generic;

namespace DatabaseCopier.Models
{
    public class TableNode
    {
        public string TableName { get; private set; }
        public int TableId { get; private set; }
        public ICollection<TableNode> Childrens { get; set; } = new List<TableNode>();

        public ICollection<TableNode> Parents { get; set; } = new List<TableNode>();


        public TableNode(string tableName, int tableId)
        {
            TableName = tableName;
            TableId = tableId;
        }

        public override string ToString() => TableName;
    }
}
