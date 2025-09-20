using Avalonia.Controls;
using System.Threading.Tasks;

namespace Lumino.ViewModels.Editor.Dialogs
{
    /// <summary>
    /// 进度对话框接口
    /// </summary>
    public interface IProgressDialog
    {
        /// <summary>
        /// 对话框标题
        /// </summary>
        string Title { get; set; }

        /// <summary>
        /// 进度消息
        /// </summary>
        string Message { get; set; }

        /// <summary>
        /// 是否不确定进度
        /// </summary>
        bool IsIndeterminate { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        int Maximum { get; set; }

        /// <summary>
        /// 当前值
        /// </summary>
        int Value { get; set; }

        /// <summary>
        /// 显示对话框
        /// </summary>
        Task ShowAsync();

        /// <summary>
        /// 关闭对话框
        /// </summary>
        void Close();
    }
}