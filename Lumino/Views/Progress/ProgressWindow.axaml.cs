using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lumino.ViewModels.Progress;

namespace Lumino.Views.Progress
{
    /// <summary>
    /// 进度窗口代码隐藏
    /// </summary>
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
        }

        public ProgressWindow(ProgressViewModel viewModel) : this()
        {
            DataContext = viewModel;
            
            // 监听ViewModel的关闭请求
            if (viewModel != null)
            {
                viewModel.CloseRequested += (sender, args) => Close();
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// 更新进度（线程安全）
        /// </summary>
        /// <param name="progress">进度值 (0-100)</param>
        /// <param name="statusText">状态文本</param>
        /// <param name="detailText">详细信息</param>
        public void UpdateProgress(double progress, string? statusText = null, string? detailText = null)
        {
            if (DataContext is ProgressViewModel vm)
            {
                // 使用Dispatcher确保UI更新在主线程中执行
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.UpdateProgress(progress, statusText, detailText);
                });
            }
        }

        /// <summary>
        /// 设置为不确定的进度状态
        /// </summary>
        /// <param name="statusText">状态文本</param>
        public void SetIndeterminate(string statusText = "正在处理...")
        {
            if (DataContext is ProgressViewModel vm)
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.IsIndeterminate = true;
                    vm.StatusText = statusText;
                });
            }
        }

        /// <summary>
        /// 设置为确定的进度状态
        /// </summary>
        public void SetDeterminate()
        {
            if (DataContext is ProgressViewModel vm)
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.IsIndeterminate = false;
                });
            }
        }
    }
}