using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using EnderDebugger;
using Lumino.Services.Implementation;
using Lumino.Views.Testing;
using System;
using System.Diagnostics;

namespace Lumino.Views.Pages
{
    public partial class VulkanDemoPage : UserControl
    {
        private static readonly EnderLogger _logger = EnderLogger.Instance;
        private readonly DispatcherTimer _statusTimer;

        public VulkanDemoPage()
        {
            InitializeComponent();
            
            // 绑定事件
            OpenTestWindowButton.Click += OnOpenTestWindowClicked;
            ToggleRendererButton.Click += OnToggleRendererClicked;
            
            // 设置状态更新定时器
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _statusTimer.Tick += OnStatusTimerTick;
            _statusTimer.Start();
            
            // 初始状态更新
            UpdateStatus();
        }

        private void InitializeComponent()
        {
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        }

        private void OnOpenTestWindowClicked(object? sender, RoutedEventArgs e)
        {
            try
            {
                var testWindow = new VulkanTestWindow
                {
                    Width = 1000,
                    Height = 700
                };
                testWindow.Show();
            }
            catch (Exception ex)
            {
                _logger.Error("OnOpenTestWindowClicked", $"打开测试窗口失败: {ex.Message}");
            }
        }

        private void OnToggleRendererClicked(object? sender, RoutedEventArgs e)
        {
            if (DemoRenderer != null)
            {
                // 切换渲染器状态
                var currentState = DemoRenderer.EnableVulkan;
                DemoRenderer.EnableVulkan = !currentState;
                
                UpdateStatus();
            }
        }

        private void OnStatusTimerTick(object? sender, EventArgs e)
        {
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            try
            {
                var vulkanService = VulkanRenderService.Instance;
                var isVulkanAvailable = vulkanService.IsInitialized;
                var isUsingVulkan = DemoRenderer?.EnableVulkan ?? false && isVulkanAvailable;

                // 更新渲染器状态
                RendererStatusText.Text = isUsingVulkan ? "渲染器: Vulkan ✓" : "渲染器: Skia ✓";
                
                // 更新性能信息
                if (isUsingVulkan)
                {
                    var stats = vulkanService.GetRenderStats();
                    PerformanceText.Text = $"性能: {stats.TotalFrames} 帧已渲染";
                    FeatureText.Text = "特性: MSAA, VSync, 抗锯齿";
                }
                else
                {
                    PerformanceText.Text = "性能: 使用Skia软件渲染";
                    FeatureText.Text = isVulkanAvailable ? "特性: Vulkan可用但未启用" : "特性: Vulkan不可用";
                }

                // 更新按钮文本
                if (ToggleRendererButton != null)
                {
                    ToggleRendererButton.Content = isUsingVulkan ? "切换到Skia" : "切换到Vulkan";
                }
            }
            catch (Exception ex)
            {
                _logger.Error("UpdateStatus", $"更新状态失败: {ex.Message}");
                RendererStatusText.Text = "渲染器: 状态检测失败";
                PerformanceText.Text = "性能: 检测失败";
                FeatureText.Text = "特性: 检测失败";
            }
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            
            // 清理定时器
            _statusTimer?.Stop();
        }
    }
}