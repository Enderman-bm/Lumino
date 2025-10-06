using Avalonia;
using Avalonia.Controls;
using Lumino.ViewModels;
using System.ComponentModel;

namespace Lumino.Views
{
    public partial class ProjectSettingsWindow : Window
    {
        public ProjectSettingsWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            // 监听ShouldClose属性，当需要关闭时关闭窗口
            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is ProjectSettingsViewModel viewModel)
                {
                    viewModel.PropertyChanged += OnViewModelPropertyChanged;
                }
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is ProjectSettingsViewModel viewModel && 
                e.PropertyName == nameof(ProjectSettingsViewModel.ShouldClose) && 
                viewModel.ShouldClose)
            {
                Close();
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // 清理事件订阅
            if (DataContext is ProjectSettingsViewModel viewModel)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }
            base.OnClosing(e);
        }
    }
}
