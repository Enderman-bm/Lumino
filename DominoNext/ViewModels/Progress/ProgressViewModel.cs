using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lumino.ViewModels.Progress
{
    /// <summary>
    /// ���ȴ��ڵ�ViewModel
    /// </summary>
    public partial class ProgressViewModel : ViewModelBase
    {
        private CancellationTokenSource? _cancellationTokenSource;

        #region ����

        /// <summary>
        /// ���ڱ���
        /// </summary>
        [ObservableProperty]
        private string title = "���ڴ���...";

        /// <summary>
        /// ��ǰ����ֵ (0-100)
        /// </summary>
        [ObservableProperty]
        private double currentProgress = 0;

        /// <summary>
        /// �����ı���ʾ
        /// </summary>
        [ObservableProperty]
        private string progressText = "0%";

        /// <summary>
        /// ״̬�ı�
        /// </summary>
        [ObservableProperty]
        private string statusText = "���ڳ�ʼ��...";

        /// <summary>
        /// ��ϸ��Ϣ�ı�
        /// </summary>
        [ObservableProperty]
        private string detailText = "";

        /// <summary>
        /// �Ƿ���ʾ��ϸ��Ϣ
        /// </summary>
        [ObservableProperty]
        private bool showDetail = false;

        /// <summary>
        /// �Ƿ����ȡ������
        /// </summary>
        [ObservableProperty]
        private bool canCancel = false;

        /// <summary>
        /// �Ƿ�Ϊ��ȷ���Ľ��ȣ��޷�Ԥ֪���ʱ�䣩
        /// </summary>
        [ObservableProperty]
        private bool isIndeterminate = false;

        /// <summary>
        /// ����ֵ���ԣ�����UI��
        /// </summary>
        public double Progress => CurrentProgress;

        /// <summary>
        /// ȡ������
        /// </summary>
        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

        #endregion

        #region �¼�

        /// <summary>
        /// ����رմ����¼�
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// ȡ�������¼�
        /// </summary>
        public event EventHandler? OperationCancelled;

        #endregion

        #region ���캯��

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

        #region ����

        /// <summary>
        /// ���½���
        /// </summary>
        /// <param name="progressValue">����ֵ (0-100)</param>
        /// <param name="status">״̬�ı�</param>
        /// <param name="detail">��ϸ��Ϣ</param>
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
        /// ��ɲ���
        /// </summary>
        public void Complete()
        {
            CurrentProgress = 100;
            ProgressText = "100%";
            StatusText = "���";
            OnPropertyChanged(nameof(Progress));
            
            // �ӳ�һ��ʱ�����û��������״̬
            Task.Delay(500).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                });
            });
        }

        /// <summary>
        /// ���ô���״̬
        /// </summary>
        /// <param name="errorMessage">������Ϣ</param>
        public void SetError(string errorMessage)
        {
            StatusText = $"����: {errorMessage}";
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

        #region ����

        /// <summary>
        /// ȡ����������
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                StatusText = "����ȡ��...";
                CanCancel = false;
                
                OperationCancelled?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region ���Ա仯����

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