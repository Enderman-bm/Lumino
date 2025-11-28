// 文件用途：
// MainWindow 是应用程序的主窗口类，负责初始化和显示主界面。
// 使用限制：
// 1. 仅供 Lumino 项目使用。
// 2. 修改此文件需经过代码审查。

using System;
using Avalonia.Controls;
using EnderDebugger;
using Lumino.ViewModels;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

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
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            
            // 初始化VulkanRenderService
            try
            {
                var windowHandle = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                _logger.Info("MainWindow", $"窗口句柄: {windowHandle}");
                
                // 初始化VulkanRenderService
                var vulkanRenderService = Lumino.Services.Implementation.VulkanRenderService.Instance;
                if (vulkanRenderService.Initialize(windowHandle))
                {
                    _logger.Info("MainWindow", "VulkanRenderService 初始化成功");
                }
                else
                {
                    _logger.Error("MainWindow", "VulkanRenderService 初始化失败");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("MainWindow", $"初始化VulkanRenderService时发生错误: {ex.Message}");
            }
        }
    }
}