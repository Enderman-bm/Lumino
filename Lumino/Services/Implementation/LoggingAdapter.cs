using System;
using EnderDebugger;
using Lumino.Services.Interfaces;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 日志服务包装器 - 将ILoggingService包装为EnderLogger使用
    /// </summary>
    public class LoggingAdapter
    {
        private readonly ILoggingService _loggingService;

        public LoggingAdapter(ILoggingService loggingService)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        /// <summary>
        /// 获取EnderLogger实例，使用ILoggingService作为后端
        /// </summary>
        public EnderLogger GetEnderLogger()
        {
            // 创建一个新的EnderLogger实例，它会自动初始化
            var logger = new EnderLogger("LuminoWaveTable");
            
            // 由于EnderLogger的方法不是虚方法，我们不能直接重写
            // 所以我们需要创建一个包装方法来代理调用
            return logger;
        }

        /// <summary>
        /// 直接使用ILoggingService记录日志
        /// </summary>
        public void LogInfo(string category, string message)
        {
            _loggingService.LogInfo(message, category);
        }

        public void LogWarning(string category, string message)
        {
            _loggingService.LogWarning(message, category);
        }

        public void LogError(string category, string message)
        {
            _loggingService.LogError(message, category);
        }

        public void LogDebug(string category, string message)
        {
            _loggingService.LogDebug(message, category);
        }

        public void LogException(string category, Exception ex, string? message = null)
        {
            _loggingService.LogException(ex, message, category);
        }
    }
}