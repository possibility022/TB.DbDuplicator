using DatabaseCopier.Models;
using DatabaseCopier.Proxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace DatabaseCopier.TUI
{
    class App
    {
        private const string CacheFileName = "cache.cache";

        private DatabaseIO _databaseIO;
        private Dictionary<int, TableNode> _allLoadedTables;
        private Engine _currentEngine;
        private readonly Timer _timer;
        private int _elapsedSeconds;
        private bool _inProgress;

        public List<TableNode> TablesToCopy { get; } = new List<TableNode>();
        public List<TableNode> TablesToIgnore { get; } = new List<TableNode>();

        public string SourceConnectionString { get; set; }
        public string DestinationConnectionString { get; set; }
        public int TimeoutMinutes { get; set; } = 30;

        public bool IsInProgress => _inProgress;
        public bool IsLoaded => _databaseIO != null && TablesToCopy.Any();

        // UI callbacks set by MainView — all are called on a background thread;
        // the view is responsible for dispatching to the UI thread.
        public Action<string> OnLog { get; set; }
        public Action<long, long> OnRowProgress { get; set; }
        public Action<int, int> OnTableProgress { get; set; }
        public Action<int> OnTimerTick { get; set; }
        public Action OnTablesLoaded { get; set; }
        public Action OnCopyFinished { get; set; }

        public App()
        {
            _timer = new Timer(1000);
            _timer.Elapsed += (s, e) =>
            {
                _elapsedSeconds++;
                OnTimerTick?.Invoke(_elapsedSeconds);
            };

            LoadCacheFile();
        }

        private void LoadCacheFile()
        {
            if (File.Exists(CacheFileName))
            {
                try
                {
                    CacheFile.Instance = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(CacheFileName));
                }
                catch
                {
                    // Ignore corrupt cache
                }
            }

            SourceConnectionString = CacheFile.Instance.DatabaseSource.FirstOrDefault()
                ?? "Server=myServerAddress;Database=myDataBase;Trusted_Connection=True;";
            DestinationConnectionString = CacheFile.Instance.DatabaseDestination.FirstOrDefault()
                ?? "Server=myServerAddress;Database=myDataBase;";
        }

        private void SaveCacheFile()
        {
            if (!string.IsNullOrEmpty(SourceConnectionString))
                CacheFile.Instance.DatabaseSource.Add(SourceConnectionString);
            if (!string.IsNullOrEmpty(DestinationConnectionString))
                CacheFile.Instance.DatabaseDestination.Add(DestinationConnectionString);
            CacheFile.Instance.LastIgnoredTables = new HashSet<string>(TablesToIgnore.Select(t => t.TableName));
            try
            {
                File.WriteAllText(CacheFileName, JsonSerializer.Serialize(CacheFile.Instance));
            }
            catch
            {
                // Ignore write errors
            }
        }

        public bool Load()
        {
            if (string.IsNullOrEmpty(SourceConnectionString) || string.IsNullOrEmpty(DestinationConnectionString))
            {
                OnLog?.Invoke("Error: Source and Destination connection strings are required.");
                return false;
            }

            try
            {
                _databaseIO = new DatabaseIO(SourceConnectionString, DestinationConnectionString);
                _allLoadedTables?.Clear();
                TablesToCopy.Clear();
                TablesToIgnore.Clear();

                _allLoadedTables = _databaseIO.GetTables();

                foreach (var kv in _allLoadedTables)
                {
                    if (!CacheFile.Instance.LastIgnoredTables.Contains(kv.Value.TableName))
                        TablesToCopy.Add(kv.Value);
                    else
                        TablesToIgnore.Add(kv.Value);
                }

                OnLog?.Invoke($"Loaded {TablesToCopy.Count} tables to copy, {TablesToIgnore.Count} to ignore.");
                OnTablesLoaded?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Error loading tables: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> Start()
        {
            if (_inProgress)
                return false;

            if (!TablesToCopy.Any())
            {
                OnLog?.Invoke("No tables selected to copy.");
                return false;
            }

            if (_databaseIO == null
                || _databaseIO.TargetConnectionString != DestinationConnectionString
                || _databaseIO.SourceConnectionString != SourceConnectionString)
            {
                OnLog?.Invoke("Please load tables before starting.");
                return false;
            }

            try
            {
                _inProgress = true;
                _elapsedSeconds = 0;
                int tablesTotal = TablesToCopy.Count;

                OnRowProgress?.Invoke(0, 1);
                OnTableProgress?.Invoke(0, tablesTotal);
                _timer.Start();

                var hierarchy = new Hierarchy(_allLoadedTables, _databaseIO.GetForeignKeys());
                var tables = hierarchy.GetTablesInOrder()
                    .Except(TablesToIgnore)
                    .ToList();

                _currentEngine = new Engine(_databaseIO, tables);
                _currentEngine.Timeout = TimeoutMinutes * 60;
                _currentEngine.StartingWith += Engine_StartingWith;
                _currentEngine.RowsCopiedNotify += Engine_RowsCopiedNotify;
                _currentEngine.DoneWith += Engine_DoneWith;
                _currentEngine.Stopped += Engine_Stopped;

                SaveCacheFile();
                await _currentEngine.StartAsync();
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Error during copy: {ex.Message}");
                if (ex.InnerException != null)
                    OnLog?.Invoke($"  Cause: {ex.InnerException.Message}");
                return false;
            }
            finally
            {
                if (_currentEngine != null)
                {
                    _currentEngine.StartingWith -= Engine_StartingWith;
                    _currentEngine.RowsCopiedNotify -= Engine_RowsCopiedNotify;
                    _currentEngine.DoneWith -= Engine_DoneWith;
                    _currentEngine.Stopped -= Engine_Stopped;
                    _currentEngine = null;
                }
                _timer.Stop();
                _inProgress = false;
                OnCopyFinished?.Invoke();
            }
        }

        public void Stop()
        {
            _currentEngine?.Stop();
            OnLog?.Invoke("Stop requested...");
        }

        public void MoveToIgnore(TableNode table)
        {
            if (table == null) return;
            TablesToCopy.Remove(table);
            TablesToIgnore.Add(table);
        }

        public void MoveToCopy(TableNode table)
        {
            if (table == null) return;
            TablesToIgnore.Remove(table);
            TablesToCopy.Add(table);
        }

        private long _rowsTotal = 1;
        private int _tablesCopied;

        private void Engine_StartingWith(object sender, Tuple<string, long> args)
        {
            _rowsTotal = Math.Max(1, args.Item2);
            OnLog?.Invoke($"Starting: {args.Item1} ({args.Item2:N0} rows)");
            OnRowProgress?.Invoke(0, _rowsTotal);
        }

        private void Engine_RowsCopiedNotify(object sender, long e)
        {
            OnRowProgress?.Invoke(e, _rowsTotal);
        }

        private void Engine_DoneWith(object sender, string tableName)
        {
            if (tableName == null)
            {
                OnLog?.Invoke("All tables copied successfully.");
                return;
            }
            if (TablesToCopy.Any(t => t.FullTableName == tableName))
                _tablesCopied++;
            OnRowProgress?.Invoke(_rowsTotal, _rowsTotal);
            OnTableProgress?.Invoke(_tablesCopied, TablesToCopy.Count);
            OnLog?.Invoke($"Done: {tableName}");
        }

        private void Engine_Stopped(object sender, EventArgs e)
        {
            OnLog?.Invoke("Operation stopped by user.");
        }
    }
}
