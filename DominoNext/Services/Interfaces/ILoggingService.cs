using System;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// ��־����ӿ� - ͳһ����Ӧ�ó������־��¼
    /// �ṩ��ͬ�������־��¼���ܣ�֧���쳣��¼�͸�ʽ�����
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// ��¼��Ϣ��־
        /// </summary>
        /// <param name="message">��־��Ϣ</param>
        /// <param name="category">��־���ࣨ��ѡ��</param>
        void LogInfo(string message, string? category = null);

        /// <summary>
        /// ��¼������־
        /// </summary>
        /// <param name="message">������Ϣ</param>
        /// <param name="category">��־���ࣨ��ѡ��</param>
        void LogWarning(string message, string? category = null);

        /// <summary>
        /// ��¼������־
        /// </summary>
        /// <param name="message">������Ϣ</param>
        /// <param name="category">��־���ࣨ��ѡ��</param>
        void LogError(string message, string? category = null);

        /// <summary>
        /// ��¼�쳣��־
        /// </summary>
        /// <param name="exception">�쳣����</param>
        /// <param name="message">������Ϣ����ѡ��</param>
        /// <param name="category">��־���ࣨ��ѡ��</param>
        void LogException(Exception exception, string? message = null, string? category = null);

        /// <summary>
        /// ��¼������־
        /// </summary>
        /// <param name="message">������Ϣ</param>
        /// <param name="category">��־���ࣨ��ѡ��</param>
        void LogDebug(string message, string? category = null);

        /// <summary>
        /// ���ָ����־�����Ƿ�����
        /// </summary>
        /// <param name="level">��־����</param>
        /// <returns>�Ƿ����øü������־</returns>
        bool IsEnabled(LogLevel level);
    }

    /// <summary>
    /// ��־����ö��
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// ���Լ��� - ��ϸ�ĵ�����Ϣ
        /// </summary>
        Debug = 0,

        /// <summary>
        /// ��Ϣ���� - һ��������Ϣ
        /// </summary>
        Info = 1,

        /// <summary>
        /// ���漶�� - Ǳ�����⵫��Ӱ������
        /// </summary>
        Warning = 2,

        /// <summary>
        /// ���󼶱� - ������Ϣ��Ӧ�ó�����Լ�������
        /// </summary>
        Error = 3,

        /// <summary>
        /// ���ؼ��� - ����Ӧ�ó�����ֹ�����ش���
        /// </summary>
        Critical = 4
    }
}