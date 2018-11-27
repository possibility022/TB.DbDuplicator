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

namespace DatabaseCopier.ViewModels
{
    class MainWindowViewModel : BindableBase
    {
        private const string fileName = "cache.cache";

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
            set { SetProperty(ref _databaseSourceList, value); }
        }

        private ObservableCollection<string> _databaseDestinationList;
        public ObservableCollection<string> DatabaseDestinationList
        {
            get { return _databaseDestinationList; }
            set { SetProperty(ref _databaseDestinationList, value); }
        }

        private string _databaseDestination;
        public string DatabaseDestination
        {
            get { return _databaseDestination; }
            set { SetProperty(ref _databaseDestination, value); }
        }

        private bool _startEnabled = false;
        public bool StartEnabled
        {
            get => _startEnabled;
            set => SetProperty(ref _startEnabled, value);
        }

        public MainWindowViewModel()
        {
            TablesToCopy = new ObservableCollection<TableNode>();
            TablesToIgnore = new ObservableCollection<TableNode>();
            DatabaseDestinationList = new ObservableCollection<string>();
            DatabaseSourceList = new ObservableCollection<string>();
            LoadCacheFile();
        }

        public void Load()
        {

            for (int i = 0; i < 10; i++)
            {
                TablesToCopy.Add(new TableNode($"TableName{i}", i));
            }

            _databaseIO = new DatabaseIO(DatabaseSource, DatabaseDestination);
            try
            {
                allLoadedTables = _databaseIO.GetTables();
            }
            catch (Exception ex)
            {
                _infoMessageBuffer.AppendLine(ex.Message);
                _infoMessageBuffer.AppendLine(ex.StackTrace);
            }

            foreach (var t in allLoadedTables)
            {
                if (!CacheFile.Instance.LastIgnoredTables.Contains(t.Value.TableName))
                    TablesToCopy.Add(t.Value);
                else
                    TablesToIgnore.Add(t.Value);
            }

            StartEnabled = TablesToCopy.Any();
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
                _databaseDestinationList = new ObservableCollection<string>(CacheFile.Instance.DatabaseDestination);
                _databaseSourceList = new ObservableCollection<string>(CacheFile.Instance.DatabaseSource);
            }
        }

        internal async Task<bool> Start()
        {
            Engine engine = null;
            try
            {
                InfoText = string.Empty;
                _infoMessageBuffer = new StringBuilder();

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
                engine.InforationEvent += Engine_OnTableCopied;

                var task = engine.StartAsync();
                UpdateCacheFile();

                await task;

                return true;
            }
            catch (Exception ex)
            {
                if (engine != null)
                    engine.InforationEvent -= Engine_OnTableCopied;

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
        }

        private void Engine_OnTableCopied(object sender, string e)
        {
            _infoMessageBuffer.AppendLine(e);
            InfoText = _infoMessageBuffer.ToString();
        }
    }
}
