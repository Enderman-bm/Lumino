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

        public MainWindow()
        {
            InitializeComponent();
            _logger = new EnderLogger("MainWindow");
            _logger.Info("Initialization", "[EnderDebugger][{DateTime.Now}][EnderLogger][MainWindow] 主窗口已初始化。");
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            
            // 订阅渲染事件以更新帧率
            // 这确保了无论Vulkan是否工作，我们都能获得UI渲染的帧率
            if (VisualRoot is IRenderRoot renderRoot)
            {
                // 使用Renderer.Diagnostics.DebugOverlays可能会更好，但这里我们手动计算
                // 注意：Avalonia 11+ 可能有不同的API，这里假设是标准API
            }
            
            // 使用CompositionTarget.Rendering (如果可用) 或者直接挂钩到Renderer
            // 在Avalonia中，通常可以通过TopLevel.Renderer.Diagnostics获取信息，或者订阅Paint事件
            // 但为了简单起见，我们使用全局渲染事件或类似机制
            
            // 尝试订阅全局渲染事件
            // 注意：Avalonia没有WPF那样的CompositionTarget.Rendering静态事件
            // 但我们可以使用DispatcherTimer或者在Render循环中更新
            // 不过，为了准确反映"渲染"帧率，我们需要挂钩到渲染器
            
            // 既然我们在View层，我们可以直接在Render override中调用，或者使用Tick事件
            // 但MainWindow本身可能不常重绘。
            
            // 更好的方法是使用 TopLevel.RequestAnimationFrame
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                topLevel.RequestAnimationFrame(OnFrame);
            }

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
        private void OnFrame(TimeSpan time)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.UpdateFrameInfo();
            }
            
            // 请求下一帧
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                topLevel.RequestAnimationFrame(OnFrame);
            }
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            // 停止请求动画帧 (实际上RequestAnimationFrame是一次性的，所以不需要显式停止，只要不再请求即可)
            // 但我们需要确保在窗口关闭时不继续请求
            
            base.OnDetachedFromVisualTree(e);
        }
    }
}