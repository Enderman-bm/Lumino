using System;
using System.Diagnostics;
using Lumino.Services.Interfaces;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// ��־����ʵ�� - ����System.Diagnostics.Debug����־��¼
    /// �ṩͳһ����־��ʽ�ͷ�����������ڵ��Ժ�����׷��
    /// </summary>
    public class LoggingService : ILoggingService
    {
        private readonly LogLevel _minimumLogLevel;

        /// <summary>
        /// ��ʼ����־����
        /// </summary>
        /// <param name="minimumLogLevel">�����־���𣬵��ڴ˼������־��������</param>
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
        /// д����־���������
        /// ͳһ����־��ʽ��[ʱ���] [����] [����] ��Ϣ
        /// </summary>
        /// <param name="level">��־����</param>
        /// <param name="message">��־��Ϣ</param>
        /// <param name="category">��־����</param>
        private void WriteLog(LogLevel level, string message, string? category)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var levelString = GetLevelString(level);
                var categoryString = string.IsNullOrEmpty(category) ? "General" : category;

                var formattedMessage = $"[{timestamp}] [{levelString}] [{categoryString}] {message}";
                
                Debug.WriteLine(formattedMessage);

                // ���ڴ�������ؼ���Ҳ���������̨��������ã�
                if (level >= LogLevel.Error)
                {
                    Console.WriteLine(formattedMessage);
                }
            }
            catch (Exception ex)
            {
                // ��־��¼������������ʱ��ֱ�������Debug���������޵ݹ�
                Debug.WriteLine($"[LOGGING ERROR] Failed to write log: {ex.Message}");
            }
        }

        /// <summary>
        /// ��ʽ���쳣��Ϣ
        /// �����쳣���͡���Ϣ�Ͷ�ջ������Ϣ
        /// </summary>
        /// <param name="exception">�쳣����</param>
        /// <param name="additionalMessage">������Ϣ</param>
        /// <returns>��ʽ�����쳣��Ϣ</returns>
        private string FormatExceptionMessage(Exception exception, string? additionalMessage)
        {
            var exceptionInfo = $"{exception.GetType().Name}: {exception.Message}";
            
            if (!string.IsNullOrEmpty(additionalMessage))
            {
                exceptionInfo = $"{additionalMessage} - {exceptionInfo}";
            }

            // ֻ��Debugģʽ�°�����ջ���٣�����Release�汾����־�����߳�
            if (Debugger.IsAttached)
            {
                exceptionInfo += $"\nStackTrace: {exception.StackTrace}";
            }

            // �����ڲ��쳣��Ϣ
            if (exception.InnerException != null)
            {
                exceptionInfo += $"\nInner Exception: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
            }

            return exceptionInfo;
        }

        /// <summary>
        /// ��ȡ��־������ַ�����ʾ
        /// ȷ����־�����һ���ԺͿɶ���
        /// </summary>
        /// <param name="level">��־����</param>
        /// <returns>�����ַ���</returns>
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