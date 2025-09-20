using Avalonia.Controls;
using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia;

namespace Lumino.ViewModels.Editor.Dialogs
{
    /// <summary>
    /// 进度对话框实现
    /// </summary>
    public class ProgressDialog : Window, IProgressDialog
    {
        private readonly ProgressBar _progressBar;
        private readonly TextBlock _messageTextBlock;

        /// <summary>
        /// 对话框标题
        /// </summary>
        public new string Title 
        { 
            get => base.Title ?? string.Empty;
            set => base.Title = value;
        }

        /// <summary>
        /// 进度消息
        /// </summary>
        public string Message 
        { 
            get => _messageTextBlock.Text ?? string.Empty;
            set => _messageTextBlock.Text = value;
        }

        /// <summary>
        /// 是否不确定进度
        /// </summary>
        public bool IsIndeterminate 
        { 
            get => _progressBar.IsIndeterminate;
            set => _progressBar.IsIndeterminate = value;
        }

        /// <summary>
        /// 最大值
        /// </summary>
        public int Maximum 
        { 
            get => (int)_progressBar.Maximum;
            set => _progressBar.Maximum = value;
        }

        /// <summary>
        /// 当前值
        /// </summary>
        public int Value 
        { 
            get => (int)_progressBar.Value;
            set => _progressBar.Value = value;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ProgressDialog()
        {
            Title = "进度";
            Width = 400;
            Height = 150;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 10
            };

            _messageTextBlock = new TextBlock
            {
                Text = "请稍候...",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };

            _progressBar = new ProgressBar
            {
                Height = 20,
                IsIndeterminate = true,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            stackPanel.Children.Add(_messageTextBlock);
            stackPanel.Children.Add(_progressBar);

            Content = stackPanel;
        }

        /// <summary>
        /// 显示对话框
        /// </summary>
        public async Task ShowAsync()
        {
            await ShowDialog(GetMainWindow());
        }

        /// <summary>
        /// 关闭对话框
        /// </summary>
        public new void Close()
        {
            base.Close();
        }

        /// <summary>
        /// 获取主窗口
        /// </summary>
        private static Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }
    }
}