using Avalonia.Controls;
using Avalonia.Interactivity;
using Lumino.Services.Implementation;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

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
            
            Debug.WriteLine($"Vulkan基准测试结果:");
            Debug.WriteLine($"总帧数: {_benchmarkFrames}");
            Debug.WriteLine($"总时间: {totalTime:F2}秒");
            Debug.WriteLine($"平均FPS: {avgFps:F1}");
            Debug.WriteLine($"平均帧时间: {frameTime:F1}ms");
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