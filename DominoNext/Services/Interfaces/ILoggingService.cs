using System;

namespace DominoNext.Services.Interfaces
{
    /// <summary>
    /// 日志服务接口 - 统一管理应用程序的日志记录
    /// 提供不同级别的日志记录功能，支持异常记录和格式化输出
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="category">日志分类（可选）</param>
        void LogInfo(string message, string? category = null);

        /// <summary>
        /// 记录警告日志
        /// </summary>
        /// <param name="message">警告消息</param>
        /// <param name="category">日志分类（可选）</param>
        void LogWarning(string message, string? category = null);

        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="category">日志分类（可选）</param>
        void LogError(string message, string? category = null);

        /// <summary>
        /// 记录异常日志
        /// </summary>
        /// <param name="exception">异常对象</param>
        /// <param name="message">附加消息（可选）</param>
        /// <param name="category">日志分类（可选）</param>
        void LogException(Exception exception, string? message = null, string? category = null);

        /// <summary>
        /// 记录调试日志
        /// </summary>
        /// <param name="message">调试消息</param>
        /// <param name="category">日志分类（可选）</param>
        void LogDebug(string message, string? category = null);

        /// <summary>
        /// 检查指定日志级别是否启用
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <returns>是否启用该级别的日志</returns>
        bool IsEnabled(LogLevel level);
    }

    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 调试级别 - 详细的调试信息
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 信息级别 - 一般运行信息
        /// </summary>
        Info = 1,

        /// <summary>
        /// 警告级别 - 潜在问题但不影响运行
        /// </summary>
        Warning = 2,

        /// <summary>
        /// 错误级别 - 错误信息但应用程序可以继续运行
        /// </summary>
        Error = 3,

        /// <summary>
        /// 严重级别 - 导致应用程序终止的严重错误
        /// </summary>
        Critical = 4
    }
}