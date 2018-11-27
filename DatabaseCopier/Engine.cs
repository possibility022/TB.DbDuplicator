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

        public event EventHandler<string> InforationEvent;

        public Engine(DatabaseIO databaseIO, IEnumerable<TableNode> tablesToCopy)
        {
            _databaseIO = databaseIO;
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
                InforationEvent?.Invoke(this, $"Starting with {t.TableName}. Rows: {rows}");
                _databaseIO.CopyTable(t);
                //System.Threading.Thread.Sleep(5000); // simulate long operation
                InforationEvent?.Invoke(this, $"Coping {t.TableName} done.");
            }
            s.Stop();
            return s.Elapsed;
        }
    }
}
