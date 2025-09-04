using System.Threading.Tasks;

namespace DominoNext.Services.Interfaces
{
    /// <summary>
    /// 对话框服务接口 - 用于在ViewModel中打开对话框，遵循MVVM原则
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// 显示设置对话框
        /// </summary>
        /// <returns>用户是否确认了设置更改</returns>
        Task<bool> ShowSettingsDialogAsync();

        /// <summary>
        /// 显示文件打开对话框
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="filters">文件过滤器</param>
        /// <returns>选择的文件路径，如果取消则返回null</returns>
        Task<string?> ShowOpenFileDialogAsync(string title, string[]? filters = null);

        /// <summary>
        /// 显示文件保存对话框
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="defaultFileName">默认文件名</param>
        /// <param name="filters">文件过滤器</param>
        /// <returns>选择的保存路径，如果取消则返回null</returns>
        Task<string?> ShowSaveFileDialogAsync(string title, string? defaultFileName = null, string[]? filters = null);

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息内容</param>
        /// <returns>用户是否确认</returns>
        Task<bool> ShowConfirmationDialogAsync(string title, string message);

        /// <summary>
        /// 显示错误对话框
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">错误消息</param>
        Task ShowErrorDialogAsync(string title, string message);

        /// <summary>
        /// 显示信息对话框
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">信息内容</param>
        Task ShowInfoDialogAsync(string title, string message);
    }
}