using System.Threading.Tasks;

namespace DominoNext.Services.Interfaces
{
    /// <summary>
    /// �Ի������ӿ� - ������ViewModel�д򿪶Ի�����ѭMVVMԭ��
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// ��ʾ���öԻ���
        /// </summary>
        /// <returns>�û��Ƿ�ȷ�������ø���</returns>
        Task<bool> ShowSettingsDialogAsync();

        /// <summary>
        /// ��ʾ�ļ��򿪶Ի���
        /// </summary>
        /// <param name="title">�Ի������</param>
        /// <param name="filters">�ļ�������</param>
        /// <returns>ѡ����ļ�·�������ȡ���򷵻�null</returns>
        Task<string?> ShowOpenFileDialogAsync(string title, string[]? filters = null);

        /// <summary>
        /// ��ʾ�ļ�����Ի���
        /// </summary>
        /// <param name="title">�Ի������</param>
        /// <param name="defaultFileName">Ĭ���ļ���</param>
        /// <param name="filters">�ļ�������</param>
        /// <returns>ѡ��ı���·�������ȡ���򷵻�null</returns>
        Task<string?> ShowSaveFileDialogAsync(string title, string? defaultFileName = null, string[]? filters = null);

        /// <summary>
        /// ��ʾȷ�϶Ի���
        /// </summary>
        /// <param name="title">����</param>
        /// <param name="message">��Ϣ����</param>
        /// <returns>�û��Ƿ�ȷ��</returns>
        Task<bool> ShowConfirmationDialogAsync(string title, string message);

        /// <summary>
        /// ��ʾ����Ի���
        /// </summary>
        /// <param name="title">����</param>
        /// <param name="message">������Ϣ</param>
        Task ShowErrorDialogAsync(string title, string message);

        /// <summary>
        /// ��ʾ��Ϣ�Ի���
        /// </summary>
        /// <param name="title">����</param>
        /// <param name="message">��Ϣ��Ϣ</param>
        Task ShowInfoDialogAsync(string title, string message);

        /// <summary>
        /// 显示加载中对话框
        /// </summary>
        /// <param name="message">加载信息</param>
        Task ShowLoadingDialogAsync(string message);

        /// <summary>
        /// 关闭加载中对话框
        /// </summary>
        Task CloseLoadingDialogAsync();
    }
}