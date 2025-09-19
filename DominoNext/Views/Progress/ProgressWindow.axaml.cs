using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lumino.ViewModels.Progress;

namespace Lumino.Views.Progress
{
    /// <summary>
    /// ���ȴ��ڴ�������
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
            
            // ����ViewModel�Ĺر�����
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
        /// ���½��ȣ��̰߳�ȫ��
        /// </summary>
        /// <param name="progress">����ֵ (0-100)</param>
        /// <param name="statusText">״̬�ı�</param>
        /// <param name="detailText">��ϸ��Ϣ</param>
        public void UpdateProgress(double progress, string? statusText = null, string? detailText = null)
        {
            if (DataContext is ProgressViewModel vm)
            {
                // ʹ��Dispatcherȷ��UI���������߳���ִ��
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.UpdateProgress(progress, statusText, detailText);
                });
            }
        }

        /// <summary>
        /// ����Ϊ��ȷ���Ľ���״̬
        /// </summary>
        /// <param name="statusText">״̬�ı�</param>
        public void SetIndeterminate(string statusText = "���ڴ���...")
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
        /// ����Ϊȷ���Ľ���״̬
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