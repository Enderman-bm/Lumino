using Avalonia.Controls;
using Avalonia.Interactivity;
using Lumino.Services.Implementation;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EnderDebugger;

namespace Lumino.Views.Testing
{
    public partial class VulkanTestWindow : Window
    {
        private readonly Stopwatch _benchmarkTimer = new Stopwatch();
        private int _benchmarkFrames = 0;
        private bool _isBenchmarking = false;

        public VulkanTestWindow()
        {
            InitializeComponent();
            
            // 绑定事件
            VulkanToggle.IsCheckedChanged += OnVulkanToggleChanged;
            RefreshButton.Click += OnRefreshClicked;
            BenchmarkButton.Click += OnBenchmarkClicked;
            
            // 检查Vulkan支持状态
            UpdateStatusText();
        }

        private void InitializeComponent()
        {
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        }

        private void OnVulkanToggleChanged(object? sender, RoutedEventArgs e)
        {
            if (TestControl != null)
            {
                TestControl.EnableVulkan = VulkanToggle.IsChecked ?? false;
                // 如果用户打开 Vulkan 开关且服务尚未初始化，尝试在窗口句柄可用时初始化 Vulkan（测试用途，安全包裹）
                try
                {
                    var vulkanService = Lumino.Services.Implementation.VulkanRenderService.Instance;
                    if ((VulkanToggle.IsChecked ?? false) && !vulkanService.IsInitialized)
                    {
                        // 平台句柄获取方法在不同平台/版本可能不同，为避免跨平台访问错误，先做一个保守尝试：
                        // 传入 IntPtr.Zero 触发 Vulkan 初始化尝试（若底层需要有效句柄会失败并被捕获）；
                        // 这样可以记录初始化尝试的结果到日志以便诊断，而不会因访问受保护成员而编译失败。
                        try
                        {
                            var ok = vulkanService.Initialize((nint)IntPtr.Zero);
                            EnderDebugger.EnderLogger.Instance.Info("VulkanTestWindow", $"Vulkan 初始化尝试完成: success={ok}");
                        }
                        catch (Exception exInit)
                        {
                            EnderDebugger.EnderLogger.Instance.LogException(exInit, "VulkanTestWindow", "尝试初始化 Vulkan (IntPtr.Zero) 时出错");
                        }
                    }
                }
                catch (Exception ex)
                {
                    EnderDebugger.EnderLogger.Instance.LogException(ex, "VulkanTestWindow", "OnVulkanToggleChanged 内部处理出错");
                }
                UpdateStatusText();
            }
        }

        private void OnRefreshClicked(object? sender, RoutedEventArgs e)
        {
            TestControl?.InvalidateVisual();
            UpdateStatusText();
        }

        private async void OnBenchmarkClicked(object? sender, RoutedEventArgs e)
        {
            if (_isBenchmarking) return;

            _isBenchmarking = true;
            BenchmarkButton.IsEnabled = false;
            StatusText.Text = "状态: 性能测试中...";

            try
            {
                await RunBenchmark();
            }
            finally
            {
                _isBenchmarking = false;
                BenchmarkButton.IsEnabled = true;
                UpdateStatusText();
            }
        }

        private async Task RunBenchmark()
        {
            _benchmarkTimer.Restart();
            _benchmarkFrames = 0;

            // 运行5秒的基准测试
            var benchmarkDuration = TimeSpan.FromSeconds(5);
            var endTime = DateTime.Now + benchmarkDuration;

            while (DateTime.Now < endTime)
            {
                TestControl?.InvalidateVisual();
                _benchmarkFrames++;
                
                // 每帧后稍微延迟，避免完全占用CPU
                await Task.Delay(1);
            }

            _benchmarkTimer.Stop();

            // 计算结果
            var totalTime = _benchmarkTimer.Elapsed.TotalSeconds;
            var avgFps = _benchmarkFrames / totalTime;
            var frameTime = (totalTime * 1000) / _benchmarkFrames;

            StatusText.Text = $"基准测试完成: {avgFps:F1} FPS, {frameTime:F1}ms/帧";
            
            EnderLogger.Instance.Info("Vulkan基准测试", "基准测试结果:");
            EnderLogger.Instance.Info("Vulkan基准测试", $"总帧数: {_benchmarkFrames}");
            EnderLogger.Instance.Info("Vulkan基准测试", $"总时间: {totalTime:F2}秒");
            EnderLogger.Instance.Info("Vulkan基准测试", $"平均FPS: {avgFps:F1}");
            EnderLogger.Instance.Info("Vulkan基准测试", $"平均帧时间: {frameTime:F1}ms");
        }

        private void UpdateStatusText()
        {
            var vulkanService = VulkanRenderService.Instance;
            var isVulkanEnabled = TestControl?.EnableVulkan ?? false;
            var isVulkanAvailable = vulkanService.IsInitialized;

            string status;
            if (isVulkanEnabled && isVulkanAvailable)
            {
                var stats = vulkanService.GetRenderStats();
                status = $"状态: Vulkan已启用 | 帧数: {stats.TotalFrames} | 错误: {stats.ErrorCount}";
            }
            else if (isVulkanEnabled && !isVulkanAvailable)
            {
                status = "状态: Vulkan不可用，使用Skia回退";
            }
            else
            {
                status = "状态: 使用Skia渲染";
            }

            StatusText.Text = status;
        }
    }
}