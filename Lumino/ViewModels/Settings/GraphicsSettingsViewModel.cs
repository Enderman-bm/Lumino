using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumino.Services.Implementation;

namespace Lumino.ViewModels.Settings
{
    public partial class GraphicsSettingsViewModel : ViewModelBase
    {
        private readonly DispatcherTimer _timer;
        private PerformanceCounter? _gpuCounter;

        [ObservableProperty]
        private string _renderMode = "Skia";

        [ObservableProperty]
        private string _gpuName = "Unknown";

        [ObservableProperty]
        private string _gpuUsage = "N/A";

        [ObservableProperty]
        private bool _isVulkan = false;

        [ObservableProperty]
        private string _vulkanApiVersion = "";

        [ObservableProperty]
        private string _vulkanDriverVersion = "";

        public Dictionary<string, string> VulkanDetails { get; private set; } = new();

        public GraphicsSettingsViewModel()
        {
            Initialize();
            
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void Initialize()
        {
            var vulkanService = VulkanRenderService.Instance;
            if (vulkanService.IsInitialized && vulkanService.VulkanManager != null)
            {
                RenderMode = "Vulkan";
                IsVulkan = true;
                GpuName = vulkanService.VulkanManager.GetDeviceName();
                VulkanApiVersion = vulkanService.VulkanManager.GetApiVersion();
                VulkanDriverVersion = vulkanService.VulkanManager.GetDriverVersion();
                VulkanDetails = vulkanService.VulkanManager.GetDetailedInfo();
            }
            else
            {
                RenderMode = "Skia";
                IsVulkan = false;
                GpuName = "Default / Software";
            }

            InitializeGpuCounter();
        }

        private void InitializeGpuCounter()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GpuUsage = "N/A (Non-Windows)";
                return;
            }

            try
            {
                // 尝试查找 GPU 计数器
                // 注意：这需要 System.Diagnostics.PerformanceCounter 包
                // 如果项目中没有引用，这里会编译失败
                // 我们先尝试一下，如果失败再移除
                
                #pragma warning disable CA1416 // 验证平台兼容性
                var category = new PerformanceCounterCategory("GPU Engine");
                var instanceNames = category.GetInstanceNames();
                
                var pid = Environment.ProcessId.ToString();
                foreach (var name in instanceNames)
                {
                    if (name.Contains($"pid_{pid}") && name.EndsWith("engtype_3D"))
                    {
                        _gpuCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name);
                        break;
                    }
                }
                #pragma warning restore CA1416
            }
            catch
            {
                GpuUsage = "N/A";
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_gpuCounter != null)
            {
                try
                {
                    #pragma warning disable CA1416
                    var usage = _gpuCounter.NextValue();
                    GpuUsage = $"{usage:F1}%";
                    #pragma warning restore CA1416
                }
                catch
                {
                    GpuUsage = "Error";
                }
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _gpuCounter?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
