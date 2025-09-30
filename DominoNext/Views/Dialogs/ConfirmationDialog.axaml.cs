using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DominoNext.Views.Dialogs
{
    /// <summary>
    /// 确认对话框 - 用于显示确认消息并获取用户选择
    /// 简单的模态对话框，支持确定/取消操作
    /// </summary>
    public partial class ConfirmationDialog : Window
    {
        /// <summary>
        /// 对话框结果 - 用户是否确认操作
        /// </summary>
        public bool Result { get; private set; } = false;

        /// <summary>
        /// 显示的消息内容
        /// </summary>
        public string Message { get; set; } = string.Empty;

        public ConfirmationDialog()
        {
            InitializeComponent();
            
            // 设置DataContext为自己，以便绑定Message属性
            DataContext = this;
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            Result = true;
            Close(Result);
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Result = false;
            Close(Result);
        }
    }
}