// 文件用途：
// MainWindow 是应用程序的主窗口类，负责初始化和显示主界面。
// 使用限制：
// 1. 仅供 Lumino 项目使用。
// 2. 修改此文件需经过代码审查。

using System;
using Avalonia.Controls;
using Avalonia.Rendering;
using EnderDebugger;
using Lumino.ViewModels;

namespace Lumino.Views
{
    public partial class MainWindow : Window
    {
        private readonly EnderLogger _logger;

        public MainWindow()
        {
            InitializeComponent();
            _logger = new EnderLogger("MainWindow");
            _logger.Info("Initialization", "[EnderDebugger][{DateTime.Now}][EnderLogger][MainWindow] 主窗口已初始化。");
            
            // 添加渲染事件处理程序
            this.AttachedToVisualTree += OnAttachedToVisualTree;
        }
        
        private void OnAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            // 获取渲染器并添加渲染事件处理程序
            var renderer = this.GetRenderer();
            if (renderer != null)
            {
                renderer.SceneInvalidated += OnSceneInvalidated;
            }
        }
        
        private void OnSceneInvalidated(object sender, EventArgs e)
        {
            // 在每次渲染时更新帧信息
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.UpdateFrameInfo();
            }
        }
    }
}