using System;
using System.Diagnostics;
using Lumino.Services.Interfaces;
using EnderDebugger;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 日志服务实现 - 基于EnderLogger的日志记录
    /// 提供统一的日志格式和分类管理，便于调试和问题追踪
    /// </summary>
    public class LoggingService : ILoggingService
    {
        private readonly Lumino.Services.Interfaces.LogLevel _minimumLogLevel;
        private readonly EnderLogger _enderLogger;

        /// <summary>
        /// 初始化日志服务
        /// </summary>
        /// <param name="minimumLogLevel">最低日志级别，低于此级别的日志将被忽略</param>
        public LoggingService(Lumino.Services.Interfaces.LogLevel minimumLogLevel = Lumino.Services.Interfaces.LogLevel.Debug)
        {
            _minimumLogLevel = minimumLogLevel;
            _enderLogger = EnderLogger.Instance;
        }

        public void LogInfo(string message, string? category = null)
        {
            if (IsEnabled(Lumino.Services.Interfaces.LogLevel.Info))
            {
                WriteLog(Lumino.Services.Interfaces.LogLevel.Info, message, category);
            }
        }

        public void LogWarning(string message, string? category = null)
        {
            if (IsEnabled(Lumino.Services.Interfaces.LogLevel.Warning))
            {
                WriteLog(Lumino.Services.Interfaces.LogLevel.Warning, message, category);
            }
        }

        public void LogError(string message, string? category = null)
        {
            if (IsEnabled(Lumino.Services.Interfaces.LogLevel.Error))
            {
                WriteLog(Lumino.Services.Interfaces.LogLevel.Error, message, category);
            }
        }

        public void LogException(Exception exception, string? message = null, string? category = null)
        {
            if (IsEnabled(Lumino.Services.Interfaces.LogLevel.Error))
            {
                var exceptionMessage = FormatExceptionMessage(exception, message);
                WriteLog(Lumino.Services.Interfaces.LogLevel.Error, exceptionMessage, category ?? "Exception");
            }
        }

        public void LogDebug(string message, string? category = null)
        {
            if (IsEnabled(Lumino.Services.Interfaces.LogLevel.Debug))
            {
                WriteLog(Lumino.Services.Interfaces.LogLevel.Debug, message, category);
            }
        }

        public bool IsEnabled(Lumino.Services.Interfaces.LogLevel level)
        {
            return level >= _minimumLogLevel;
        }

        /// <summary>
        /// 写入日志到EnderLogger
        /// 统一的日志格式：[时间戳] [级别] [分类] 消息
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志分类</param>
        private void WriteLog(Lumino.Services.Interfaces.LogLevel level, string message, string? category)
        {
            try
            {
                var categoryString = string.IsNullOrEmpty(category) ? "General" : category;
                
                // 将Lumino的LogLevel映射到EnderLogger的日志方法
                switch (level)
                {
                    case Lumino.Services.Interfaces.LogLevel.Debug:
                        _enderLogger.Debug(categoryString, message);
                        break;
                    case Lumino.Services.Interfaces.LogLevel.Info:
                        _enderLogger.Info(categoryString, message);
                        break;
                    case Lumino.Services.Interfaces.LogLevel.Warning:
                        _enderLogger.Warn(categoryString, message);
                        break;
                    case Lumino.Services.Interfaces.LogLevel.Error:
                        _enderLogger.Error(categoryString, message);
                        break;
                    case Lumino.Services.Interfaces.LogLevel.Critical:
                        _enderLogger.Fatal(categoryString, message);
                        break;
                    default:
                        _enderLogger.Info(categoryString, message);
                        break;
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
        private string GetLevelString(Lumino.Services.Interfaces.LogLevel level)
        {
            return level switch
            {
                Lumino.Services.Interfaces.LogLevel.Debug => "DEBUG",
                Lumino.Services.Interfaces.LogLevel.Info => "INFO ",
                Lumino.Services.Interfaces.LogLevel.Warning => "WARN ",
                Lumino.Services.Interfaces.LogLevel.Error => "ERROR",
                Lumino.Services.Interfaces.LogLevel.Critical => "FATAL",
                _ => "UNKN "
            };
        }
    }
}