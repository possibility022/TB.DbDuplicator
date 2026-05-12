using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseCopier.Models
{
    public class Hierarchy
    {
        private int _highestCountOfRelations;
        private HashSet<TableNode> _tables;

        private static void BuildReferences(IDictionary<int, TableNode> tables, IEnumerable<ForeignKey> keys)
        {
            foreach (var foreignKey in keys)
            {
                tables[foreignKey.OwnerTableId].Childrens.Add(tables[foreignKey.TargetTableId]);
                tables[foreignKey.TargetTableId].Parents.Add(tables[foreignKey.OwnerTableId]);
            }

            // Add temporal table relationships: history table must be copied before main table
            foreach (var table in tables.Values)
            {
                if (table.HistoryTableNode != null && tables.ContainsKey(table.HistoryTableNode.TableId))
                {
                    // Main table depends on history table (history is a "child" that must be processed first)
                    if (!table.Childrens.Contains(table.HistoryTableNode))
                        table.Childrens.Add(table.HistoryTableNode);
                    if (!table.HistoryTableNode.Parents.Contains(table))
                        table.HistoryTableNode.Parents.Add(table);
                }
            }
        }

        public Hierarchy(IDictionary<int, TableNode> tables, IEnumerable<ForeignKey> keys)
        {
            BuildReferences(tables, keys);
            _highestCountOfRelations = tables.Values.Max(r => r.Childrens.Count);
            _tables = new HashSet<TableNode>(tables.Values);
        }

        public List<TableNode> GetTablesInOrder()
        {
            var visited = new List<TableNode>();
            var notVisited = new HashSet<TableNode>(_tables);


            TableNode workOn = null;

            while (notVisited.Any())
            {
                if (workOn == null || !notVisited.Contains(workOn))
                    workOn = notVisited.First(f => f.Childrens.Count == notVisited.Min(f2 => f2.Childrens.Count));

                var workOnChanged = false;

                foreach (var child in workOn.Childrens)
                {
                    if (ReferenceEquals(child, workOn))
                        break;

                    if (notVisited.Contains(child))
                    {
                        workOnChanged = true;
                        workOn = child;
                        break;
                    }
                }

                if (!workOnChanged)
                {
                    notVisited.Remove(workOn);
                    visited.Add(workOn);
                }
            }

            return visited;
        }
    }
}
