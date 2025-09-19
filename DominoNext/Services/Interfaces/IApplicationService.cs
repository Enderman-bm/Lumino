using System.Threading.Tasks;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// Ӧ�ó����������ڷ���ӿ� - ����Ӧ�ó�����������ڲ���
    /// </summary>
    public interface IApplicationService
    {
        /// <summary>
        /// �˳�Ӧ�ó���
        /// </summary>
        void Shutdown();

        /// <summary>
        /// ����Ӧ�ó���
        /// </summary>
        void Restart();

        /// <summary>
        /// ��С��Ӧ�ó���ϵͳ����
        /// </summary>
        void MinimizeToTray();

        /// <summary>
        /// ��ϵͳ���̻�ԭӦ�ó���
        /// </summary>
        void RestoreFromTray();

        /// <summary>
        /// ����Ƿ���԰�ȫ�˳��������Ƿ���δ����ĸ��ģ�
        /// </summary>
        /// <returns>�Ƿ���԰�ȫ�˳�</returns>
        Task<bool> CanShutdownSafelyAsync();

        /// <summary>
        /// ��ȡӦ�ó�����Ϣ
        /// </summary>
        /// <returns>Ӧ�ó���汾�����Ƶ���Ϣ</returns>
        (string Name, string Version, string Description) GetApplicationInfo();
    }
}