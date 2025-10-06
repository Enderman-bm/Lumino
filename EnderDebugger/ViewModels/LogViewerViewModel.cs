using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Media;
using EnderDebugger.Views;
using EnderDebugger.Services;

namespace EnderDebugger.ViewModels
{
    public partial class LogViewerViewModel : ViewModelBase
    {
        private readonly object _lock = new object();
        private readonly Queue<LogEntry> _pendingLogs = new();
        private DispatcherTimer? _updateTimer;
        private LogTransportService? _transportService; // IPC 接收服务
        private const int MaxLogsToKeep = 10000; // 限制最大日志数量
        private const int BatchUpdateInterval = 100; // 批量更新间隔(毫秒)

        [ObservableProperty]
        private ObservableCollection<LogEntry> logs = new();

        [ObservableProperty]
        private ObservableCollection<LogEntry> filteredLogs = new();

        [ObservableProperty]
        private string selectedLogLevel = "全部";

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool autoScrollToBottom = true;

        [ObservableProperty]
        private string statusText = "日志查看器已启动";

        public Window? Window { get; set; }

        public List<string> LogLevels { get; } = new() { "全部", "Debug", "Info", "Warn", "Error", "Fatal" };

        public string TotalLogsText => $"总日志: {Logs.Count}";
        public string FilteredLogsText => $"显示: {FilteredLogs.Count}";

        public LogViewerViewModel()
        {
            // 启动 IPC 接收服务
            _transportService = new LogTransportService();
            _transportService.LogReceived += OnLogEntryAdded;
            _transportService.StartServer();

            // 订阅本地日志事件(用于 EnderDebugger 自己的日志)
            EnderLogger.Instance.LogEntryAdded += OnLogEntryAdded;

            // 初始化批量更新定时器
            InitializeUpdateTimer();

            // 初始化过滤
            UpdateFilteredLogs();

            // 添加一些测试日志
            AddTestLogs();
        }

        private void InitializeUpdateTimer()
        {
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(BatchUpdateInterval)
            };
            _updateTimer.Tick += OnUpdateTimerTick;
            _updateTimer.Start();
        }

        private void AddTestLogs()
        {
            EnderLogger.Instance.EnableDebugMode("all");
            EnderLogger.Instance.Debug("ApplicationStarted", "日志查看器应用程序已启动");
            EnderLogger.Instance.Info("UILoaded", "用户界面已加载完成");
            EnderLogger.Instance.Info("LogViewerInitialized", "日志查看器初始化完成");
            EnderLogger.Instance.Debug("EventSubscription", "已订阅日志事件通知");
            EnderLogger.Instance.Info("Ready", "系统准备就绪,等待日志输入");
            
            StatusText = $"日志查看器已启动 - {DateTime.Now:HH:mm:ss}";
        }

        partial void OnSelectedLogLevelChanged(string value)
        {
            UpdateFilteredLogs();
        }

        partial void OnSearchTextChanged(string value)
        {
            UpdateFilteredLogs();
        }

        private void OnLogEntryAdded(LogEntry entry)
        {
            // 将日志添加到待处理队列,而不是立即更新UI
            lock (_lock)
            {
                // 如果收到的是通过 IPC 传输的日志,需要从颜色字符串转换为Color对象
                if (!string.IsNullOrEmpty(entry.LevelColorString))
                {
                    entry.LevelColor = Color.Parse(entry.LevelColorString);
                    entry.BorderColor = Color.Parse(entry.BorderColorString);
                }
                // 如果是本地日志(已经是字符串),也需要转换
                else if (entry.LevelColor is string colorString)
                {
                    entry.LevelColor = Color.Parse(colorString);
                }
                
                if (entry.BorderColor is string borderColorString)
                {
                    entry.BorderColor = Color.Parse(borderColorString);
                }

                _pendingLogs.Enqueue(entry);
            }
        }

        private void OnUpdateTimerTick(object? sender, EventArgs e)
        {
            // 批量处理待处理的日志
            List<LogEntry> logsToAdd = new();
            
            lock (_lock)
            {
                // 一次性取出所有待处理的日志
                while (_pendingLogs.Count > 0)
                {
                    logsToAdd.Add(_pendingLogs.Dequeue());
                }
            }

            if (logsToAdd.Count == 0)
                return;

            // 在UI线程批量添加
            Dispatcher.UIThread.Post(() =>
            {
                lock (_lock)
                {
                    // 批量添加到集合
                    foreach (var entry in logsToAdd)
                    {
                        Logs.Add(entry);
                        
                        // 限制日志数量,移除旧日志
                        if (Logs.Count > MaxLogsToKeep)
                        {
                            Logs.RemoveAt(0);
                        }
                    }

                    // 只在批量添加后更新一次过滤
                    UpdateFilteredLogs();

                    // 更新状态文本(只显示最后一条)
                    var lastEntry = logsToAdd[logsToAdd.Count - 1];
                    StatusText = $"最新日志: [{lastEntry.Level}] {lastEntry.Message.Substring(0, Math.Min(50, lastEntry.Message.Length))}...";

                    // 只在批量更新后滚动一次
                    if (AutoScrollToBottom)
                    {
                        ScrollToBottom();
                    }
                }
            }, DispatcherPriority.Background); // 使用Background优先级,避免阻塞UI
        }

        private void UpdateFilteredLogs()
        {
            lock (_lock)
            {
                var filtered = Logs.AsEnumerable();

                // 按级别过滤
                if (SelectedLogLevel != "全部")
                {
                    filtered = filtered.Where(log => log.Level.Equals(SelectedLogLevel, StringComparison.OrdinalIgnoreCase));
                }

                // 按搜索文本过滤
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var search = SearchText.ToLower();
                    filtered = filtered.Where(log =>
                        log.Message.ToLower().Contains(search) ||
                        log.Source.ToLower().Contains(search) ||
                        log.EventType.ToLower().Contains(search));
                }

                // 限制显示数量,提高性能
                var filteredList = filtered.Take(5000).ToList();

                FilteredLogs.Clear();
                foreach (var log in filteredList)
                {
                    FilteredLogs.Add(log);
                }

                OnPropertyChanged(nameof(TotalLogsText));
                OnPropertyChanged(nameof(FilteredLogsText));
            }
        }

        private void ScrollToBottom()
        {
            if (Window is LogViewerWindow logViewerWindow)
            {
                var scrollViewer = logViewerWindow.FindControl<ScrollViewer>("LogScrollViewer");
                if (scrollViewer != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        scrollViewer.ScrollToEnd();
                    });
                }
            }
        }

        [RelayCommand]
        private void ClearLogs()
        {
            lock (_lock)
            {
                Logs.Clear();
                FilteredLogs.Clear();
                StatusText = "日志已清空";
                OnPropertyChanged(nameof(TotalLogsText));
                OnPropertyChanged(nameof(FilteredLogsText));
            }
        }

        [RelayCommand]
        private async Task SaveLogs()
        {
            if (Window == null) return;

            var topLevel = TopLevel.GetTopLevel(Window);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "保存日志文件",
                DefaultExtension = "txt",
                SuggestedFileName = $"EnderDebugger_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                FileTypeChoices = new List<Avalonia.Platform.Storage.FilePickerFileType>
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("文本文件")
                    {
                        Patterns = new[] { "*.txt" },
                        MimeTypes = new[] { "text/plain" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("所有文件")
                    {
                        Patterns = new[] { "*" },
                        MimeTypes = new[] { "application/octet-stream" }
                    }
                }
            });

            if (file != null)
            {
                try
                {
                    var content = string.Join(Environment.NewLine, FilteredLogs.Select(log => $"[{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}][{log.Source}][{log.EventType}]{log.Message}"));
                    await File.WriteAllTextAsync(file.Path.LocalPath, content);
                    StatusText = $"日志已保存到: {Path.GetFileName(file.Name)}";
                }
                catch (Exception ex)
                {
                    StatusText = $"保存失败: {ex.Message}";
                }
            }
        }
    }
}
