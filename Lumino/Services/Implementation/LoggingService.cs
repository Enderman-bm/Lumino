using System;
using System.Diagnostics;
using Lumino.Services.Interfaces;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 日志服务实现 - 基于System.Diagnostics.Debug的日志记录
    /// 提供统一的日志格式和分类管理，便于调试和问题追踪
    /// </summary>
    public class LoggingService : ILoggingService
    {
        private readonly LogLevel _minimumLogLevel;

        /// <summary>
        /// 初始化日志服务
        /// </summary>
        /// <param name="minimumLogLevel">最低日志级别，低于此级别的日志将被忽略</param>
        public LoggingService(LogLevel minimumLogLevel = LogLevel.Debug)
        {
            _minimumLogLevel = minimumLogLevel;
        }

        public void LogInfo(string message, string? category = null)
        {
            if (IsEnabled(LogLevel.Info))
            {
                WriteLog(LogLevel.Info, message, category);
            }
        }

        public void LogWarning(string message, string? category = null)
        {
            if (IsEnabled(LogLevel.Warning))
            {
                WriteLog(LogLevel.Warning, message, category);
            }
        }

        public void LogError(string message, string? category = null)
        {
            if (IsEnabled(LogLevel.Error))
            {
                WriteLog(LogLevel.Error, message, category);
            }
        }

        public void LogException(Exception exception, string? message = null, string? category = null)
        {
            if (IsEnabled(LogLevel.Error))
            {
                var exceptionMessage = FormatExceptionMessage(exception, message);
                WriteLog(LogLevel.Error, exceptionMessage, category ?? "Exception");
            }
        }

        public void LogDebug(string message, string? category = null)
        {
            if (IsEnabled(LogLevel.Debug))
            {
                WriteLog(LogLevel.Debug, message, category);
            }
        }

        public bool IsEnabled(LogLevel level)
        {
            return level >= _minimumLogLevel;
        }

        /// <summary>
        /// 写入日志到调试输出
        /// 统一的日志格式：[时间戳] [级别] [分类] 消息
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志分类</param>
        private void WriteLog(LogLevel level, string message, string? category)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var levelString = GetLevelString(level);
                var categoryString = string.IsNullOrEmpty(category) ? "General" : category;

                var formattedMessage = $"[{timestamp}] [{levelString}] [{categoryString}] {message}";
                
                Debug.WriteLine(formattedMessage);

                // 对于错误和严重级别，也输出到控制台（如果可用）
                if (level >= LogLevel.Error)
                {
                    Console.WriteLine(formattedMessage);
                }
            }
            catch (Exception ex)
            {
                // 日志记录本身发生错误时，直接输出到Debug，避免无限递归
                Debug.WriteLine($"[LOGGING ERROR] Failed to write log: {ex.Message}");
            }
        }

        /// <summary>
        /// 格式化异常信息
        /// 包含异常类型、消息和堆栈跟踪信息
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <param name="additionalMessage">附加消息</param>
        /// <returns>格式化的异常信息</returns>
        private string FormatExceptionMessage(Exception exception, string? additionalMessage)
        {
            var exceptionInfo = $"{exception.GetType().Name}: {exception.Message}";
            
            if (!string.IsNullOrEmpty(additionalMessage))
            {
                exceptionInfo = $"{additionalMessage} - {exceptionInfo}";
            }

            // 只在Debug模式下包含堆栈跟踪，避免Release版本的日志过于冗长
            if (Debugger.IsAttached)
            {
                exceptionInfo += $"\nStackTrace: {exception.StackTrace}";
            }

            // 包含内部异常信息
            if (exception.InnerException != null)
            {
                exceptionInfo += $"\nInner Exception: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
            }

            return exceptionInfo;
        }

        /// <summary>
        /// 获取日志级别的字符串表示
        /// 确保日志输出的一致性和可读性
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <returns>级别字符串</returns>
        private string GetLevelString(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => "DEBUG",
                LogLevel.Info => "INFO ",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "FATAL",
                _ => "UNKN "
            };
        }
    }
}