using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using EnderDebugger;

namespace LuminoRenderEngine.Vulkan
{
    /// <summary>
    /// Vulkan上下文 - 管理Vulkan实例、设备和相关资源
    /// </summary>
    public class VulkanContext : IDisposable
    {
        private readonly EnderLogger _logger = new EnderLogger("VulkanContext");
        private bool _disposed = false;

        // Vulkan实例和核心对象
        public Instance Instance { get; private set; }
        public PhysicalDevice PhysicalDevice { get; private set; }
        public Device Device { get; private set; }
        public Queue Queue { get; private set; }
        public uint QueueFamilyIndex { get; private set; }
        public CommandPool CommandPool { get; private set; }

        // 扩展和函数指针
        private KhrSurface? _khrSurface;
        private KhrSwapchain? _khrSwapchain;
        public SurfaceKHR Surface { get; private set; }

        // 分配器
        private VkAllocationCallbacks? _allocator;

        public bool IsValid => Instance.Handle != 0 && Device.Handle != 0;

        /// <summary>
        /// 初始化Vulkan上下文
        /// </summary>
        public unsafe void Initialize()
        {
            try
            {
                _logger.Info("VulkanContext", "开始初始化Vulkan上下文");

                // 创建Vulkan实例
                CreateInstance();
                _logger.Info("VulkanContext", "Vulkan实例创建成功");

                // 选择物理设备
                SelectPhysicalDevice();
                _logger.Info("VulkanContext", "物理设备选择完成");

                // 创建逻辑设备
                CreateLogicalDevice();
                _logger.Info("VulkanContext", "逻辑设备创建完成");

                // 创建命令池
                CreateCommandPool();
                _logger.Info("VulkanContext", "命令池创建完成");

                _logger.Info("VulkanContext", "Vulkan上下文初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error("VulkanContext", $"Vulkan上下文初始化失败: {ex.Message}");
                Cleanup(); // 初始化失败时清理已创建的资源
                throw;
            }
        }

        /// <summary>
        /// 创建Vulkan实例
        /// </summary>
        private unsafe void CreateInstance()
        {
            _logger.Info("VulkanContext", "创建Vulkan实例");

            var vk = Vk.GetApi();
            
            // 应用程序信息
            var appName = new MemoryHelper.AllocateAndWriteString(new(HeapAllocationCallbacks), "Lumino Editor");
            var engineName = new MemoryHelper.AllocateAndWriteString(new(HeapAllocationCallbacks), "Lumino Render Engine");
            
            var appInfo = new ApplicationInfo()
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = appName,
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = engineName,
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version12
            };

            // 获取可用的实例扩展
            uint extensionCount = 0;
            vk.EnumerateInstanceExtensionProperties(null, ref extensionCount, null);
            var extensions = new ExtensionProperties[extensionCount];
            fixed (ExtensionProperties* extensionsPtr = extensions)
            {
                vk.EnumerateInstanceExtensionProperties(null, ref extensionCount, extensionsPtr);
            }

            _logger.Info("VulkanContext", $"找到 {extensionCount} 个可用的实例扩展:");
            foreach (var ext in extensions)
            {
                var extName = SilkMarshal.PtrToString(new IntPtr(ext.ExtensionName));
                _logger.Debug("VulkanContext", $"  - {extName} (版本: {ext.SpecVersion})");
            }

            // 选择需要的扩展
            var requiredExtensions = new List<string>();
            
            // 检查是否有调试扩展可用
            var debugExtensions = extensions.Where(ext => 
                SilkMarshal.PtrToString(new IntPtr(ext.ExtensionName)) == "VK_EXT_debug_utils").ToList();
                
            if (debugExtensions.Any())
            {
                requiredExtensions.Add("VK_EXT_debug_utils");
                _logger.Info("VulkanContext", "启用VK_EXT_debug_utils扩展");
            }

            // 实例创建信息
            var createInfo = new InstanceCreateInfo()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationName = appName,
                PEngineName = engineName,
                ApiVersion = Vk.Version12,
                EnabledExtensionCount = (uint)requiredExtensions.Count,
                PpEnabledExtensionNames = SilkMarshal.StringArrayToPtr(requiredExtensions.ToArray())
            };

            // 如果启用了调试扩展，设置调试回调
            var debugCreateInfo = new DebugUtilsMessengerCreateInfoEXT();
            if (requiredExtensions.Contains("VK_EXT_debug_utils"))
            {
                debugCreateInfo = new DebugUtilsMessengerCreateInfoEXT
                {
                    SType = StructureType.DebugUtilsMessengerCreateInfoExt,
                    MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                    DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                    DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
                    MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                                DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
                    PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback
                };
                
                createInfo.PNext = &debugCreateInfo;
            }

            // 创建实例
            var result = vk.CreateInstance(createInfo, _allocator, out Instance);
            if (result != Result.Success)
            {
                throw new Exception($"创建Vulkan实例失败，错误码: {result}");
            }

            // 释放临时字符串
            SilkMarshal.Free(appName);
            SilkMarshal.Free(engineName);
            SilkMarshal.Free(createInfo.PpEnabledExtensionNames);

            // 保存Vulkan API引用
            vk.LoadInstance(Instance);

            _logger.Info("VulkanContext", "Vulkan实例创建成功");
        }

        /// <summary>
        /// 调试回调函数
        /// </summary>
        private unsafe uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, 
            DebugUtilsMessageTypeFlagsEXT messageTypes, 
            DebugUtilsMessengerCallbackDataEXT* pCallbackData, 
            void* pUserData)
        {
            var message = SilkMarshal.PtrToString(new IntPtr(pCallbackData->PMessage));
            
            if (messageSeverity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt))
            {
                _logger.Error("VulkanValidation", message);
            }
            else if (messageSeverity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.WarningBitExt))
            {
                _logger.Warn("VulkanValidation", message);
            }
            else if (messageSeverity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.InfoBitExt))
            {
                _logger.Info("VulkanValidation", message);
            }
            else if (messageSeverity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt))
            {
                _logger.Debug("VulkanValidation", message);
            }

            return Vk.False; // 不阻止消息
        }

        /// <summary>
        /// 选择物理设备
        /// </summary>
        private unsafe void SelectPhysicalDevice()
        {
            _logger.Info("VulkanContext", "开始选择物理设备");

            var vk = Vk.GetApi();

            // 枚举物理设备
            uint deviceCount = 0;
            vk.EnumeratePhysicalDevices(Instance, ref deviceCount, null);
            
            if (deviceCount == 0)
            {
                throw new Exception("未找到Vulkan支持的物理设备");
            }

            var devices = new PhysicalDevice[deviceCount];
            fixed (PhysicalDevice* devicesPtr = devices)
            {
                vk.EnumeratePhysicalDevices(Instance, ref deviceCount, devicesPtr);
            }

            _logger.Info("VulkanContext", $"找到 {deviceCount} 个物理设备");

            // 寻找合适的设备
            PhysicalDevice selectedDevice = default;
            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                var props = vk.GetPhysicalDeviceProperties(device);
                var deviceName = SilkMarshal.PtrToString(new IntPtr(props.DeviceName));
                
                _logger.Info("VulkanContext", $"设备 {i}: {deviceName}, 类型: {props.DeviceType}");
                
                // 优先选择独立显卡
                if (props.DeviceType == PhysicalDeviceType.DiscreteGpu)
                {
                    selectedDevice = device;
                    _logger.Info("VulkanContext", $"选择设备: {deviceName} (独立显卡)");
                    break;
                }
                // 如果没有独立显卡，选择集成显卡
                else if (selectedDevice.Handle == 0 && props.DeviceType == PhysicalDeviceType.IntegratedGpu)
                {
                    selectedDevice = device;
                    _logger.Info("VulkanContext", $"选择设备: {deviceName} (集成显卡)");
                }
            }

            if (selectedDevice.Handle == 0)
            {
                selectedDevice = devices[0]; // 选择第一个设备
                var props = vk.GetPhysicalDeviceProperties(selectedDevice);
                var deviceName = SilkMarshal.PtrToString(new IntPtr(props.DeviceName));
                _logger.Info("VulkanContext", $"选择默认设备: {deviceName}");
            }

            PhysicalDevice = selectedDevice;

            // 检查队列族
            uint queueFamilyCount = 0;
            vk.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, ref queueFamilyCount, null);
            
            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            {
                vk.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, ref queueFamilyCount, queueFamiliesPtr);
            }

            // 寻找支持图形的队列族
            for (uint i = 0; i < queueFamilies.Length; i++)
            {
                if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                {
                    QueueFamilyIndex = i;
                    _logger.Info("VulkanContext", $"找到图形队列族: {i}");
                    break;
                }
            }

            if (QueueFamilyIndex == uint.MaxValue)
            {
                throw new Exception("未找到支持图形的队列族");
            }
        }

        /// <summary>
        /// 创建逻辑设备
        /// </summary>
        private unsafe void CreateLogicalDevice()
        {
            _logger.Info("VulkanContext", "创建逻辑设备");

            var vk = Vk.GetApi();

            // 队列创建信息
            float queuePriority = 1.0f;
            var queueCreateInfo = new DeviceQueueCreateInfo()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = QueueFamilyIndex,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            // 设备创建信息
            var deviceCreateInfo = new DeviceCreateInfo()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                EnabledExtensionCount = 0,
                PpEnabledExtensionNames = null
            };

            // 创建设备
            var result = vk.CreateDevice(PhysicalDevice, deviceCreateInfo, _allocator, out Device);
            if (result != Result.Success)
            {
                throw new Exception($"创建Vulkan设备失败，错误码: {result}");
            }

            // 获取图形队列
            vk.GetDeviceQueue(Device, QueueFamilyIndex, 0, out Queue);

            _logger.Info("VulkanContext", "逻辑设备创建成功");
        }

        /// <summary>
        /// 创建命令池
        /// </summary>
        private unsafe void CreateCommandPool()
        {
            _logger.Info("VulkanContext", "创建命令池");

            var vk = Vk.GetApi();

            var commandPoolCreateInfo = new CommandPoolCreateInfo()
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = QueueFamilyIndex,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit // 允许重置命令缓冲区
            };

            var result = vk.CreateCommandPool(Device, commandPoolCreateInfo, _allocator, out CommandPool);
            if (result != Result.Success)
            {
                throw new Exception($"创建命令池失败，错误码: {result}");
            }

            _logger.Info("VulkanContext", "命令池创建成功");
        }

        /// <summary>
        /// 等待设备空闲
        /// </summary>
        public void WaitIdle()
        {
            if (!_disposed && Device.Handle != 0)
            {
                var vk = Vk.GetApi();
                vk.DeviceWaitIdle(Device);
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            if (!_disposed)
            {
                _logger.Info("VulkanContext", "开始清理Vulkan资源");
                
                if (Device.Handle != 0)
                {
                    var vk = Vk.GetApi();
                    
                    // 等待设备空闲
                    vk.DeviceWaitIdle(Device);
                    
                    // 销毁命令池
                    if (CommandPool.Handle != 0)
                    {
                        vk.DestroyCommandPool(Device, CommandPool, _allocator);
                        CommandPool = default;
                    }
                    
                    // 销毁逻辑设备
                    vk.DestroyDevice(Device, _allocator);
                    Device = default;
                }
                
                // 销毁实例
                if (Instance.Handle != 0)
                {
                    var vk = Vk.GetApi();
                    vk.DestroyInstance(Instance, _allocator);
                    Instance = default;
                }
                
                _logger.Info("VulkanContext", "Vulkan资源清理完成");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Cleanup();
            }
        }
    }
}