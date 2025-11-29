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
using Avalonia.Rendering;

namespace Lumino.Views
{
    public partial class MainWindow : Window
    {
        private readonly EnderLogger _logger;
        private bool _isFrameRequested = false;

        public MainWindow()
        {
            InitializeComponent();
            _logger = new EnderLogger("MainWindow");
            _logger.Info("Initialization", "[EnderDebugger][{DateTime.Now}][EnderLogger][MainWindow] 主窗口已初始化。");
            
            // 监听Activated事件以确保动画帧循环在窗口激活时运行
            this.Activated += (s, e) => RequestAnimationFrame();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            _logger.Info("MainWindow", "窗口已打开，开始请求动画帧");
            RequestAnimationFrame();
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _logger.Info("MainWindow", "已附加到可视树，尝试请求动画帧");
            RequestAnimationFrame();

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
        private void RequestAnimationFrame()
        {
            if (_isFrameRequested) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                _isFrameRequested = true;
                topLevel.RequestAnimationFrame(OnFrame);
            }
            else
            {
                // 如果无法获取TopLevel，可能是窗口还未完全初始化
                // 这种情况下我们不记录错误，因为在OnAttachedToVisualTree等早期阶段这是正常的
                // 只要在OnOpened或OnActivated中能成功即可
            }
        }

        private void OnFrame(TimeSpan time)
        {
            _isFrameRequested = false;
            
            if (DataContext is MainWindowViewModel vm)
            {
                vm.UpdateFrameInfo();
            }
            
            // 请求下一帧
            RequestAnimationFrame();
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            // 停止请求动画帧 (实际上RequestAnimationFrame是一次性的，所以不需要显式停止，只要不再请求即可)
            // 但我们需要确保在窗口关闭时不继续请求
            
            base.OnDetachedFromVisualTree(e);
        }
    }
}