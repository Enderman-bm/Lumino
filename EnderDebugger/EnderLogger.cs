using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using EnderDebugger.Services;

namespace EnderDebugger
{
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Fatal = 4
    }

    /// <summary>
    /// 日志条目类,用于UI显示和IPC传输
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        
        // 用于 JSON 序列化的颜色字符串
        public string LevelColorString { get; set; } = string.Empty;
        public string BorderColorString { get; set; } = string.Empty;
        
        // 用于 UI 显示的颜色对象(不参与序列化)
        [System.Text.Json.Serialization.JsonIgnore]
        public object LevelColor { get; set; } = null!;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public object BorderColor { get; set; } = null!;

        public string FullMessage => $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}][{Source}][{EventType}]{Message}";
    }

    /// <summary>
    /// EnderDebugger日志服务 - 集中式日志管理
    /// </summary>
    public class EnderLogger
    {
        private static EnderLogger? _instance = null;
        private static readonly object _lock = new object();
        private string _source = "EnderLogger"; // 默认来源
        private string _logDirectory = string.Empty;
        private string _logFilePath = string.Empty;
        private bool _isInitialized = false;
        private LogTransportClient? _transportClient; // IPC 传输客户端

        /// <summary>
        /// 日志条目添加事件，用于UI更新(仅在 EnderDebugger 进程中使用)
        /// </summary>
        public event Action<LogEntry>? LogEntryAdded;

        /// <summary>
        /// 是否启用了调试模式
        /// </summary>
        public bool IsDebugMode { get; private set; }

        /// <summary>
        /// 当前最小日志级别
        /// </summary>
        public LogLevel MinLogLevel { get; private set; } = LogLevel.Info;

        /// <summary>
        /// 单例实例
        /// </summary>
        public static EnderLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new EnderLogger();
                        }
                    }
                }
                return _instance;
            }
        }

        private EnderLogger()
        {
            Initialize();
        }

        /// <summary>
        /// 创建一个新的EnderLogger实例
        /// </summary>
        /// <param name="source">日志来源</param>
        public EnderLogger(string source)
        {
            _source = source;
            Initialize();
        }

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        private void Initialize()
        {
            try
            {
                // 创建日志目录
                string? projectRoot = FindProjectRoot();
                if (projectRoot == null)
                {
                    projectRoot = Directory.GetCurrentDirectory() ?? ".";
                }
                _logDirectory = Path.Combine(projectRoot, "EnderDebugger", "Logs");
                
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                // 创建日志文件
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _logFilePath = Path.Combine(_logDirectory, $"EnderDebugger_{timestamp}.log");

                // 检查命令行参数
                ParseCommandLineArgs();

                // 初始化 IPC 传输客户端(仅在 Lumino 进程中)
                // EnderDebugger 进程不需要传输客户端,直接使用事件
                if (!IsEnderDebuggerProcess())
                {
                    _transportClient = new LogTransportClient();
                }

                _isInitialized = true;
                
                // 记录初始化信息
                LogInternal(LogLevel.Info, "EnderLogger", "EnderDebugger日志系统初始化成功");
                LogInternal(LogLevel.Info, "EnderLogger", $"日志文件: {_logFilePath}");
                LogInternal(LogLevel.Info, "EnderLogger", $"调试模式: {(IsDebugMode ? "启用" : "禁用")}");
                LogInternal(LogLevel.Info, "EnderLogger", $"最小日志级别: {MinLogLevel}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnderDebugger] 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查当前进程是否为 EnderDebugger 进程
        /// </summary>
        private bool IsEnderDebuggerProcess()
        {
            var processName = Process.GetCurrentProcess().ProcessName;
            return processName.Contains("EnderDebugger", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 查找项目根目录
        /// </summary>
        private string? FindProjectRoot()
        {
            string? currentDir = Directory.GetCurrentDirectory();
            
            // 向上查找包含解决方案文件的目录
            DirectoryInfo? dir = currentDir != null ? new DirectoryInfo(currentDir) : null;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Lumino.sln")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            
            // 如果找不到，返回当前目录
            return currentDir;
        }

        /// <summary>
        /// 解析命令行参数
        /// </summary>
        private void ParseCommandLineArgs()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--debug" && i + 1 < args.Length)
                    {
                        EnableDebugMode(args[i + 1]);
                        return;
                    }
                    else if (args[i] == "--debug")
                    {
                        EnableDebugMode("all");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnderDebugger] 解析命令行参数失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 启用调试模式
        /// </summary>
        public void EnableDebugMode(string logLevel = "all")
        {
            IsDebugMode = true;
            
            switch (logLevel?.ToLower())
            {
                case "error":
                    MinLogLevel = LogLevel.Error;
                    break;
                case "warn":
                case "warning":
                    MinLogLevel = LogLevel.Warn;
                    break;
                case "info":
                    MinLogLevel = LogLevel.Info;
                    break;
                case "debug":
                    MinLogLevel = LogLevel.Debug;
                    break;
                case "all":
                default:
                    MinLogLevel = LogLevel.Debug;
                    break;
            }

            LogInternal(LogLevel.Info, "EnderLogger", $"调试模式已启用，日志级别: {MinLogLevel}");
        }

        /// <summary>
        /// 禁用调试模式
        /// </summary>
        public void DisableDebugMode()
        {
            IsDebugMode = false;
            MinLogLevel = LogLevel.Info;
            LogInternal(LogLevel.Info, "EnderLogger", "调试模式已禁用");
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        public void Debug(string eventType, string content)
        {
            Log(LogLevel.Debug, eventType, content);
        }

        /// <summary>
        /// 信息日志
        /// </summary>
        public void Info(string eventType, string content)
        {
            Log(LogLevel.Info, eventType, content);
        }

        /// <summary>
        /// 警告日志
        /// </summary>
        public void Warn(string eventType, string content)
        {
            Log(LogLevel.Warn, eventType, content);
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        public void Error(string eventType, string content)
        {
            Log(LogLevel.Error, eventType, content);
        }

        /// <summary>
        /// 致命错误日志
        /// </summary>
        public void Fatal(string eventType, string content)
        {
            Log(LogLevel.Fatal, eventType, content);
        }

        /// <summary>
        /// 记录异常
        /// </summary>
        public void LogException(Exception exception, string eventType = "Exception", string? content = null)
        {
            if (exception == null) return;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(content))
            {
                sb.AppendLine(content);
            }

            sb.AppendLine($"异常类型: {exception.GetType().Name}");
            sb.AppendLine($"异常消息: {exception.Message}");
            sb.AppendLine($"堆栈跟踪:");
            sb.AppendLine(exception.StackTrace);

            if (exception.InnerException != null)
            {
                sb.AppendLine($"内部异常: {exception.InnerException.Message}");
            }

            Log(LogLevel.Error, eventType, sb.ToString());
        }

        /// <summary>
        /// 内部日志记录方法
        /// </summary>
        private void Log(LogLevel level, string eventType, string content)

        {
            if (!ShouldLog(level))
                return;

            LogInternal(level, eventType, content);
        }

        /// <summary>
        /// 是否应该记录日志
        /// </summary>
        private bool ShouldLog(LogLevel level)
        {
            if (!IsDebugMode && level < LogLevel.Info)
                return false;

            return level >= MinLogLevel;
        }

        /// <summary>
        /// 内部日志输出
        /// </summary>
        private void LogInternal(LogLevel level, string eventType, string content)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string levelText = GetLevelText(level);
                
                // 格式化日志消息
                string logMessage = $"[EnderDebugger][{timestamp}][{_source}][{eventType}]{content}";
                
                // 写入日志文件
                WriteToFile(logMessage);

                // 创建UI日志条目
                var logEntry = CreateLogEntry(level, eventType, content);
                
                // 如果有 IPC 传输客户端,通过 IPC 发送(Lumino 进程)
                if (_transportClient != null)
                {
                    _transportClient.SendLog(logEntry); // 非阻塞发送
                }
                else
                {
                    // EnderDebugger 进程直接触发事件
                    LogEntryAdded?.Invoke(logEntry);
                }
            }
            catch (Exception ex)
            {
                // 防止日志记录本身抛出异常
                System.Diagnostics.Debug.WriteLine($"[EnderDebugger] 日志记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入日志文件
        /// </summary>
        private void WriteToFile(string message)
        {
            try
            {
                if (!string.IsNullOrEmpty(_logFilePath) && _isInitialized)
                {
                    File.AppendAllText(_logFilePath, message + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnderDebugger] 写入日志文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建UI日志条目
        /// </summary>
        private LogEntry CreateLogEntry(LogLevel level, string eventType, string content)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = GetLevelText(level).Trim(),
                Source = _source,
                EventType = eventType,
                Message = content
            };

            // 根据日志级别设置颜色
            switch (level)
            {
                case LogLevel.Debug:
                    entry.LevelColorString = "#808080"; // 灰色
                    entry.BorderColorString = "#C8C8C8";
                    entry.LevelColor = "#808080";
                    entry.BorderColor = "#C8C8C8";
                    break;
                case LogLevel.Info:
                    entry.LevelColorString = "#008000"; // 绿色
                    entry.BorderColorString = "#90EE90";
                    entry.LevelColor = "#008000";
                    entry.BorderColor = "#90EE90";
                    break;
                case LogLevel.Warn:
                    entry.LevelColorString = "#FFA500"; // 橙色
                    entry.BorderColorString = "#FFDAB9";
                    entry.LevelColor = "#FFA500";
                    entry.BorderColor = "#FFDAB9";
                    break;
                case LogLevel.Error:
                    entry.LevelColorString = "#FF0000"; // 红色
                    entry.BorderColorString = "#FFB6C1";
                    entry.LevelColor = "#FF0000";
                    entry.BorderColor = "#FFB6C1";
                    break;
                case LogLevel.Fatal:
                    entry.LevelColorString = "#8B0000"; // 深红色
                    entry.BorderColorString = "#FF6347";
                    entry.LevelColor = "#8B0000";
                    entry.BorderColor = "#FF6347";
                    break;
            }

            return entry;
        }

        /// <summary>
        /// 获取日志级别文本
        /// </summary>
        private string GetLevelText(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return "DEBUG";
                case LogLevel.Info:
                    return "INFO ";
                case LogLevel.Warn:
                    return "WARN ";
                case LogLevel.Error:
                    return "ERROR";
                case LogLevel.Fatal:
                    return "FATAL";
                default:
                    return "UNKNOWN";
            }
        }

        /// <summary>
        /// 获取当前日志文件路径
        /// </summary>
        public string GetCurrentLogFilePath()
        {
            return _logFilePath;
        }

        /// <summary>
        /// 获取日志目录路径
        /// </summary>
        public string GetLogDirectory()
        {
            return _logDirectory;
        }
    }
}