using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using EnderDebugger;

namespace Lumino.ViewModels
{
    /// <summary>
    /// 日志查看器ViewModel
    /// </summary>
    public class LogViewerViewModel : ViewModelBase
    {
        private string _searchText = string.Empty;
        private bool _followFile = true;
        private int _maxLines = 1000;
        private bool _showTimestamp = true;
        private bool _autoScrollToEnd = true;
        private string _statusMessage = "就绪";
        private LogLevelFilter _selectedLevel = LogLevelFilter.All;
        private string _logFilePath = string.Empty;
        private FileSystemWatcher? _fileWatcher;
        private long _lastPosition = 0;
        
        public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new ObservableCollection<LogEntryViewModel>();
        
        private ICommand? _refreshLogsCommand;
        private ICommand? _clearLogsCommand;
        private ICommand? _exportLogsCommand;
        private ICommand? _followFileCommand;
        
        /// <summary>
        /// 当需要自动滚动到末尾时触发
        /// </summary>
        public event EventHandler? AutoScrollToEndRequested;
        
        /// <summary>
        /// 搜索文本
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterLogEntries();
                }
            }
        }
        
        /// <summary>
        /// 是否跟踪文件变化
        /// </summary>
        public bool FollowFile
        {
            get => _followFile;
            set => SetProperty(ref _followFile, value);
        }
        
        /// <summary>
        /// 最大显示行数
        /// </summary>
        public int MaxLines
        {
            get => _maxLines;
            set
            {
                if (SetProperty(ref _maxLines, Math.Max(100, value)))
                {
                    TrimLogEntries();
                }
            }
        }
        
        /// <summary>
        /// 是否显示时间戳
        /// </summary>
        public bool ShowTimestamp
        {
            get => _showTimestamp;
            set => SetProperty(ref _showTimestamp, value);
        }
        
        /// <summary>
        /// 是否自动滚动到末尾
        /// </summary>
        public bool AutoScrollToEnd
        {
            get => _autoScrollToEnd;
            set => SetProperty(ref _autoScrollToEnd, value);
        }
        
        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        /// <summary>
        /// 选中的日志级别
        /// </summary>
        public LogLevelFilter SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                if (SetProperty(ref _selectedLevel, value))
                {
                    FilterLogEntries();
                }
            }
        }
        
        /// <summary>
        /// 日志文件路径
        /// </summary>
        public string LogFilePath
        {
            get => _logFilePath;
            set => SetProperty(ref _logFilePath, value);
        }
        
        /// <summary>
        /// 日志级别选项
        /// </summary>
        /// <summary>
        /// 刷新日志命令
        /// </summary>
        public ICommand RefreshLogsCommand
        {
            get => _refreshLogsCommand;
            set => SetProperty(ref _refreshLogsCommand, value);
        }
        
        /// <summary>
        /// 清除日志命令
        /// </summary>
        public ICommand ClearLogsCommand
        {
            get => _clearLogsCommand;
            set => SetProperty(ref _clearLogsCommand, value);
        }
        
        /// <summary>
        /// 导出日志命令
        /// </summary>
        public ICommand ExportLogsCommand
        {
            get => _exportLogsCommand;
            set => SetProperty(ref _exportLogsCommand, value);
        }
        
        /// <summary>
        /// 切换文件跟踪命令
        /// </summary>
        public ICommand FollowFileCommand
        {
            get => _followFileCommand;
            set => SetProperty(ref _followFileCommand, value);
        }
        public Array LogLevelOptions => Enum.GetValues(typeof(LogLevelFilter));
        
        public LogViewerViewModel()
        {
            InitializeLogFilePath();
            StartLogWatching();
        }
        
        /// <summary>
        /// 初始化日志文件路径
        /// </summary>
        private void InitializeLogFilePath()
        {
            try
            {
                string? projectRoot = FindProjectRoot();
                if (projectRoot == null)
                {
                    projectRoot = Directory.GetCurrentDirectory() ?? ".";
                }
                var logDir = Path.Combine(projectRoot, "EnderDebugger", "Logs");
                Directory.CreateDirectory(logDir);

                // 优先解析 EnderLogger 写入的索引文件或时间戳文件，若无则回退到固定文件名
                LogFilePath = ResolveViewerFilePath(logDir);

                // 如果最终的 LogFilePath 指向的文件不存在，创建它（以便 FileSystemWatcher 可以监控）
                try
                {
                    if (!string.IsNullOrEmpty(LogFilePath) && !File.Exists(LogFilePath))
                    {
                        File.WriteAllText(LogFilePath, string.Empty);
                    }
                }
                catch
                {
                    // 忽略创建失败，后续读取会报告错误状态
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"初始化日志路径失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 解析 Viewer 日志文件的实际路径：优先使用 EnderLogger 写入的索引 (LuminoLogViewer.current)，
        /// 否则选择最新的 LuminoLogViewer_*.log，再不然回退到固定名 LuminoLogViewer.log
        /// </summary>
        private string ResolveViewerFilePath(string logDir)
        {
            try
            {
                if (string.IsNullOrEmpty(logDir))
                    return "LuminoLogViewer.log";

                // 1) 尝试读取索引文件
                var indexPath = Path.Combine(logDir, "LuminoLogViewer.current");
                if (File.Exists(indexPath))
                {
                    try
                    {
                        var name = File.ReadAllText(indexPath).Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            var path = Path.Combine(logDir, name);
                            if (File.Exists(path))
                                return path;
                        }
                    }
                    catch { /* 忽略索引解析错误，继续下一个策略 */ }
                }

                // 2) 选择最新的带时间戳的 Viewer 日志
                var files = Directory.GetFiles(logDir, "LuminoLogViewer_*.log");
                if (files != null && files.Length > 0)
                {
                    var newest = files.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault();
                    if (!string.IsNullOrEmpty(newest))
                        return newest;
                }

                // 3) 回退到固定文件名
                return Path.Combine(logDir, "LuminoLogViewer.log");
            }
            catch
            {
                return "LuminoLogViewer.log";
            }
        }
        
        /// <summary>
        /// 查找项目根目录
        /// </summary>
        private string? FindProjectRoot()
        {
            string? currentDir = Directory.GetCurrentDirectory();
            if (string.IsNullOrEmpty(currentDir))
                return null;
            
            DirectoryInfo? dir = new DirectoryInfo(currentDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Lumino.sln")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            
            return currentDir;
        }
        
        /// <summary>
        /// 开始监控日志文件
        /// </summary>
        private void StartLogWatching()
        {
            Task.Run(() =>
            {
                try
                {
                    ReadExistingLogs();
                    
                    if (FollowFile)
                    {
                        SetupFileWatcher();
                        StatusMessage = "正在监控日志文件...";
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        StatusMessage = $"启动监控失败: {ex.Message}";
                    });
                }
            });
        }
        
        /// <summary>
        /// 读取现有日志
        /// </summary>
        private void ReadExistingLogs()
        {
            try
            {
                using var stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                
                var lines = new System.Collections.Generic.List<string>();
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                    if (lines.Count > MaxLines)
                    {
                        lines.RemoveAt(0);
                    }
                }
                
                foreach (var logLine in lines)
                {
                    ProcessLogLine(logLine);
                }
                
                _lastPosition = stream.Position;
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = $"读取日志失败: {ex.Message}";
                });
            }
        }
        
        /// <summary>
        /// 设置文件监控器
        /// </summary>
        private void SetupFileWatcher()
        {
            try
            {
                var logDir = Path.GetDirectoryName(LogFilePath);
                if (string.IsNullOrEmpty(logDir))
                {
                    throw new InvalidOperationException("无法获取日志文件目录");
                }
                _fileWatcher = new FileSystemWatcher(logDir)
                {
                    Filter = Path.GetFileName(LogFilePath) ?? "LuminoLogViewer.log",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += (sender, e) =>
                {
                    ReadNewLogs();
                };
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = $"设置文件监控失败: {ex.Message}";
                });
            }
        }
        
        /// <summary>
        /// 读取新日志
        /// </summary>
        private void ReadNewLogs()
        {
            try
            {
                using var stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                
                stream.Seek(_lastPosition, SeekOrigin.Begin);
                
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    ProcessLogLine(line);
                }
                
                _lastPosition = stream.Position;
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = $"读取新日志失败: {ex.Message}";
                });
            }
        }
        
        /// <summary>
        /// 处理日志行
        /// </summary>
        private void ProcessLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            try
            {
                LogEntryViewModel? logEntry = null;
                
                // 尝试解析JSON格式
                if (line.StartsWith("{"))
                {
                    var jsonLog = JsonSerializer.Deserialize<JsonLogEntry>(line);
                    if (jsonLog != null)
                    {
                        logEntry = new LogEntryViewModel
                        {
                            Timestamp = jsonLog.Timestamp.ToString("HH:mm:ss.fff"),
                            Level = jsonLog.Level,
                            Component = jsonLog.Component,
                            Source = "JSON",
                            Message = jsonLog.Message,
                            RawLine = line
                        };
                    }
                }
                
                // 尝试解析新格式 [HH:mm:ss.fff] [LEVEL] [SOURCE] [COMPONENT] Message
                if (logEntry == null)
                {
                    var newFormat = ParseNewFormat(line);
                    if (newFormat != null)
                    {
                        logEntry = newFormat;
                        logEntry.RawLine = line;
                    }
                }
                
                // 尝试解析旧格式 [EnderDebugger][DATETIME][SOURCE][COMPONENT]Message
                if (logEntry == null)
                {
                    var oldFormat = ParseOldFormat(line);
                    if (oldFormat != null)
                    {
                        logEntry = oldFormat;
                        logEntry.RawLine = line;
                    }
                }
                
                if (logEntry != null)
                {
                    // 先检查是否需要显示
                    bool shouldDisplay = false;
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        shouldDisplay = ShouldDisplayLog(logEntry);
                    });
                    
                    if (shouldDisplay)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                LogEntries.Add(logEntry);
                                TrimLogEntries();
                                
                                // 如果启用了自动滚动，触发滚动事件
                                if (AutoScrollToEnd)
                                {
                                    AutoScrollToEndRequested?.Invoke(this, EventArgs.Empty);
                                }
                            }
                            catch
                            {
                                // 忽略添加失败
                            }
                        });
                    }
                }
            }
            catch
            {
                // 忽略解析错误
            }
        }
        
        /// <summary>
        /// 判断是否应该显示日志
        /// </summary>
        private bool ShouldDisplayLog(LogEntryViewModel logEntry)
        {
            if (logEntry == null)
                return false;

            // 检查日志级别
            if (SelectedLevel != LogLevelFilter.All)
            {
                var levelPriority = GetLevelPriority(logEntry.Level);
                var selectedPriority = (int)SelectedLevel;
                if (levelPriority < selectedPriority)
                    return false;
            }
            
            // 检查搜索词
            if (!string.IsNullOrEmpty(SearchText))
            {
                return (logEntry.Message?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (logEntry.Level?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (logEntry.Component?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (logEntry.Source?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
            }
            
            return true;
        }
        
        /// <summary>
        /// 获取日志级别优先级
        /// </summary>
        private int GetLevelPriority(string level)
        {
            return level.Trim().ToUpper() switch
            {
                "DEBUG" => 0,
                "INFO" => 1,
                "WARN" or "WARNING" => 2,
                "ERROR" => 3,
                "FATAL" => 4,
                _ => 5
            };
        }
        
        /// <summary>
        /// 过滤日志条目
        /// </summary>
        private void FilterLogEntries()
        {
            // 重新应用过滤器
            var filteredEntries = LogEntries.Where(ShouldDisplayLog).ToList();
            
            Dispatcher.UIThread.Post(() =>
            {
                LogEntries.Clear();
                foreach (var entry in filteredEntries)
                {
                    LogEntries.Add(entry);
                }
            });
        }
        
        /// <summary>
        /// 修剪日志条目
        /// </summary>
        private void TrimLogEntries()
        {
            Dispatcher.UIThread.Post(() =>
            {
                while (LogEntries.Count > MaxLines)
                {
                    LogEntries.RemoveAt(0);
                }
            });
        }
        
        /// <summary>
        /// 解析新的日志格式
        /// </summary>
        private LogEntryViewModel? ParseNewFormat(string line)
        {
            var pattern = @"\[(\d{2}:\d{2}:\d{2}\.\d{3})\]\s*\[(\w+)\]\s*\[([^\]]+)\]\s*\[([^\]]+)\]\s*(.*)";
            var match = Regex.Match(line, pattern);
            
            if (match.Success)
            {
                return new LogEntryViewModel
                {
                    Timestamp = match.Groups[1].Value,
                    Level = match.Groups[2].Value.Trim(),
                    Source = match.Groups[3].Value.Trim(),
                    Component = match.Groups[4].Value.Trim(),
                    Message = match.Groups[5].Value.Trim()
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// 解析旧的日志格式
        /// </summary>
        private LogEntryViewModel? ParseOldFormat(string line)
        {
            var pattern = @"\[EnderDebugger\]\[([^\]]+)\]\[([^\]]+)\]\[([^\]]+)\]\s*(.*)";
            var match = Regex.Match(line, pattern);
            
            if (match.Success)
            {
                string dateTimeStr = match.Groups[1].Value;
                string source = match.Groups[2].Value.Trim();
                string component = match.Groups[3].Value.Trim();
                string message = match.Groups[4].Value.Trim();
                
                var timeMatch = Regex.Match(dateTimeStr, @"(\d{2}:\d{2}:\d{2}\.\d{3})");
                string timestamp = timeMatch.Success ? timeMatch.Groups[1].Value : "00:00:00.000";
                
                return new LogEntryViewModel
                {
                    Timestamp = timestamp,
                    Level = "INFO",
                    Source = source,
                    Component = component,
                    Message = message
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// 清除日志
        /// </summary>
        public void ClearLogs()
        {
            Dispatcher.UIThread.Post(() =>
            {
                LogEntries.Clear();
                StatusMessage = "日志已清除";
            });
        }
        
        /// <summary>
        /// 刷新日志
        /// </summary>
        public void RefreshLogs()
        {
            StatusMessage = "正在刷新...";
            _lastPosition = 0;
            Dispatcher.UIThread.Post(() =>
            {
                LogEntries.Clear();
            });
            
            Task.Run(ReadExistingLogs);
        }
        
        /// <summary>
        /// 导出日志
        /// </summary>
        public async Task ExportLogsAsync(string filePath)
        {
            try
            {
                var logs = string.Join(Environment.NewLine, LogEntries.Select(e => e.RawLine));
                await File.WriteAllTextAsync(filePath, logs);
                StatusMessage = $"日志已导出到: {filePath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"导出失败: {ex.Message}";
            }
        }
        
        public new void Dispose()
        {
            _fileWatcher?.Dispose();
        }
    }
    
    /// <summary>
    /// 日志条目视图模型
    /// </summary>
    public class LogEntryViewModel : INotifyPropertyChanged
    {
        private string _timestamp = string.Empty;
        private string _level = string.Empty;
        private string _source = string.Empty;
        private string _component = string.Empty;
        private string _message = string.Empty;
        private string _rawLine = string.Empty;
        
        public string Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }
        
        public string Level
        {
            get => _level;
            set => SetProperty(ref _level, value);
        }
        
        public string Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }
        
        public string Component
        {
            get => _component;
            set => SetProperty(ref _component, value);
        }
        
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }
        
        public string RawLine
        {
            get => _rawLine;
            set => SetProperty(ref _rawLine, value);
        }
        
        /// <summary>
        /// 获取日志级别颜色
        /// </summary>
        public string LevelColor => Level.Trim().ToUpper() switch
        {
            "DEBUG" => "#00BFFF",      // 亮青色
            "INFO" => "#00FF00",       // 亮绿色
            "WARN" or "WARNING" => "#FFFF00", // 亮黄色
            "ERROR" => "#FF0000",      // 亮红色
            "FATAL" => "#FF00FF",      // 亮紫色
            _ => "#C0C0C0"            // 亮灰色
        };
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName ?? string.Empty);
            return true;
        }
    }
    
    /// <summary>
    /// 日志级别过滤器
    /// </summary>
    public enum LogLevelFilter
    {
        All = -1,
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Fatal = 4
    }
    
    /// <summary>
    /// JSON日志条目
    /// </summary>
    internal class JsonLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}