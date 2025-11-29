using System;
using System.IO;
using System.Text.Json;
using EnderDebugger;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// Vulkan渲染配置管理
    /// </summary>
    public class VulkanConfiguration
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Lumino",
            "vulkan_config.json"
        );

        /// <summary>
        /// 是否启用Vulkan渲染
        /// </summary>
        public bool EnableVulkan { get; set; } = true;

        /// <summary>
        /// 验证层级别 (None, Standard, Verbose)
        /// </summary>
        public ValidationLevel ValidationLevel { get; set; } = ValidationLevel.None;

        /// <summary>
        /// 首选GPU索引 (-1表示自动选择)
        /// </summary>
        public int PreferredGpuIndex { get; set; } = -1;

        /// <summary>
        /// 是否启用MSAA抗锯齿
        /// </summary>
        public bool EnableMsaa { get; set; } = true;

        /// <summary>
        /// MSAA采样数 (2, 4, 8, 16)
        /// </summary>
        public int MsaaSamples { get; set; } = 4;

        /// <summary>
        /// 是否启用VSync
        /// </summary>
        public bool EnableVSync { get; set; } = true;

        /// <summary>
        /// 最大帧缓冲大小
        /// </summary>
        public int MaxFramebufferSize { get; set; } = 4096;

        /// <summary>
        /// 是否启用调试信息
        /// </summary>
        public bool EnableDebugInfo { get; set; } = false;

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static VulkanConfiguration Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<VulkanConfiguration>(json) ?? new VulkanConfiguration();
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanConfiguration", "加载Vulkan配置失败");
            }

            return new VulkanConfiguration();
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanConfiguration", "保存Vulkan配置失败");
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefaults()
        {
            EnableVulkan = true;
            ValidationLevel = ValidationLevel.None;
            PreferredGpuIndex = -1;
            EnableMsaa = true;
            MsaaSamples = 4;
            EnableVSync = true;
            MaxFramebufferSize = 4096;
            EnableDebugInfo = false;
        }
    }

    /// <summary>
    /// 验证层级别
    /// </summary>
    public enum ValidationLevel
    {
        None = 0,
        Standard = 1,
        Verbose = 2
    }
}