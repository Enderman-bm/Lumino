using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;

namespace LuminoLogViewer
{
    internal sealed class Program
    {
        private static string logFilePath;
        private static FileSystemWatcher fileWatcher;
        private static long lastPosition = 0;
        
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("=== Lumino 日志查看器 ===");
            Console.WriteLine("正在监听日志输出...");
            Console.WriteLine("按 Ctrl+C 退出");
            Console.WriteLine();

            // 获取日志文件路径
            // 首先尝试查找项目根目录
            string? projectRoot = FindProjectRoot();
            if (projectRoot == null)
            {
                // 如果找不到项目根目录，使用当前目录
                projectRoot = Directory.GetCurrentDirectory() ?? ".";
            }
            
            var logDir = Path.Combine(projectRoot, "EnderDebugger", "Logs");
            Directory.CreateDirectory(logDir);
            logFilePath = Path.Combine(logDir, "LuminoLogViewer.log");

            // 如果日志文件不存在，创建它
            if (!File.Exists(logFilePath))
            {
                File.WriteAllText(logFilePath, "");
            }

            // 读取现有日志内容
            ReadExistingLogs();

            // 设置文件监听器
            SetupFileWatcher();

            // 保持程序运行
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("正在退出日志查看器...");
                fileWatcher?.Dispose();
                Environment.Exit(0);
            };

            while (true)
            {
                Thread.Sleep(1000);
            }
        }
        
        /// <summary>
        /// 查找项目根目录
        /// </summary>
        private static string? FindProjectRoot()
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
        
        private static void ReadExistingLogs()
        {
            try
            {
                using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    // 只读取最后100行
                    var lines = new System.Collections.Generic.List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                        if (lines.Count > 100)
                        {
                            lines.RemoveAt(0);
                        }
                    }
                    
                    foreach (var logLine in lines)
                    {
                        ProcessLogLine(logLine);
                    }
                    
                    lastPosition = stream.Position;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取现有日志时出错: {ex.Message}");
            }
        }
        
        private static void SetupFileWatcher()
        {
            var logDir = Path.GetDirectoryName(logFilePath);
            fileWatcher = new FileSystemWatcher(logDir)
            {
                Filter = Path.GetFileName(logFilePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            fileWatcher.Changed += (sender, e) =>
            {
                try
                {
                    ReadNewLogs();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"读取新日志时出错: {ex.Message}");
                }
            };
        }
        
        private static void ReadNewLogs()
        {
            using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                // 跳转到上次读取的位置
                stream.Seek(lastPosition, SeekOrigin.Begin);
                
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    ProcessLogLine(line);
                }
                
                lastPosition = stream.Position;
            }
        }
        
        private static void ProcessLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            try
            {
                // 解析日志行（假设是JSON格式）
                if (line.StartsWith("{"))
                {
                    var logEntry = JsonSerializer.Deserialize<LogEntry>(line);
                    if (logEntry != null)
                    {
                        // 格式化为与EnderLogger控制台输出一致的格式
                        string timestamp = logEntry.Timestamp.ToString("HH:mm:ss.fff");
                        string levelText = GetLevelText(logEntry.Level);
                        string levelColor = GetLevelColor(logEntry.Level);
                        string resetColor = "\u001b[0m"; // 重置颜色
                        
                        Console.WriteLine($"{levelColor}[{timestamp}] [{levelText}] [{logEntry.Component}] [LogViewer] {logEntry.Message}{resetColor}");
                        return;
                    }
                }
                
                // 如果不是JSON格式，直接输出
                Console.WriteLine(line);
            }
            catch
            {
                // 如果解析失败，直接输出原始行
                Console.WriteLine(line);
            }
        }
        
        /// <summary>
        /// 获取日志级别文本
        /// </summary>
        private static string GetLevelText(string level)
        {
            switch (level.Trim().ToUpper())
            {
                case "DEBUG":
                    return "DEBUG";
                case "INFO":
                    return "INFO ";
                case "WARN":
                    return "WARN ";
                case "ERROR":
                    return "ERROR";
                case "FATAL":
                    return "FATAL";
                default:
                    return "UNKNOWN";
            }
        }
        
        /// <summary>
        /// 获取日志级别对应的颜色标识
        /// </summary>
        private static string GetLevelColor(string level)
        {
            switch (level.Trim().ToUpper())
            {
                case "DEBUG":
                    return "\u001b[36m"; // 青色
                case "INFO":
                    return "\u001b[32m"; // 绿色
                case "WARN":
                    return "\u001b[33m"; // 黄色
                case "ERROR":
                    return "\u001b[31m"; // 红色
                case "FATAL":
                    return "\u001b[35m"; // 紫色
                default:
                    return "\u001b[0m";  // 默认颜色
            }
        }
        
        private class LogEntry
        {
            public string Level { get; set; } = "";
            public string Component { get; set; } = "";
            public string Message { get; set; } = "";
            public DateTime Timestamp { get; set; }
        }
    }
}