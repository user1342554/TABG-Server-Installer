using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using TabgInstaller.Core.Model;
using TabgInstaller.Gui.ViewModels;

namespace TabgInstaller.Gui.Tabs
{
    public partial class ConsolePanel : UserControl
    {
        private CollectionViewSource? _logViewSource;
        private bool _autoScroll = true;
        private DispatcherTimer? _searchDebounceTimer;

        private ConsolePanelViewModel? Vm => DataContext as ConsolePanelViewModel;

        public ConsolePanel()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not ConsolePanelViewModel vm) return;

            // Enable cross-thread access to the ObservableCollection (WPF threading concern)
            vm.RegisterCollectionSynchronization((collection, lockObj) =>
                BindingOperations.EnableCollectionSynchronization(
                    (System.Collections.IEnumerable)collection, lockObj));

            // Wire CollectionViewSource
            _logViewSource = (CollectionViewSource)FindResource("LogViewSource");
            _logViewSource.Source = vm.LogEntries;

            // Subscribe to LogEntryReceived for auto-scroll (View concern)
            vm.LogEntryReceived += _ => Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_autoScroll) ScrollToBottom();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WARN] Auto-scroll failed: {ex.Message}");
                }
            });

            // Set up search debounce timer
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchDebounceTimer.Tick += (_, _) =>
            {
                _searchDebounceTimer.Stop();
                if (Vm is { } v) v.SearchText = SearchBox.Text ?? "";
                RefreshFilter();
            };

            // Hook scroll events for auto-scroll detection
            LogListBox.AddHandler(ScrollViewer.ScrollChangedEvent,
                new ScrollChangedEventHandler(OnLogListScrollChanged));
        }

        // ── CollectionViewSource filter ──────────────────────────────────────────

        private void LogViewSource_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is LogEntry entry && Vm is { } vm)
                e.Accepted = vm.FilterLogEntry(entry);
            else
                e.Accepted = false;
        }

        private void RefreshFilter()
        {
            try { _logViewSource?.View?.Refresh(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] Filter refresh failed: {ex.Message}");
            }
        }

        // ── Severity toggles — update VM state then refresh filter ───────────────

        private void SeverityToggle_Click(object sender, RoutedEventArgs e)
        {
            if (Vm is { } vm)
            {
                vm.ShowInfo = ToggleInfo.IsChecked == true;
                vm.ShowWarning = ToggleWarn.IsChecked == true;
                vm.ShowError = ToggleError.IsChecked == true;
            }
            RefreshFilter();
        }

        // ── Search debounce ──────────────────────────────────────────────────────

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            _searchDebounceTimer?.Stop();
            _searchDebounceTimer?.Start();
        }

        // ── Auto-scroll ──────────────────────────────────────────────────────────

        private void OnLogListScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is ScrollViewer sv)
                _autoScroll = sv.VerticalOffset >= sv.ScrollableHeight - 10;
        }

        private void ScrollToBottom()
        {
            if (LogListBox.Items.Count > 0)
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
        }

        // ── Enter key in command box ─────────────────────────────────────────────

        private void TxtCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Vm?.SendCommandCommand.Execute(null);
        }

        // ── Export — needs SaveFileDialog, keep thin handler here ────────────────

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm is not { } vm) return;

            var dialog = new SaveFileDialog
            {
                FileName = $"TABG-Server-Log-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt",
                DefaultExt = ".txt",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var view = _logViewSource?.View;
                if (view != null)
                    vm.ExportLog(dialog.FileName, view);
            }
        }
    }
}
