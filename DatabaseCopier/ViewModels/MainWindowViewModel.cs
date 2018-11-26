using DatabaseCopier.Models;
using DatabaseCopier.Proxy;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseCopier.ViewModels
{
    class MainWindowViewModel : BindableBase
    {
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

        private string _errorText;
        public string ErrorText
        {
            get { return _errorText; }
            set { SetProperty(ref _errorText, value); }
        }



        private string _databaseSource;
        public string DatabaseSource
        {
            get { return _databaseSource; }
            set { SetProperty(ref _databaseSource, value); }
        }

        private string _databaseDestination;
        public string DatabaseDestination
        {
            get { return _databaseDestination; }
            set { SetProperty(ref _databaseDestination, value); }
        }

        public MainWindowViewModel()
        {
            TablesToCopy = new ObservableCollection<TableNode>();
            TablesToIgnore = new ObservableCollection<TableNode>();

            for (int i = 0; i < 10; i++)
            {
                TablesToCopy.Add(new TableNode($"TableName{i}", i));
            }
        }

        public void Load()
        {
            _databaseIO = new DatabaseIO(DatabaseSource, DatabaseDestination);
            allLoadedTables = _databaseIO.GetTables();
            foreach (var t in allLoadedTables)
                TablesToCopy.Add(t.Value);
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

        internal void Start()
        {
            try
            {
                ErrorText = string.Empty;
                var hierarchy = new Hierarchy(allLoadedTables, _databaseIO.GetForeignKeys());
                var tables = hierarchy.GetTablesInOrder();
                var ignore = new HashSet<TableNode>(_tablesToIgnore);

                foreach (var t in tables)
                {
                    if (ignore.Contains(t))
                        _databaseIO.CopyTable(t);
                }
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();

                sb.AppendLine(ex.Message);
                sb.AppendLine(ex.StackTrace);

                while (ex.InnerException != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("INNER EXCEPTION:");
                    sb.AppendLine();
                    ex = ex.InnerException;
                    sb.AppendLine(ex.Message);
                    sb.AppendLine(ex.StackTrace);
                }

                ErrorText = sb.ToString();

            }
        }

    }
}
