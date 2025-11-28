using System;
using System.Threading.Tasks;
using EnderDebugger;
using LuminoRenderEngine.Vulkan;

namespace LuminoRenderEngine.Examples
{
    /// <summary>
    /// Vulkan初始化测试
    /// </summary>
    public class VulkanInitializationTest
    {
        private readonly EnderLogger _logger = new EnderLogger("VulkanInitializationTest");

        public async Task RunAsync()
        {
            Console.WriteLine("开始Vulkan初始化测试...");
            _logger.Info("VulkanTest", "开始Vulkan初始化测试");

            try
            {
                // 启用调试模式以查看详细日志
                EnderLogger.Instance.EnableDebugMode("debug");

                // 创建并初始化Vulkan渲染管理器
                var renderManager = new VulkanRenderManager();
                
                Console.WriteLine("正在初始化Vulkan...");
                renderManager.Initialize();

                if (renderManager.IsInitialized && renderManager.Context != null && renderManager.Context.IsValid)
                {
                    Console.WriteLine("✓ Vulkan初始化成功！");
                    _logger.Info("VulkanTest", "Vulkan初始化成功");
                    
                    // 显示一些设备信息
                    var vk = Silk.NET.Vulkan.Vk.GetApi();
                    var props = vk.GetPhysicalDeviceProperties(renderManager.Context.PhysicalDevice);
                    var deviceName = Silk.NET.Core.SilkMarshal.PtrToString(new IntPtr(props.DeviceName));
                    
                    Console.WriteLine($"  - 设备名称: {deviceName}");
                    Console.WriteLine($"  - 设备类型: {props.DeviceType}");
                    Console.WriteLine($"  - Vulkan版本: {props.ApiVersion}");
                    Console.WriteLine($"  - 驱动版本: {props.DriverVersion}");
                    
                    _logger.Info("VulkanTest", $"设备名称: {deviceName}");
                    _logger.Info("VulkanTest", $"设备类型: {props.DeviceType}");
                    _logger.Info("VulkanTest", $"Vulkan版本: {props.ApiVersion}");
                }
                else
                {
                    Console.WriteLine("✗ Vulkan初始化失败！");
                    _logger.Error("VulkanTest", "Vulkan初始化失败或上下文无效");
                }

                // 清理资源
                renderManager.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Vulkan初始化测试失败: {ex.Message}");
                Console.WriteLine($"  详细信息: {ex.StackTrace}");
                _logger.Error("VulkanTest", $"Vulkan初始化测试失败: {ex.Message}");
                _logger.LogException(ex, "VulkanTest", "Vulkan初始化异常");
            }

            Console.WriteLine("Vulkan初始化测试完成。");
            _logger.Info("VulkanTest", "Vulkan初始化测试完成");
        }
    }
}