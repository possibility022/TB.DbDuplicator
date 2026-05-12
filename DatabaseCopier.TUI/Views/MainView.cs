using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DatabaseCopier.TUI.Views
{
    class MainView : Window
    {
        private readonly App _app;

        // Connection / config
        private TextField _sourceField;
        private TextField _destField;
        private TextField _timeoutField;
        private Button _loadBtn;
        private Button _startBtn;
        private Button _stopBtn;

        // Table lists
        private ListView _copyList;
        private ListView _ignoreList;
        private Button _moveToIgnoreBtn;
        private Button _moveToCopyBtn;

        // Progress
        private ProgressBar _progressBar;
        private Label _rowsLabel;
        private Label _tablesLabel;
        private Label _timeLabel;

        // Log — each entry is one line shown in a ListView for built-in scroll support
        private ListView _logView;
        private readonly ObservableCollection<string> _logItems = new ObservableCollection<string>();
        private const int MaxLogLines = 500;

        public MainView()
        {
            Title = $"DatabaseCopier ({Application.GetDefaultKey(Command.Quit)} to quit)";
            _app = new App();
            SetupCallbacks();
            BuildLayout();
            RefreshButtonStates();
        }

        private void SetupCallbacks()
        {
            // All callbacks are fired from background threads; use App.Invoke to
            // marshal back to the UI thread (App property is set after Run starts).
            _app.OnLog = msg => App?.Invoke(() => AppendLog(msg));
            _app.OnRowProgress = (copied, total) => App?.Invoke(() => UpdateRowProgress(copied, total));
            _app.OnTableProgress = (done, total) => App?.Invoke(() => UpdateTableProgress(done, total));
            _app.OnTimerTick = seconds => App?.Invoke(() => UpdateTimer(seconds));
            _app.OnTablesLoaded = () => App?.Invoke(() => { RefreshTableLists(); RefreshButtonStates(); });
            _app.OnCopyFinished = () => App?.Invoke(RefreshButtonStates);
        }

        private void BuildLayout()
        {
            // ── Source row ────────────────────────────────────────────────────
            var srcLabel = new Label { Text = "Source:  ", X = 0, Y = 0 };
            _sourceField = new TextField
            {
                Text = _app.SourceConnectionString ?? string.Empty,
                X = Pos.Right(srcLabel),
                Y = 0,
                Width = Dim.Fill()
            };

            // ── Destination row ────────────────────────────────────────────────
            var dstLabel = new Label { Text = "Dest:    ", X = 0, Y = Pos.Bottom(srcLabel) };
            _destField = new TextField
            {
                Text = _app.DestinationConnectionString ?? string.Empty,
                X = Pos.Right(dstLabel),
                Y = Pos.Bottom(srcLabel),
                Width = Dim.Fill()
            };

            // ── Controls row ──────────────────────────────────────────────────
            var timeoutLabel = new Label { Text = "Timeout (min):", X = 0, Y = Pos.Bottom(dstLabel) };
            _timeoutField = new TextField
            {
                Text = _app.TimeoutMinutes.ToString(),
                X = Pos.Right(timeoutLabel) + 1,
                Y = Pos.Bottom(dstLabel),
                Width = 5
            };
            _loadBtn = new Button { Text = "Load", X = Pos.Right(_timeoutField) + 2, Y = Pos.Bottom(dstLabel) };
            _startBtn = new Button { Text = "Start", X = Pos.Right(_loadBtn) + 1, Y = Pos.Bottom(dstLabel) };
            _stopBtn = new Button { Text = "Stop", X = Pos.Right(_startBtn) + 1, Y = Pos.Bottom(dstLabel) };

            _loadBtn.Accepting += (s, e) => { OnLoad(); e.Handled = true; };
            _startBtn.Accepting += (s, e) => { OnStart(); e.Handled = true; };
            _stopBtn.Accepting += (s, e) => { OnStop(); e.Handled = true; };

            // ── Table lists ───────────────────────────────────────────────────
            var tableY = Pos.Bottom(timeoutLabel);

            var copyFrame = new FrameView
            {
                Title = "Tables to Copy",
                X = 0,
                Y = tableY,
                Width = Dim.Percent(50),
                Height = Dim.Percent(40)
            };
            _copyList = new ListView
            {
                X = 0, Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1
            };
            _moveToIgnoreBtn = new Button
            {
                Text = "→ Move to Ignore",
                X = Pos.Center(),
                Y = Pos.AnchorEnd(1)
            };
            _moveToIgnoreBtn.Accepting += (s, e) => { OnMoveToIgnore(); e.Handled = true; };
            copyFrame.Add(_copyList, _moveToIgnoreBtn);

            var ignoreFrame = new FrameView
            {
                Title = "Tables to Ignore",
                X = Pos.Right(copyFrame),
                Y = tableY,
                Width = Dim.Fill(),
                Height = Dim.Percent(40)
            };
            _ignoreList = new ListView
            {
                X = 0, Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1
            };
            _moveToCopyBtn = new Button
            {
                Text = "← Move to Copy",
                X = Pos.Center(),
                Y = Pos.AnchorEnd(1)
            };
            _moveToCopyBtn.Accepting += (s, e) => { OnMoveToCopy(); e.Handled = true; };
            ignoreFrame.Add(_ignoreList, _moveToCopyBtn);

            // ── Progress frame ────────────────────────────────────────────────
            var progressFrame = new FrameView
            {
                Title = "Progress",
                X = 0,
                Y = Pos.Bottom(copyFrame),
                Width = Dim.Fill(),
                Height = 5
            };
            _progressBar = new ProgressBar { X = 0, Y = 0, Width = Dim.Fill(), Fraction = 0f };
            _rowsLabel = new Label { Text = "Rows: 0 / 0", X = 0, Y = 1 };
            _tablesLabel = new Label { Text = "Tables: 0 / 0", X = 20, Y = 1 };
            _timeLabel = new Label { Text = "Elapsed: 00:00:00", X = 40, Y = 1 };
            progressFrame.Add(_progressBar, _rowsLabel, _tablesLabel, _timeLabel);

            // ── Log frame (ListView for built-in keyboard scroll support) ─────
            var logFrame = new FrameView
            {
                Title = "Log",
                X = 0,
                Y = Pos.Bottom(progressFrame),
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            _logView = new ListView
            {
                X = 0, Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            _logView.SetSource(_logItems);
            logFrame.Add(_logView);

            Add(srcLabel, _sourceField,
                dstLabel, _destField,
                timeoutLabel, _timeoutField, _loadBtn, _startBtn, _stopBtn,
                copyFrame, ignoreFrame,
                progressFrame,
                logFrame);
        }

        private void OnLoad()
        {
            _app.SourceConnectionString = _sourceField.Text?.ToString() ?? string.Empty;
            _app.DestinationConnectionString = _destField.Text?.ToString() ?? string.Empty;
            if (int.TryParse(_timeoutField.Text?.ToString(), out var t))
                _app.TimeoutMinutes = t;
            AppendLog("Loading tables...");
            _app.Load();
        }

        private void OnStart()
        {
            if (_app.IsInProgress) return;
            _app.SourceConnectionString = _sourceField.Text?.ToString() ?? string.Empty;
            _app.DestinationConnectionString = _destField.Text?.ToString() ?? string.Empty;
            if (int.TryParse(_timeoutField.Text?.ToString(), out var t))
                _app.TimeoutMinutes = t;
            RefreshButtonStates();
            Task.Run(async () => await _app.Start());
        }

        private void OnStop()
        {
            _app.Stop();
        }

        private void OnMoveToIgnore()
        {
            int idx = _copyList.SelectedItem ?? -1;
            if (idx < 0 || idx >= _app.TablesToCopy.Count) return;
            _app.MoveToIgnore(_app.TablesToCopy[idx]);
            RefreshTableLists();
        }

        private void OnMoveToCopy()
        {
            int idx = _ignoreList.SelectedItem ?? -1;
            if (idx < 0 || idx >= _app.TablesToIgnore.Count) return;
            _app.MoveToCopy(_app.TablesToIgnore[idx]);
            RefreshTableLists();
        }

        private void RefreshTableLists()
        {
            var copyItems = new ObservableCollection<string>();
            foreach (var t in _app.TablesToCopy) copyItems.Add(t.ToString());
            _copyList.SetSource(copyItems);

            var ignoreItems = new ObservableCollection<string>();
            foreach (var t in _app.TablesToIgnore) ignoreItems.Add(t.ToString());
            _ignoreList.SetSource(ignoreItems);
        }

        private void RefreshButtonStates()
        {
            _loadBtn.Enabled = !_app.IsInProgress;
            _startBtn.Enabled = !_app.IsInProgress && _app.IsLoaded;
            _stopBtn.Enabled = _app.IsInProgress;
        }

        private void AppendLog(string message)
        {
            _logItems.Add(message);
            if (_logItems.Count > MaxLogLines)
                _logItems.RemoveAt(0);
            // Auto-scroll to the newest entry
            _logView.SelectedItem = _logItems.Count - 1;
        }

        private void UpdateRowProgress(long copied, long total)
        {
            _progressBar.Fraction = total > 0 ? Math.Min(1f, (float)copied / total) : 0f;
            _rowsLabel.Text = $"Rows: {copied:N0} / {total:N0}";
        }

        private void UpdateTableProgress(int done, int total)
        {
            _tablesLabel.Text = $"Tables: {done} / {total}";
        }

        private void UpdateTimer(int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            _timeLabel.Text = $"Elapsed: {ts:hh\\:mm\\:ss}";
        }
    }
}
