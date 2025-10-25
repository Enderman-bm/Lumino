using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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
    /// EnderDebugger日志服务 - 集中式日志管理
    /// </summary>
    public class EnderLogger
    {
        private static EnderLogger? _instance = null;
        private static readonly object _lock = new object();
        private static bool _hasLoggedInitialization = false; // 添加静态标志跟踪是否已记录初始化
        private string _source = "EnderLogger"; // 默认来源
        private string _logDirectory = string.Empty;
        private string _logFilePath = string.Empty;
        private bool _isInitialized = false;

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

                // 创建LuminoLogViewer监听文件
                string viewerLogPath = Path.Combine(_logDirectory, "LuminoLogViewer.log");
                if (!File.Exists(viewerLogPath))
                {
                    File.WriteAllText(viewerLogPath, string.Empty);
                }

                // 检查命令行参数
                ParseCommandLineArgs();

                _isInitialized = true;
                
                // 只在第一次初始化时记录日志信息
                lock (_lock)
                {
                    if (!_hasLoggedInitialization)
                    {
                        if (IsDebugMode)
                        {
                            LogInternal(LogLevel.Info, "EnderLogger", "EnderDebugger日志系统初始化成功");
                            LogInternal(LogLevel.Info, "EnderLogger", $"日志文件: {_logFilePath}");
                            LogInternal(LogLevel.Info, "EnderLogger", $"调试模式: 启用");
                            LogInternal(LogLevel.Info, "EnderLogger", $"最小日志级别: {MinLogLevel}");
                        }
                        // 非调试模式下不输出任何日志
                        
                        _hasLoggedInitialization = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnderDebugger] 初始化失败: {ex.Message}");
            }
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

            // 只有在已经是调试模式时才输出日志，避免在非调试模式下输出日志
            if (IsDebugMode)
            {
                LogInternal(LogLevel.Info, "EnderLogger", $"调试模式已启用，日志级别: {MinLogLevel}");
            }
        }

        /// <summary>
        /// 禁用调试模式
        /// </summary>
        public void DisableDebugMode()
        {
            IsDebugMode = false;
            MinLogLevel = LogLevel.Info;
            // 不输出日志，因为此时已是非调试模式
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
            // 如果不是调试模式，不输出任何日志
            if (!IsDebugMode)
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
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string levelText = GetLevelText(level);
                string levelColor = GetLevelColor(level);
                string resetColor = "\u001b[0m"; // 重置颜色
                
                // 确保内容不为空
                if (string.IsNullOrEmpty(content))
                {
                    content = " ";
                }
                
                // 美化日志消息格式
                string logMessage = $"{levelColor}[{timestamp}] [{levelText}] [{_source}] [{eventType}] {content}{resetColor}";
                
                // 输出到调试控制台
                System.Diagnostics.Debug.WriteLine(logMessage);
                
                // 输出到控制台（显示颜色）
                Console.WriteLine(logMessage);
                
                // 写入日志文件（不包含颜色代码）
                string fileLogMessage = $"[{timestamp}] [{levelText}] [{_source}] [{eventType}] {content}";
                WriteToFile(fileLogMessage);
                
                // 写入LuminoLogViewer监听的日志文件（JSON格式）
                WriteToLuminoLogViewer(levelText, _source, content);
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
                // 写入原始日志文件
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
        /// 写入LuminoLogViewer监听的日志文件
        /// </summary>
        private void WriteToLuminoLogViewer(string level, string component, string content)
        {
            try
            {
                if (!_isInitialized) return;
                
                // 创建JSON格式的日志条目
                var logEntry = new
                {
                    Timestamp = DateTime.Now,
                    Level = level.Trim(),
                    Component = component,
                    Message = content
                };
                
                string jsonEntry = System.Text.Json.JsonSerializer.Serialize(logEntry) + Environment.NewLine;
                
                // 写入LuminoLogViewer监听的日志文件
                string viewerLogPath = Path.Combine(_logDirectory, "LuminoLogViewer.log");
                File.AppendAllText(viewerLogPath, jsonEntry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnderDebugger] 写入LuminoLogViewer日志失败: {ex.Message}");
            }
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
        /// 获取日志级别对应的颜色标识
        /// </summary>
        private string GetLevelColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return "\u001b[36m"; // 青色
                case LogLevel.Info:
                    return "\u001b[32m"; // 绿色
                case LogLevel.Warn:
                    return "\u001b[33m"; // 黄色
                case LogLevel.Error:
                    return "\u001b[31m"; // 红色
                case LogLevel.Fatal:
                    return "\u001b[35m"; // 紫色
                default:
                    return "\u001b[0m";  // 默认颜色
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