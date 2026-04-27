using DatabaseCopier.Models;
using DatabaseCopier.Proxy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseCopier
{
    class Engine
    {
        private readonly DatabaseIO _databaseIO;
        private readonly IEnumerable<TableNode> _tablesToCopy;
        private CancellationTokenSource _cancellationTokenSource;

        public int Timeout;

        public event EventHandler<long> RowsCopiedNotify;
        public event EventHandler<Tuple<string, long>> StartingWith;
        public event EventHandler<string> DoneWith;
        public event EventHandler Stopped;

        public Engine(DatabaseIO databaseIO, IEnumerable<TableNode> tablesToCopy)
        {
            _databaseIO = databaseIO;
            _databaseIO.ProgressEvent = ProgressEvent;
            this._tablesToCopy = tablesToCopy;
        }

        public async Task<TimeSpan> StartAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var t = await Task.Factory.StartNew(() => Start(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            return t;
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }

        public TimeSpan Start(CancellationToken cancellationToken)
        {
            var s = new Stopwatch();
            s.Start();
            try
            {
                foreach (var t in _tablesToCopy)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Stopped?.Invoke(this, EventArgs.Empty);
                        break;
                    }

                    _databaseIO.CreateTableIfNotExists(t);
                    var rows = _databaseIO.GetRows(t);
                    StartingWith?.Invoke(this, new Tuple<string, long>(t.FullTableName, rows));
                    _databaseIO.TimeOut = Timeout;
                    _databaseIO.CopyTable(t, cancellationToken);
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Stopped?.Invoke(this, EventArgs.Empty);
                        break;
                    }
                    
                    DoneWith?.Invoke(this, t.FullTableName);
                }
                
                if (!cancellationToken.IsCancellationRequested)
                    DoneWith?.Invoke(this, null);
            }
            catch (OperationCanceledException)
            {
                Stopped?.Invoke(this, EventArgs.Empty);
            }
            s.Stop();
            return s.Elapsed;
        }

        public void ProgressEvent (object sender, Microsoft.Data.SqlClient.SqlRowsCopiedEventArgs args)
        {
            RowsCopiedNotify?.Invoke(this, args.RowsCopied);
        }
    }
}
