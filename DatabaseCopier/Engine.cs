using DatabaseCopier.Models;
using DatabaseCopier.Proxy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DatabaseCopier
{
    class Engine
    {
        private readonly DatabaseIO _databaseIO;
        private readonly IEnumerable<TableNode> _tablesToCopy;

        public event EventHandler<long> RowsCopiedNotify;
        public event EventHandler<Tuple<string, long>> StartingWith;
        public event EventHandler<string> DoneWith;

        public Engine(DatabaseIO databaseIO, IEnumerable<TableNode> tablesToCopy)
        {
            _databaseIO = databaseIO;
            _databaseIO.ProgressEvent = ProgressEvent;
            this._tablesToCopy = tablesToCopy;
        }

        public async Task<TimeSpan> StartAsync()
        {
            var t = await Task.Factory.StartNew(Start);
            return t;
        }

        public TimeSpan Start()
        {
            var s = new Stopwatch();
            s.Start();
            foreach (var t in _tablesToCopy)
            {
                var rows = _databaseIO.GetRows(t);
                StartingWith?.Invoke(this, new Tuple<string, long>(t.FullTableName, rows));
                _databaseIO.CopyTable(t);
                //System.Threading.Thread.Sleep(5000); // simulate long operation
                DoneWith?.Invoke(this, t.FullTableName);
            }
            DoneWith?.Invoke(this, null);
            s.Stop();
            return s.Elapsed;
        }

        public void ProgressEvent (object sender, System.Data.SqlClient.SqlRowsCopiedEventArgs args)
        {
            RowsCopiedNotify?.Invoke(this, args.RowsCopied);
        }
    }
}
