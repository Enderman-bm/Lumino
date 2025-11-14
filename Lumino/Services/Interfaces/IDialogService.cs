using System;
using System.Threading;
using System.Threading.Tasks;
using Lumino.Views.Progress;

namespace Lumino.Services.Interfaces
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
        /// 显示加载中对话框（简单版本，无进度）
        /// </summary>
        /// <param name="message">加载信息</param>
        Task ShowLoadingDialogAsync(string message);

        /// <summary>
        /// 关闭加载中对话框
        /// </summary>
        Task CloseLoadingDialogAsync();

        /// <summary>
        /// 显示进度窗口
        /// </summary>
        /// <param name="title">窗口标题</param>
        /// <param name="canCancel">是否允许取消</param>
        /// <returns>进度窗口实例，用于更新进度</returns>
        Task<ProgressWindow> ShowProgressDialogAsync(string title, bool canCancel = false);

        /// <summary>
        /// 关闭进度窗口
        /// </summary>
        /// <param name="progressWindow">要关闭的进度窗口</param>
        Task CloseProgressDialogAsync(ProgressWindow progressWindow);

        /// <summary>
        /// 执行带进度回调的长时间运行任务
        /// </summary>
        /// <typeparam name="T">任务返回类型</typeparam>
        /// <param name="title">进度窗口标题</param>
        /// <param name="task">要执行的任务，接收进度回调和取消令牌</param>
        /// <param name="canCancel">是否允许取消</param>
        /// <returns>任务结果</returns>
        Task<T> RunWithProgressAsync<T>(string title, 
            Func<IProgress<(double Progress, string Status)>, CancellationToken, Task<T>> task, 
            bool canCancel = false);

        /// <summary>
        /// 执行带进度回调的长时间运行任务（无返回值）
        /// </summary>
        /// <param name="title">进度窗口标题</param>
        /// <param name="task">要执行的任务，接收进度回调和取消令牌</param>
        /// <param name="canCancel">是否允许取消</param>
        Task RunWithProgressAsync(string title, 
            Func<IProgress<(double Progress, string Status)>, CancellationToken, Task> task, 
            bool canCancel = false);

        /// <summary>
        /// 显示导入MIDI前的预加载设置对话框，提供文件名、大小信息，并允许用户 取消 / 重新选择 / 加载
        /// </summary>
        /// <param name="fileName">所选文件名（带扩展名）</param>
        /// <param name="fileSize">文件大小（字节）</param>
        Task<PreloadDialogResult> ShowPreloadMidiDialogAsync(string fileName, long fileSize);

        /// <summary>
        /// 在预加载对话框内执行带进度回调的任务：先显示预加载对话框，用户选择加载后在同一对话框内显示进度并执行任务。
        /// 若用户取消或重新选择，则直接返回对应的选择项；若用户选择加载，则等待任务完成并返回结果。
        /// </summary>
        /// <typeparam name="T">任务返回类型</typeparam>
        /// <param name="fileName">所选文件名（带扩展名）</param>
        /// <param name="fileSize">文件大小（字节）</param>
        /// <param name="task">要执行的任务，接收进度回调和取消令牌</param>
        /// <param name="canCancel">是否允许取消</param>
        /// <returns>元组 (PreloadDialogResult, 任务返回值（仅当选择为 Load 且任务成功时不为默认）)</returns>
        Task<(PreloadDialogResult Choice, T? Result)> ShowPreloadAndRunAsync<T>(string fileName, long fileSize,
            Func<IProgress<(double Progress, string Status)>, CancellationToken, Task<T>> task,
            bool canCancel = false);

        /// <summary>
        /// 显示导出对话框并在其中运行任务
        /// </summary>
        /// <typeparam name="T">任务返回值的类型</typeparam>
        /// <param name="fileName">文件名</param>
        /// <param name="fileSize">文件大小（字节）</param>
        /// <param name="task">要在对话框中执行的任务</param>
        /// <param name="canCancel">是否允许取消</param>
        /// <returns>元组 (PreloadDialogResult, 任务返回值（仅当选择为 Export 且任务成功时不为默认）)</returns>
        Task<(PreloadDialogResult Choice, T? Result)> ShowExportAndRunAsync<T>(string fileName, long fileSize,
            Func<IProgress<(double Progress, string Status)>, CancellationToken, Task<T>> task,
            bool canCancel = false);
    }
}