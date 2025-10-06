using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumino.Models.Settings
{
    /// <summary>
    /// Vulkan渲染设置
    /// </summary>
    public partial class VulkanSettings : ObservableObject
    {
        [ObservableProperty]
        private bool _vulkanEnabled = true;

        [ObservableProperty]
        private bool _enableValidation = false;

        [ObservableProperty]
        private bool _enableMsaa = true;

        [ObservableProperty]
        private int _msaaSamples = 4;

        [ObservableProperty]
        private bool _enableVSync = true;

        [ObservableProperty]
        private int _maxFramesInFlight = 2;

        [ObservableProperty]
        private bool _enableGpuDebugging = false;

        [ObservableProperty]
        private int _maxGpuResourceSizeMB = 512;

        [ObservableProperty]
        private bool _enableComputeShaders = true;

        [ObservableProperty]
        private bool _enableAsyncCompute = true;

        [ObservableProperty]
        private int _computeWorkgroupSize = 256;

        [ObservableProperty]
        private bool _preferDiscreteGpu = true;

        [ObservableProperty]
        private bool _enableRayTracing = false;

        [ObservableProperty]
        private bool _enableMeshShaders = false;

        /// <summary>
        /// Vulkan是否可用
        /// </summary>
        public bool IsAvailable => Services.Implementation.VulkanRenderService.Instance.IsSupported;

        /// <summary>
        /// 是否启用Vulkan渲染
        /// </summary>
        public bool IsEnabled => IsAvailable && VulkanEnabled;

        /// <summary>
        /// 性能预设
        /// </summary>
        public enum PerformancePreset
        {
            [Description("最高性能")]
            MaximumPerformance,
            
            [Description("平衡")]
            Balanced,
            
            [Description("最高质量")]
            MaximumQuality,
            
            [Description("自定义")]
            Custom
        }

        [ObservableProperty]
        private PerformancePreset _currentPreset = PerformancePreset.Balanced;

        /// <summary>
        /// 应用性能预设
        /// </summary>
        public void ApplyPreset(PerformancePreset preset)
        {
            CurrentPreset = preset;
            
            switch (preset)
            {
                case PerformancePreset.MaximumPerformance:
                    EnableMsaa = false;
                    MsaaSamples = 1;
                    EnableVSync = false;
                    MaxFramesInFlight = 1;
                    EnableAsyncCompute = true;
                    ComputeWorkgroupSize = 512;
                    break;
                    
                case PerformancePreset.Balanced:
                    EnableMsaa = true;
                    MsaaSamples = 2;
                    EnableVSync = true;
                    MaxFramesInFlight = 2;
                    EnableAsyncCompute = true;
                    ComputeWorkgroupSize = 256;
                    break;
                    
                case PerformancePreset.MaximumQuality:
                    EnableMsaa = true;
                    MsaaSamples = 8;
                    EnableVSync = true;
                    MaxFramesInFlight = 3;
                    EnableAsyncCompute = false;
                    ComputeWorkgroupSize = 128;
                    break;
                    
                case PerformancePreset.Custom:
                    // 保持当前设置
                    break;
            }
        }

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        public void ResetToDefaults()
        {
            VulkanEnabled = true;
            EnableValidation = false;
            EnableMsaa = true;
            MsaaSamples = 4;
            EnableVSync = true;
            MaxFramesInFlight = 2;
            EnableGpuDebugging = false;
            MaxGpuResourceSizeMB = 512;
            EnableComputeShaders = true;
            EnableAsyncCompute = true;
            ComputeWorkgroupSize = 256;
            PreferDiscreteGpu = true;
            EnableRayTracing = false;
            EnableMeshShaders = false;
            CurrentPreset = PerformancePreset.Balanced;
        }

        /// <summary>
        /// 验证设置
        /// </summary>
        public bool Validate()
        {
            if (MsaaSamples < 1 || MsaaSamples > 16)
                return false;
                
            if (MaxFramesInFlight < 1 || MaxFramesInFlight > 4)
                return false;
                
            if (MaxGpuResourceSizeMB < 64 || MaxGpuResourceSizeMB > 4096)
                return false;
                
            if (ComputeWorkgroupSize < 64 || ComputeWorkgroupSize > 1024)
                return false;
                
            return true;
        }
    }
}