using DatabaseCopier.Commands;
using DatabaseCopier.Models;
using DatabaseCopier.Proxy;
using Newtonsoft.Json;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;

namespace DatabaseCopier.ViewModels
{
    class MainWindowViewModel : BindableBase
    {
        private const string fileName = "cache.cache";

        private bool _inProgress = false;

        private ObservableCollection<TableNode> _tablesToCopy;
        private ObservableCollection<TableNode> _tablesToIgnore;

        public ObservableCollection<TableNode> TablesToCopy
        {
            get => _tablesToCopy;
            set => SetProperty(ref _tablesToCopy, value);
        }

        public ObservableCollection<TableNode> TablesToIgnore { get => _tablesToIgnore; set => SetProperty(ref _tablesToIgnore, value); }

        private TableNode _selectedInToIgnore;
        public TableNode SelectedInToIgnore
        {
            get { return _selectedInToIgnore; }
            set { SetProperty(ref _selectedInToIgnore, value); }
        }

        private TableNode _selectedInToCopyList;
        public TableNode SelectedInToCopyList
        {
            get { return _selectedInToCopyList; }
            set { SetProperty(ref _selectedInToCopyList, value); }
        }

        DatabaseIO _databaseIO;
        Dictionary<int, TableNode> allLoadedTables;

        private string _infoText;
        public string InfoText
        {
            get { return _infoText; }
            set { SetProperty(ref _infoText, value); }
        }

        private StringBuilder _infoMessageBuffer;


        private string _databaseSource;
        public string DatabaseSource
        {
            get { return _databaseSource; }
            set { SetProperty(ref _databaseSource, value); }
        }

        private ObservableCollection<string> _databaseSourceList;
        public ObservableCollection<string> DatabaseSourceList
        {
            get { return _databaseSourceList; }
            private set { SetProperty(ref _databaseSourceList, value); }
        }

        private ObservableCollection<string> _databaseDestinationList;
        public ObservableCollection<string> DatabaseDestinationList
        {
            get { return _databaseDestinationList; }
            private set { SetProperty(ref _databaseDestinationList, value); }
        }

        private string _databaseDestination;
        public string DatabaseDestination
        {
            get { return _databaseDestination; }
            set { SetProperty(ref _databaseDestination, value); }
        }

        private string _timeout = 30.ToString();
        public string Timeout
        {
            get => _timeout.ToString();
            set => SetProperty(ref _timeout, value);
        }

        private ICommand _startCommand;
        public ICommand StartCommand
        {
            get { return _startCommand; }
            set { SetProperty(ref _startCommand, value); }
        }

        private ICommand _loadCommand;
        public ICommand LoadCommand
        {
            get { return _loadCommand; }
            set { SetProperty(ref _loadCommand, value); }
        }

        private int _tablesCopied;
        private int _allTablesToCopy;
        private long _progressBar = 0;
        private long _rowsToCopy = 1;
        private int _timeSecounds;

        public int TablesCopied { get => _tablesCopied; set => SetProperty(ref _tablesCopied, value); }
        public int AllTablesToCopy { get => _allTablesToCopy; private set => SetProperty(ref _allTablesToCopy, value); }
        public long ProgressBar { get => _progressBar; private set => SetProperty(ref _progressBar, value); }
        public long RowsToCopy { get => _rowsToCopy; private set => SetProperty(ref _rowsToCopy, value); }
        public int TimeSecounds { get => _timeSecounds; set => SetProperty(ref _timeSecounds, value); }

        Timer _timer;


        public MainWindowViewModel()
        {
            TablesToCopy = new ObservableCollection<TableNode>();
            TablesToIgnore = new ObservableCollection<TableNode>();
            DatabaseDestinationList = new ObservableCollection<string>();
            DatabaseSourceList = new ObservableCollection<string>();

            StartCommand = new RelayCommand<Task<bool>>(Start, CanStart);
            LoadCommand = new RelayCommand<bool>(Load, CanLoad);

            _infoMessageBuffer = new StringBuilder();
            _timer = new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;
            LoadCacheFile();
        }

        private bool CanStart()
        {
            return
                DatabaseDestination == _databaseIO?.TargetConnectionString
                && TablesToCopy.Any()
                && !_inProgress;
        }

        private bool CanLoad()
        {
            return !_inProgress &&
                !string.IsNullOrEmpty(DatabaseSource) &&
                !string.IsNullOrEmpty(DatabaseDestination);
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeSecounds += 1;
        }

        public bool Load()
        {
            _databaseIO = new DatabaseIO(DatabaseSource, DatabaseDestination);
            InfoText = string.Empty;

            try
            {
                allLoadedTables?.Clear();
                TablesToIgnore?.Clear();
                TablesToCopy?.Clear();

                allLoadedTables = _databaseIO.GetTables();

                foreach (var t in allLoadedTables)
                {
                    if (!CacheFile.Instance.LastIgnoredTables.Contains(t.Value.TableName))
                        TablesToCopy.Add(t.Value);
                    else
                        TablesToIgnore.Add(t.Value);
                }
                return true;
            }
            catch (Exception ex)
            {
                _infoMessageBuffer.AppendLine(ex.Message);
                _infoMessageBuffer.AppendLine(ex.StackTrace);
                InfoText = _infoMessageBuffer.ToString();
                return false;
            }
        }

        public void MoveToIgnore()
        {
            if (SelectedInToCopyList == null)
                return;
            var table = SelectedInToCopyList;
            _tablesToCopy.Remove(table);
            _tablesToIgnore.Add(table);
        }

        public void MoveToToCopyList()
        {
            if (SelectedInToIgnore == null)
                return;
            var table = SelectedInToIgnore;
            _tablesToCopy.Add(table);
            _tablesToIgnore.Remove(table);
        }

        private void UpdateCacheFile()
        {
            CacheFile.Instance.DatabaseDestination.Add(DatabaseDestination);
            CacheFile.Instance.DatabaseSource.Add(DatabaseSource);
            CacheFile.Instance.LastIgnoredTables = new HashSet<string>(TablesToIgnore.Select(r => r.TableName));

            File.WriteAllText(fileName, JsonConvert.SerializeObject(CacheFile.Instance));
        }

        private void LoadCacheFile()
        {
            if (File.Exists(fileName))
            {
                CacheFile.Instance = JsonConvert.DeserializeObject<CacheFile>(File.ReadAllText(fileName));
                DatabaseDestinationList = new ObservableCollection<string>(CacheFile.Instance.DatabaseDestination);
                DatabaseSourceList = new ObservableCollection<string>(CacheFile.Instance.DatabaseSource);
            }

            DatabaseSourceList.Add("Server=myServerAddress;Database=myDataBase;Trusted_Connection=True;");
            DatabaseDestinationList.Add("Server=myServerAddress;Database=myDataBase;");
        }

        internal async Task<bool> Start()
        {

            if (int.TryParse(Timeout, out var timeout) == false)
            {
                MessageBox.Show("Cannot parse {Timeout} to int. Please set correct value. Numeric value in minutes.");
                return false;
            }

            Engine engine = null;
            try
            {
                TimeSecounds = 0;
                _inProgress = true;
                InfoText = string.Empty;
                TablesCopied = 0;
                AllTablesToCopy = TablesToCopy.Count;

                _timer.Start();

                _infoMessageBuffer.Clear();

                if (!_databaseSourceList.Contains(DatabaseSource) && !string.IsNullOrEmpty(DatabaseSource))
                    _databaseSourceList.Add(DatabaseSource);

                if (!_databaseDestinationList.Contains(DatabaseDestination) && string.IsNullOrEmpty(DatabaseDestination))
                    _databaseDestinationList.Add(DatabaseDestination);

                var hierarchy = new Hierarchy(allLoadedTables, _databaseIO.GetForeignKeys());
                var tables = hierarchy
                    .GetTablesInOrder()
                    .Except(_tablesToIgnore)
                    .ToList();

                engine = new Engine(_databaseIO, tables);
                engine.Timeout = timeout * 60;
                engine.StartingWith += Engine_StartingWith;
                engine.RowsCopiedNotify += Engine_RowsCopiedNotify;
                engine.DoneWith += Engine_DoneWith;

                var task = engine.StartAsync();
                UpdateCacheFile();

                await task;

                return true;
            }
            catch (Exception ex)
            {
                _infoMessageBuffer.AppendLine(ex.Message);
                _infoMessageBuffer.AppendLine(ex.StackTrace);

                while (ex.InnerException != null)
                {
                    _infoMessageBuffer.AppendLine();
                    _infoMessageBuffer.AppendLine("INNER EXCEPTION:");
                    _infoMessageBuffer.AppendLine();
                    ex = ex.InnerException;
                    _infoMessageBuffer.AppendLine(ex.Message);
                    _infoMessageBuffer.AppendLine(ex.StackTrace);
                }

                InfoText = _infoMessageBuffer.ToString();
                return false;
            }
            finally
            {
                if (engine != null)
                {
                    engine.StartingWith -= Engine_StartingWith;
                    engine.RowsCopiedNotify -= Engine_RowsCopiedNotify;
                }

                _timer.Stop();
                _inProgress = false;
            }
        }

        private void Engine_DoneWith(object sender, string e)
        {
            if (TablesToCopy.Any(t => t.FullTableName == e))
                TablesCopied += 1;

            ProgressBar = RowsToCopy;
        }

        private void Engine_RowsCopiedNotify(object sender, long e)
        {
            ProgressBar = e;
        }

        private void Engine_StartingWith(object sender, Tuple<string, long> args)
        {
            ProgressBar = 0;
            RowsToCopy = args.Item2;
            _infoMessageBuffer.AppendLine($"Starting with: {args.Item1}. Rows: {args.Item2}");
            InfoText = _infoMessageBuffer.ToString();
        }
    }
}
