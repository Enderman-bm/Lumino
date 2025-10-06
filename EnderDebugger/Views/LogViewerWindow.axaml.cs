using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using EnderDebugger.ViewModels;
using System;

namespace EnderDebugger.Views
{
    public partial class LogViewerWindow : Window
    {
        public LogViewerWindow()
        {
            InitializeComponent();
            
            // 创建并设置ViewModel
            var viewModel = new LogViewerViewModel();
            DataContext = viewModel;
            viewModel.Window = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}