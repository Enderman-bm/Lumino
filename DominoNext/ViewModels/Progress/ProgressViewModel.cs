using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DominoNext.ViewModels.Progress
{
    /// <summary>
    /// 进度窗口的ViewModel
    /// </summary>
    public partial class ProgressViewModel : ViewModelBase
    {
        private CancellationTokenSource? _cancellationTokenSource;

        #region 属性

        /// <summary>
        /// 窗口标题
        /// </summary>
        [ObservableProperty]
        private string title = "正在处理...";

        /// <summary>
        /// 当前进度值 (0-100)
        /// </summary>
        [ObservableProperty]
        private double currentProgress = 0;

        /// <summary>
        /// 进度文本显示
        /// </summary>
        [ObservableProperty]
        private string progressText = "0%";

        /// <summary>
        /// 状态文本
        /// </summary>
        [ObservableProperty]
        private string statusText = "正在初始化...";

        /// <summary>
        /// 详细信息文本
        /// </summary>
        [ObservableProperty]
        private string detailText = "";

        /// <summary>
        /// 是否显示详细信息
        /// </summary>
        [ObservableProperty]
        private bool showDetail = false;

        /// <summary>
        /// 是否可以取消操作
        /// </summary>
        [ObservableProperty]
        private bool canCancel = false;

        /// <summary>
        /// 是否为不确定的进度（无法预知完成时间）
        /// </summary>
        [ObservableProperty]
        private bool isIndeterminate = false;

        /// <summary>
        /// 进度值属性，用于UI绑定
        /// </summary>
        public double Progress => CurrentProgress;

        /// <summary>
        /// 取消令牌
        /// </summary>
        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

        #endregion

        #region 事件

        /// <summary>
        /// 请求关闭窗口事件
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// 取消操作事件
        /// </summary>
        public event EventHandler? OperationCancelled;

        #endregion

        #region 构造函数

        public ProgressViewModel()
        {
        }

        public ProgressViewModel(string windowTitle, bool allowCancel = false)
        {
            Title = windowTitle;
            CanCancel = allowCancel;
            
            if (allowCancel)
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 更新进度
        /// </summary>
        /// <param name="progressValue">进度值 (0-100)</param>
        /// <param name="status">状态文本</param>
        /// <param name="detail">详细信息</param>
        public void UpdateProgress(double progressValue, string? status = null, string? detail = null)
        {
            CurrentProgress = Math.Max(0, Math.Min(100, progressValue));
            ProgressText = $"{CurrentProgress:F1}%";
            
            if (!string.IsNullOrEmpty(status))
                StatusText = status;
                
            if (!string.IsNullOrEmpty(detail))
            {
                DetailText = detail;
                ShowDetail = true;
            }
            
            OnPropertyChanged(nameof(Progress));
        }

        /// <summary>
        /// 完成操作
        /// </summary>
        public void Complete()
        {
            CurrentProgress = 100;
            ProgressText = "100%";
            StatusText = "完成";
            OnPropertyChanged(nameof(Progress));
            
            // 延迟一点时间让用户看到完成状态
            Task.Delay(500).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                });
            });
        }

        /// <summary>
        /// 设置错误状态
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        public void SetError(string errorMessage)
        {
            StatusText = $"错误: {errorMessage}";
            CanCancel = false;
            
            Task.Delay(2000).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                });
            });
        }

        #endregion

        #region 命令

        /// <summary>
        /// 取消操作命令
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                StatusText = "正在取消...";
                CanCancel = false;
                
                OperationCancelled?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region 属性变化处理

        partial void OnCurrentProgressChanged(double value)
        {
            ProgressText = IsIndeterminate ? "..." : $"{value:F1}%";
            OnPropertyChanged(nameof(Progress));
        }

        partial void OnIsIndeterminateChanged(bool value)
        {
            if (value)
            {
                ProgressText = "...";
            }
            else
            {
                ProgressText = $"{CurrentProgress:F1}%";
            }
        }

        #endregion

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}