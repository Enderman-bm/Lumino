using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Core.Native;
using System.Linq;
using Silk.NET.Maths;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// Vulkan管理器 - 使用Silk.NET.Vulkan实现Vulkan功能
    /// </summary>
    public class VulkanManager : IDisposable
    {
        private readonly Vk _vk;
        private Instance _instance;
        private DebugUtilsMessengerEXT _debugMessenger;
        private SurfaceKHR? _surface = null;
        private PhysicalDevice _physicalDevice;
        private Device _device;
        private Queue _graphicsQueue;
        private Queue _presentQueue;
        private SwapchainKHR _swapchain;
        private Format _swapchainImageFormat;
        private Extent2D _swapchainExtent;
    private Image[]? _swapchainImages = null;
    private ImageView[]? _swapchainImageViews = null;
    private RenderPass _renderPass;
    private PipelineLayout? _pipelineLayout = null;
    private Pipeline? _graphicsPipeline = null;
    
    // 音符渲染专用管线
    private ShaderModule _vertShaderModule;
    private ShaderModule _fragShaderModule;
    private Pipeline _notePipeline;
    private PipelineLayout _notePipelineLayout;
    private Framebuffer[]? _framebuffers;
    private CommandPool _commandPool;
    private CommandBuffer[]? _commandBuffers;
    private Semaphore[]? _imageAvailableSemaphores;
    private Semaphore[]? _renderFinishedSemaphores;
    private Fence[]? _inFlightFences;
    private Fence[]? _imagesInFlight;
        private uint _currentFrame = 0;
        private bool _framebufferResized = false;
        private readonly VulkanConfiguration _configuration;

        // 渲染命令队列 - 用于动态渲染
        private readonly Queue<Action<CommandBuffer>> _renderCommands = new();
        private readonly object _renderLock = new();
    private ExtDebugUtils? _debugUtils;
    private KhrSurface? _khrSurface;
    private KhrSwapchain? _khrSwapchain;

        // 验证层
        private readonly string[] _validationLayers = { "VK_LAYER_KHRONOS_validation" };
        
        // 设备扩展
        private readonly string[] _deviceExtensions = { KhrSwapchain.ExtensionName };

        public VulkanManager()
        {
            _vk = Vk.GetApi();
            _configuration = VulkanConfiguration.Load();
        }

        /// <summary>
        /// 初始化Vulkan
        /// </summary>
        public unsafe bool Initialize(void* windowHandle)
        {
            try
            {
                CreateInstance();
                SetupDebugMessenger();
                CreateSurface(windowHandle);
                PickPhysicalDevice();
                CreateLogicalDevice();
                CreateSwapchain();
                CreateImageViews();
                CreateRenderPass();
                CreateGraphicsPipeline();
                CreateNotePipeline();
                CreateFramebuffers();
                CreateCommandPool();
                CreateCommandBuffers();
                CreateSyncObjects();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vulkan初始化失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 创建Vulkan实例
        /// </summary>
        private unsafe void CreateInstance()
        {
            // 检查验证层是否可用
            if (_configuration.ValidationLevel != ValidationLevel.None && !CheckValidationLayerSupport())
            {
                throw new Exception("验证层请求，但不可用！");
            }

            // 应用程序信息
            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Lumino"),
                ApplicationVersion = Vk.MakeVersion(1, 0, 0),
                PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
                EngineVersion = Vk.MakeVersion(1, 0, 0),
                ApiVersion = Vk.Version13
            };

            // 实例创建信息
            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo
            };

            // 获取需要的扩展
            var extensions = GetRequiredExtensions();
            createInfo.EnabledExtensionCount = (uint)extensions.Length;
            createInfo.PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToMemory(extensions);

            // 启用验证层
            if (_configuration.ValidationLevel != ValidationLevel.None)
            {
                createInfo.EnabledLayerCount = (uint)_validationLayers.Length;
                createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToMemory(_validationLayers);

                var debugCreateInfo = new DebugUtilsMessengerCreateInfoEXT();
                PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
                createInfo.PNext = &debugCreateInfo;
            }
            else
            {
                createInfo.EnabledLayerCount = 0;
                createInfo.PNext = null;
            }

            // 创建实例
            if (_vk.CreateInstance(ref createInfo, null, out _instance) != Result.Success)
            {
                throw new Exception("创建Vulkan实例失败！");
            }

            // 获取实例级别的函数指针
            _vk.CurrentInstance = _instance;
            
            // 获取扩展
            _vk.TryGetInstanceExtension(_instance, out _khrSurface);
            _vk.TryGetInstanceExtension(_instance, out _debugUtils);
            
            // 清理内存
            Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
            Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
            SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
            
            if (_configuration.ValidationLevel != ValidationLevel.None)
            {
                SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
            }
        }

        /// <summary>
        /// 设置调试消息
        /// </summary>
        private unsafe void SetupDebugMessenger()
        {
            if (_configuration.ValidationLevel == ValidationLevel.None)
                return;

            if (!_vk.TryGetInstanceExtension(_instance, out ExtDebugUtils debugUtils))
                return;

            var createInfo = new DebugUtilsMessengerCreateInfoEXT();
            PopulateDebugMessengerCreateInfo(ref createInfo);

            if (debugUtils.CreateDebugUtilsMessenger(_instance, ref createInfo, null, out _debugMessenger) != Result.Success)
            {
                throw new Exception("创建调试消息失败！");
            }

            _debugUtils = debugUtils;
        }

        /// <summary>
        /// 填充调试消息创建信息
        /// </summary>
        private unsafe void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
        {
            createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
            createInfo.MessageSeverity = 
                DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;
            createInfo.MessageType = 
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;

            if (_configuration.ValidationLevel == ValidationLevel.Verbose)
            {
                createInfo.MessageSeverity |= DebugUtilsMessageSeverityFlagsEXT.InfoBitExt;
            }

            createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
        }

        /// <summary>
        /// 调试回调函数
        /// </summary>
        private unsafe uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, 
            DebugUtilsMessageTypeFlagsEXT messageTypes,
            DebugUtilsMessengerCallbackDataEXT* pCallbackData, 
            void* pUserData)
        {
            var message = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage);
            
            if (messageSeverity == DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityErrorBitExt)
            {
                System.Diagnostics.Debug.WriteLine($"[Vulkan错误] {message}");
            }
            else if (messageSeverity == DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityWarningBitExt)
            {
                System.Diagnostics.Debug.WriteLine($"[Vulkan警告] {message}");
            }
            else if (_configuration.ValidationLevel == ValidationLevel.Verbose)
            {
                System.Diagnostics.Debug.WriteLine($"[Vulkan信息] {message}");
            }

            return Vk.False;
        }

        /// <summary>
        /// 创建表面
        /// </summary>
        public unsafe void CreateSurface(void* windowHandle)
        {
            // 注意：在实际实现中，这里需要根据平台创建表面
            // Windows: 使用vkCreateWin32SurfaceKHR
            // Linux: 使用vkCreateXlibSurfaceKHR
            // macOS: 使用vkCreateMacOSSurfaceMVK
            // 暂时跳过表面创建，假设已经通过其他方式创建
            System.Diagnostics.Debug.WriteLine("CreateSurface: 表面创建暂未实现");
        }

        /// <summary>
        /// 排队渲染命令
        /// </summary>
        public void EnqueueRenderCommand(Action<CommandBuffer> command)
        {
            lock (_renderLock)
            {
                _renderCommands.Enqueue(command);
            }
        }

        /// <summary>
        /// 清除渲染命令队列
        /// </summary>
        public void ClearRenderCommands()
        {
            lock (_renderLock)
            {
                _renderCommands.Clear();
            }
        }

        /// <summary>
        /// 选择物理设备
        /// </summary>
        private unsafe void PickPhysicalDevice()
        {
            uint deviceCount = 0;
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null);

            if (deviceCount == 0)
            {
                throw new Exception("找不到支持Vulkan的设备！");
            }

            var devices = new PhysicalDevice[deviceCount];
            fixed (PhysicalDevice* devicesPtr = devices)
            {
                _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devicesPtr);
            }

            // 选择合适的物理设备
            foreach (var device in devices)
            {
                if (IsDeviceSuitable(device))
                {
                    _physicalDevice = device;
                    break;
                }
            }

            if (_physicalDevice.Handle == 0)
            {
                throw new Exception("找不到合适的GPU设备！");
            }
        }

        /// <summary>
        /// 检查设备是否合适
        /// </summary>
        private unsafe bool IsDeviceSuitable(PhysicalDevice device)
        {
            var indices = FindQueueFamilies(device);
            bool extensionsSupported = CheckDeviceExtensionSupport(device);
            
            bool swapchainAdequate = false;
            if (extensionsSupported)
            {
                var swapchainSupport = QuerySwapchainSupport(device);
                swapchainAdequate = swapchainSupport.Formats.Length != 0 && swapchainSupport.PresentModes.Length != 0;
            }

            return indices.IsComplete() && extensionsSupported && swapchainAdequate;
        }

        /// <summary>
        /// 查找队列族
        /// </summary>
        private unsafe QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
        {
            var indices = new QueueFamilyIndices();

            uint queueFamilyCount = 0;
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            {
                _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);
            }

            for (int i = 0; i < queueFamilies.Length; i++)
            {
                var queueFamily = queueFamilies[i];
                
                // 图形队列
                if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                {
                    indices.GraphicsFamily = (uint)i;
                }

                // 呈现队列
                if (_khrSurface != null && _surface.HasValue)
                {
                    _khrSurface.GetPhysicalDeviceSurfaceSupport(device, (uint)i, _surface.Value, out var presentSupport);
                    if (presentSupport)
                    {
                        indices.PresentFamily = (uint)i;
                    }
                }

                if (indices.IsComplete())
                {
                    break;
                }
            }

            return indices;
        }

        /// <summary>
        /// 检查设备扩展支持
        /// </summary>
        private unsafe bool CheckDeviceExtensionSupport(PhysicalDevice device)
        {
            uint extensionCount = 0;
            _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, null);

            var availableExtensions = new ExtensionProperties[extensionCount];
            fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
            {
                _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, availableExtensionsPtr);
            }

            var availableExtensionNames = availableExtensions.Select(ext => Marshal.PtrToStringAnsi((nint)ext.ExtensionName)).ToHashSet();

            return _deviceExtensions.All(availableExtensionNames.Contains);
        }

        /// <summary>
        /// 查询交换链支持
        /// </summary>
        private unsafe SwapchainSupportDetails QuerySwapchainSupport(PhysicalDevice device)
        {
            var details = new SwapchainSupportDetails();

            if (_khrSurface != null && _surface.HasValue)
            {
                SurfaceCapabilitiesKHR capabilities;
                _khrSurface.GetPhysicalDeviceSurfaceCapabilities(device, _surface.Value, out capabilities);
                details.Capabilities = capabilities;

                uint formatCount = 0;
                _khrSurface.GetPhysicalDeviceSurfaceFormats(device, _surface.Value, ref formatCount, null);

                if (formatCount != 0)
                {
                    details.Formats = new SurfaceFormatKHR[formatCount];
                    fixed (SurfaceFormatKHR* formatsPtr = details.Formats)
                    {
                        uint localFormatCount = formatCount;
                        _khrSurface.GetPhysicalDeviceSurfaceFormats(device, _surface.Value, ref localFormatCount, formatsPtr);
                    }
                }
                else
                {
                    details.Formats = Array.Empty<SurfaceFormatKHR>();
                }

                uint presentModeCount = 0;
                _khrSurface.GetPhysicalDeviceSurfacePresentModes(device, _surface.Value, ref presentModeCount, null);

                if (presentModeCount != 0)
                {
                    details.PresentModes = new PresentModeKHR[presentModeCount];
                    fixed (PresentModeKHR* presentModesPtr = details.PresentModes)
                    {
                        uint localPresentModeCount = presentModeCount;
                        _khrSurface.GetPhysicalDeviceSurfacePresentModes(device, _surface.Value, ref localPresentModeCount, presentModesPtr);
                    }
                }
                else
                {
                    details.PresentModes = Array.Empty<PresentModeKHR>();
                }
            }

            return details;
        }

        /// <summary>
        /// 创建逻辑设备
        /// </summary>
        private unsafe void CreateLogicalDevice()
        {
            var indices = FindQueueFamilies(_physicalDevice);

            var uniqueQueueFamilies = new[] { indices.GraphicsFamily.Value, indices.PresentFamily.Value }
                .ToHashSet();

            var queueCreateInfos = new List<DeviceQueueCreateInfo>();
            float queuePriority = 1.0f;

            foreach (var queueFamily in uniqueQueueFamilies)
            {
                var queueCreateInfo = new DeviceQueueCreateInfo
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = queueFamily,
                    QueueCount = 1,
                    PQueuePriorities = &queuePriority
                };
                queueCreateInfos.Add(queueCreateInfo);
            }

            var deviceFeatures = new PhysicalDeviceFeatures();

            var queueCreateInfosArray = queueCreateInfos.ToArray();
            fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfosArray)
            {
                var createInfo = new DeviceCreateInfo
                {
                    SType = StructureType.DeviceCreateInfo,
                    QueueCreateInfoCount = (uint)queueCreateInfosArray.Length,
                    PQueueCreateInfos = pQueueCreateInfos,
                    PEnabledFeatures = &deviceFeatures,
                    EnabledExtensionCount = (uint)_deviceExtensions.Length,
                    PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToMemory(_deviceExtensions)
                };

                if (_configuration.ValidationLevel != ValidationLevel.None)
                {
                    createInfo.EnabledLayerCount = (uint)_validationLayers.Length;
                    createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToMemory(_validationLayers);
                }
                else
                {
                    createInfo.EnabledLayerCount = 0;
                }

                fixed (Device* devicePtr = &_device)
                {
                    if (_vk.CreateDevice(_physicalDevice, ref createInfo, null, devicePtr) != Result.Success)
                    {
                        throw new Exception("创建逻辑设备失败！");
                    }
                }

                _vk.GetDeviceQueue(_device, indices.GraphicsFamily.Value, 0, out _graphicsQueue);
                _vk.GetDeviceQueue(_device, indices.PresentFamily.Value, 0, out _presentQueue);

                // 获取设备级别的扩展
                _vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain);
                
                // 清理内存
                SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
                
                if (_configuration.ValidationLevel != ValidationLevel.None)
                {
                    SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
                }

                return;
            }
        }

        /// <summary>
        /// 创建交换链
        /// </summary>
        private unsafe void CreateSwapchain()
        {
            var swapchainSupport = QuerySwapchainSupport(_physicalDevice);
            var surfaceFormat = ChooseSwapSurfaceFormat(swapchainSupport.Formats);
            var presentMode = ChooseSwapPresentMode(swapchainSupport.PresentModes);
            var extent = ChooseSwapExtent(swapchainSupport.Capabilities);

            uint imageCount = swapchainSupport.Capabilities.MinImageCount + 1;
            if (swapchainSupport.Capabilities.MaxImageCount > 0 && imageCount > swapchainSupport.Capabilities.MaxImageCount)
            {
                imageCount = swapchainSupport.Capabilities.MaxImageCount;
            }

            var createInfo = new SwapchainCreateInfoKHR
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = _surface.Value,
                MinImageCount = imageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = extent
            };

            var indices = FindQueueFamilies(_physicalDevice);
            uint[] queueFamilyIndicesArray = { indices.GraphicsFamily.Value, indices.PresentFamily.Value };

            if (indices.GraphicsFamily != indices.PresentFamily)
            {
                createInfo.ImageSharingMode = SharingMode.Concurrent;
                createInfo.QueueFamilyIndexCount = 2;
                // 使用fixed语句正确传递数组指针
                fixed (uint* queueFamilyIndicesPtr = queueFamilyIndicesArray)
                {
                    createInfo.PQueueFamilyIndices = queueFamilyIndicesPtr;
                }
            }
            else
            {
                createInfo.ImageSharingMode = SharingMode.Exclusive;
                createInfo.QueueFamilyIndexCount = 0;
                createInfo.PQueueFamilyIndices = null;
            }

            createInfo.PreTransform = swapchainSupport.Capabilities.CurrentTransform;
            createInfo.CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
            createInfo.PresentMode = presentMode;
            createInfo.Clipped = true;
            createInfo.OldSwapchain = default;

            if (_khrSwapchain.CreateSwapchain(_device, ref createInfo, null, out _swapchain) != Result.Success)
            {
                throw new Exception("创建交换链失败！");
            }

            _khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imageCount, null);
            _swapchainImages = new Image[imageCount];
            fixed (Image* swapchainImagesPtr = _swapchainImages)
            {
                _khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imageCount, swapchainImagesPtr);
            }

            _swapchainImageFormat = surfaceFormat.Format;
            _swapchainExtent = extent;
        }

        /// <summary>
        /// 选择交换链表面格式
        /// </summary>
        private SurfaceFormatKHR ChooseSwapSurfaceFormat(IReadOnlyList<SurfaceFormatKHR> availableFormats)
        {
            if (availableFormats == null || availableFormats.Count == 0)
            {
                throw new ArgumentException("可用的表面格式列表不能为空");
            }

            foreach (var format in availableFormats)
            {
                if (format.Format == Format.B8G8R8A8Unorm && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                {
                    return format;
                }
            }

            return availableFormats[0];
        }

        /// <summary>
        /// 选择交换链呈现模式
        /// </summary>
        private PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
        {
            if (!_configuration.EnableVSync)
            {
                foreach (var presentMode in availablePresentModes)
                {
                    if (presentMode == PresentModeKHR.MailboxKhr)
                    {
                        return presentMode;
                    }
                }
            }

            return PresentModeKHR.FifoKhr;
        }

        /// <summary>
        /// 选择交换链范围
        /// </summary>
        private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.CurrentExtent.Width != uint.MaxValue)
            {
                return capabilities.CurrentExtent;
            }
            else
            {
                var actualExtent = new Extent2D
                {
                    Width = Math.Max(capabilities.MinImageExtent.Width, 
                        Math.Min(capabilities.MaxImageExtent.Width, 800)), // 默认宽度
                    Height = Math.Max(capabilities.MinImageExtent.Height, 
                        Math.Min(capabilities.MaxImageExtent.Height, 600))  // 默认高度
                };

                return actualExtent;
            }
        }

        /// <summary>
        /// 创建交换链图像视图
        /// </summary>
        private unsafe void CreateImageViews()
        {
            if (_swapchainImages == null)
                throw new InvalidOperationException("_swapchainImages 未初始化");
            _swapchainImageViews = new ImageView[_swapchainImages.Length];

            for (int i = 0; i < _swapchainImages.Length; i++)
            {
                var createInfo = new ImageViewCreateInfo
                {
                    SType = StructureType.ImageViewCreateInfo,
                    Image = _swapchainImages[i],
                    ViewType = ImageViewType.Type2D,
                    Format = _swapchainImageFormat,
                    Components =
                    {
                        R = ComponentSwizzle.Identity,
                        G = ComponentSwizzle.Identity,
                        B = ComponentSwizzle.Identity,
                        A = ComponentSwizzle.Identity
                    },
                    SubresourceRange =
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        BaseMipLevel = 0,
                        LevelCount = 1,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    }
                };

                if (_vk.CreateImageView(_device, ref createInfo, null, out _swapchainImageViews[i]) != Result.Success)
                {
                    throw new Exception("创建图像视图失败！");
                }
            }
        }

        /// <summary>
        /// 创建渲染通道
        /// </summary>
        private unsafe void CreateRenderPass()
        {
            var colorAttachment = new AttachmentDescription
            {
                Format = _swapchainImageFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.PresentSrcKhr
            };

            var colorAttachmentRef = new AttachmentReference
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal
            };

            var subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef
            };

            var dependency = new SubpassDependency
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                SrcAccessMask = 0,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit
            };

            var renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &colorAttachment,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency
            };

            fixed (RenderPass* renderPassPtr = &_renderPass)
            {
                if (_vk.CreateRenderPass(_device, ref renderPassInfo, null, renderPassPtr) != Result.Success)
                {
                    throw new Exception("创建渲染通道失败！");
                }
            }
        }

        /// <summary>
        /// 创建图形管线
        /// </summary>
        private void CreateGraphicsPipeline()
        {
            // 注意：在实际实现中，这里需要加载着色器代码
            // 由于着色器实现较为复杂，这里仅提供框架代码
            throw new NotImplementedException("需要实现着色器加载和图形管线创建");
        }

        /// <summary>
        /// 创建帧缓冲
        /// </summary>
        private unsafe void CreateFramebuffers()
        {
            if (_swapchainImageViews == null)
                throw new InvalidOperationException("_swapchainImageViews 未初始化");
            _framebuffers = new Framebuffer[_swapchainImageViews.Length];

            for (int i = 0; i < _swapchainImageViews.Length; i++)
            {
                var attachments = new ImageView[] { _swapchainImageViews[i] };
                
                // 使用fixed语句正确传递数组
                fixed (ImageView* attachmentsPtr = attachments)
                {
                    var framebufferInfo = new FramebufferCreateInfo
                    {
                        SType = StructureType.FramebufferCreateInfo,
                        RenderPass = _renderPass,
                        AttachmentCount = 1,
                        PAttachments = attachmentsPtr,
                        Width = _swapchainExtent.Width,
                        Height = _swapchainExtent.Height,
                        Layers = 1
                    };

                    if (_vk.CreateFramebuffer(_device, ref framebufferInfo, null, out _framebuffers[i]) != Result.Success)
                    {
                        throw new Exception("创建帧缓冲失败！");
                    }
                }
            }
        }

        /// <summary>
        /// 创建命令池
        /// </summary>
        private unsafe void CreateCommandPool()
        {
            var queueFamilyIndices = FindQueueFamilies(_physicalDevice);

            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = queueFamilyIndices.GraphicsFamily.Value
            };

            fixed (CommandPool* commandPoolPtr = &_commandPool)
            {
                if (_vk.CreateCommandPool(_device, ref poolInfo, null, commandPoolPtr) != Result.Success)
                {
                    throw new Exception("创建命令池失败！");
                }
            }
        }

        /// <summary>
        /// 创建命令缓冲区
        /// </summary>
        private unsafe void CreateCommandBuffers()
        {
            if (_framebuffers == null)
                throw new InvalidOperationException("_framebuffers 未初始化");
            _commandBuffers = new CommandBuffer[_framebuffers.Length];
            
            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)_commandBuffers.Length
            };

            fixed (CommandBuffer* commandBuffersPtr = _commandBuffers)
            {
                if (_vk.AllocateCommandBuffers(_device, ref allocInfo, commandBuffersPtr) != Result.Success)
                {
                    throw new Exception("分配命令缓冲区失败！");
                }
            }

            // 记录命令缓冲区
            for (int i = 0; i < _commandBuffers.Length; i++)
            {
                var beginInfo = new CommandBufferBeginInfo
                {
                    SType = StructureType.CommandBufferBeginInfo
                };

                if (_vk.BeginCommandBuffer(_commandBuffers[i], ref beginInfo) != Result.Success)
                {
                    throw new Exception("开始记录命令缓冲区失败！");
                }

                var renderPassInfo = new RenderPassBeginInfo
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _renderPass,
                    Framebuffer = _framebuffers[i],
                    RenderArea =
                    {
                        Offset = { X = 0, Y = 0 },
                        Extent = _swapchainExtent
                    }
                };

                var clearColor = new ClearValue
                {
                    Color = new ClearColorValue(0.0f, 0.0f, 0.0f, 1.0f) // 黑色背景
                };
                renderPassInfo.ClearValueCount = 1;
                renderPassInfo.PClearValues = &clearColor;

                _vk.CmdBeginRenderPass(_commandBuffers[i], &renderPassInfo, SubpassContents.Inline);
                if (_graphicsPipeline.HasValue)
                {
                    _vk.CmdBindPipeline(_commandBuffers[i], PipelineBindPoint.Graphics, _graphicsPipeline.Value);
                }
                
                // 执行排队的渲染命令
                lock (_renderLock)
                {
                    while (_renderCommands.Count > 0)
                    {
                        var command = _renderCommands.Dequeue();
                        command(_commandBuffers[i]);
                    }
                }
                
                _vk.CmdEndRenderPass(_commandBuffers[i]);

                if (_vk.EndCommandBuffer(_commandBuffers[i]) != Result.Success)
                {
                    throw new Exception("记录命令缓冲区失败！");
                }
            }
        }

        /// <summary>
        /// 创建同步对象
        /// </summary>
        private unsafe void CreateSyncObjects()
        {
            if (_swapchainImages == null)
                throw new InvalidOperationException("_swapchainImages 未初始化");
            _imageAvailableSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
            _renderFinishedSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
            _inFlightFences = new Fence[MAX_FRAMES_IN_FLIGHT];
            _imagesInFlight = new Fence[_swapchainImages.Length];

            var semaphoreInfo = new SemaphoreCreateInfo
            {
                SType = StructureType.SemaphoreCreateInfo
            };

            var fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit
            };

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                if (_vk.CreateSemaphore(_device, ref semaphoreInfo, null, out _imageAvailableSemaphores[i]) != Result.Success ||
                    _vk.CreateSemaphore(_device, ref semaphoreInfo, null, out _renderFinishedSemaphores[i]) != Result.Success ||
                    _vk.CreateFence(_device, ref fenceInfo, null, out _inFlightFences[i]) != Result.Success)
                {
                    throw new Exception("创建同步对象失败！");
                }
            }
            
            // 初始化_imagesInFlight数组
            for (int i = 0; i < _imagesInFlight.Length; i++)
            {
                _imagesInFlight[i] = new Fence();
            }
        }

        /// <summary>
        /// 绘制帧
        /// </summary>
        public unsafe void DrawFrame()
        {
            // 等待前一帧完成
            if (_inFlightFences == null)
                throw new InvalidOperationException("_inFlightFences 未初始化");
            _vk.WaitForFences(_device, 1, ref _inFlightFences[_currentFrame], true, ulong.MaxValue);

            // 获取交换链图像
            uint imageIndex = 0;
            if (_khrSwapchain == null)
                throw new InvalidOperationException("_khrSwapchain 未初始化");
            if (_imageAvailableSemaphores == null)
                throw new InvalidOperationException("_imageAvailableSemaphores 未初始化");
            var result = _khrSwapchain.AcquireNextImage(_device, _swapchain, ulong.MaxValue,
                _imageAvailableSemaphores[_currentFrame], default, ref imageIndex);            if (result == Result.ErrorOutOfDateKhr)
            {
                RecreateSwapchain();
                return;
            }
            else if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                throw new Exception("获取交换链图像失败！");
            }

            // 检查是否需要重新创建交换链
            if (_imagesInFlight[imageIndex].Handle != 0)
            {
                _vk.WaitForFences(_device, 1, ref _imagesInFlight[imageIndex], true, ulong.MaxValue);
            }
            _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];

            // 提交命令缓冲区
            if (_renderFinishedSemaphores == null)
                throw new InvalidOperationException("_renderFinishedSemaphores 未初始化");
            var waitSemaphores = stackalloc Semaphore[] { _imageAvailableSemaphores[_currentFrame] };
            var waitStages = stackalloc PipelineStageFlags[] { PipelineStageFlags.ColorAttachmentOutputBit };
            var signalSemaphores = stackalloc Semaphore[] { _renderFinishedSemaphores[_currentFrame] };

            if (_commandBuffers == null)
                throw new InvalidOperationException("_commandBuffers 未初始化");
            
            SubmitInfo submitInfo;
            fixed (CommandBuffer* commandBufferPtr = &_commandBuffers[imageIndex])
            {
                submitInfo = new SubmitInfo
                {
                    SType = StructureType.SubmitInfo,
                    WaitSemaphoreCount = 1,
                    PWaitSemaphores = waitSemaphores,
                    PWaitDstStageMask = waitStages,
                    CommandBufferCount = 1,
                    PCommandBuffers = commandBufferPtr,
                    SignalSemaphoreCount = 1,
                    PSignalSemaphores = signalSemaphores
                };
            }

            _vk.ResetFences(_device, 1, ref _inFlightFences[_currentFrame]);

            if (_vk.QueueSubmit(_graphicsQueue, 1, ref submitInfo, _inFlightFences[_currentFrame]) != Result.Success)
            {
                throw new Exception("提交队列失败！");
            }

            var swapchains = stackalloc SwapchainKHR[] { _swapchain };
            var images = stackalloc uint[] { imageIndex };

            var presentInfo = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = signalSemaphores,
                SwapchainCount = 1,
                PSwapchains = swapchains,
                PImageIndices = images
            };

            result = _khrSwapchain.QueuePresent(_presentQueue, ref presentInfo);
            
            if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr || _framebufferResized)
            {
                _framebufferResized = false;
                RecreateSwapchain();
            }
            else if (result != Result.Success)
            {
                throw new Exception("呈现图像失败！");
            }

            _currentFrame = (_currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
        }

        /// <summary>
        /// 重新创建交换链
        /// </summary>
        private unsafe void RecreateSwapchain()
        {
            _vk.DeviceWaitIdle(_device);

            CleanupSwapchain();

            CreateSwapchain();
            CreateImageViews();
            CreateFramebuffers();
        }

        /// <summary>
        /// 清理交换链资源
        /// </summary>
        private unsafe void CleanupSwapchain()
        {
            foreach (var framebuffer in _framebuffers)
            {
                _vk.DestroyFramebuffer(_device, framebuffer, null);
            }

            foreach (var imageView in _swapchainImageViews)
            {
                _vk.DestroyImageView(_device, imageView, null);
            }

            _khrSwapchain.DestroySwapchain(_device, _swapchain, null);
        }

        /// <summary>
        /// 创建音符专用渲染管线
        /// </summary>
        private unsafe void CreateNotePipeline()
        {
            // 加载着色器代码
            var vertShaderCode = LoadShaderCode("Shaders/note.vert.spv");
            var fragShaderCode = LoadShaderCode("Shaders/note.frag.spv");

            // 创建着色器模块
            CreateShaderModule(vertShaderCode, out _vertShaderModule);
            CreateShaderModule(fragShaderCode, out _fragShaderModule);

            // 着色器阶段
            var vertShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = _vertShaderModule,
                PName = (byte*)Marshal.StringToHGlobalAnsi("main")
            };

            var fragShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = _fragShaderModule,
                PName = (byte*)Marshal.StringToHGlobalAnsi("main")
            };

            var shaderStages = stackalloc PipelineShaderStageCreateInfo[] { vertShaderStageInfo, fragShaderStageInfo };

            // 顶点输入
            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 0,
                VertexAttributeDescriptionCount = 0
            };

            // 输入装配
            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList
            };

            // 视口和裁剪
            var viewport = new Viewport
            {
                X = 0.0f,
                Y = 0.0f,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            };

            var scissor = new Rect2D
            {
                Offset = { X = 0, Y = 0 },
                Extent = _swapchainExtent
            };

            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                PViewports = &viewport,
                ScissorCount = 1,
                PScissors = &scissor
            };

            // 光栅化
            var rasterizer = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.BackBit,
                FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = false
            };

            // 多重采样
            var multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = SampleCountFlags.Count1Bit
            };

            // 颜色混合
            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.Zero,
                AlphaBlendOp = BlendOp.Add
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment
            };

            // Push常量范围
            var pushConstantRange = new PushConstantRange
            {
                StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
                Offset = 0,
                Size = (uint)sizeof(PushConstants)
            };

            // 管线布局
            var pipelineLayoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 0,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &pushConstantRange
            };

            if (_vk.CreatePipelineLayout(_device, &pipelineLayoutInfo, null, out _notePipelineLayout) != Result.Success)
            {
                throw new Exception("创建音符管线布局失败！");
            }

            // 创建管线
            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = shaderStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PColorBlendState = &colorBlending,
                Layout = _notePipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0
            };

            if (_vk.CreateGraphicsPipelines(_device, default, 1, &pipelineInfo, null, out _notePipeline) != Result.Success)
            {
                throw new Exception("创建音符管线失败！");
            }

            // 清理
            Marshal.FreeHGlobal((IntPtr)vertShaderStageInfo.PName);
            Marshal.FreeHGlobal((IntPtr)fragShaderStageInfo.PName);
        }

        /// <summary>
        /// 加载着色器代码
        /// </summary>
        private byte[] LoadShaderCode(string path)
        {
            // 这里应该从嵌入资源或文件加载SPIR-V着色器代码
            // 暂时返回空数组，需要实际实现
            return Array.Empty<byte>();
        }

        /// <summary>
        /// 创建着色器模块
        /// </summary>
        private unsafe void CreateShaderModule(byte[] code, out ShaderModule shaderModule)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length
            };

            fixed (byte* codePtr = code)
            {
                createInfo.PCode = (uint*)codePtr;
                if (_vk.CreateShaderModule(_device, &createInfo, null, out shaderModule) != Result.Success)
                {
                    throw new Exception("创建着色器模块失败！");
                }
            }
        }

        /// <summary>
        /// Push常量结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct PushConstants
        {
            public Silk.NET.Maths.Matrix4X4<float> Projection;
            public Silk.NET.Maths.Vector4D<float> Color;
            public float Radius;
        }

        /// <summary>
        /// 检查验证层支持
        /// </summary>
        private unsafe bool CheckValidationLayerSupport()
        {
            uint layerCount = 0;
            _vk.EnumerateInstanceLayerProperties(ref layerCount, null);

            var availableLayers = new LayerProperties[layerCount];
            fixed (LayerProperties* availableLayersPtr = availableLayers)
            {
                _vk.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);
            }

            var availableLayerNames = availableLayers.Select(layer => 
                Marshal.PtrToStringAnsi((nint)layer.LayerName)).ToHashSet();

            return _validationLayers.All(availableLayerNames.Contains);
        }

        /// <summary>
        /// 获取必需的扩展
        /// </summary>
        private string[] GetRequiredExtensions()
        {
            var extensions = new List<string>
            {
                KhrSurface.ExtensionName
            };

            if (_configuration.ValidationLevel != ValidationLevel.None)
            {
                extensions.Add(ExtDebugUtils.ExtensionName);
            }

            // 根据平台添加特定扩展
            // Windows: KhrWin32Surface.ExtensionName
            // Linux: KhrXlibSurface.ExtensionName
            // macOS: "VK_MVK_macos_surface"

            return extensions.ToArray();
        }

        /// <summary>
        /// 获取物理设备属性
        /// </summary>
        private unsafe void GetPhysicalDeviceProperties(PhysicalDevice device, out PhysicalDeviceProperties properties)
        {
            PhysicalDeviceProperties props;
            _vk.GetPhysicalDeviceProperties(device, &props);
            properties = props;
        }

        /// <summary>
        /// 创建清除颜色值
        /// </summary>
        private ClearValue newClearColorValue(float r, float g, float b, float a)
        {
            return new ClearValue
            {
                Color = new ClearColorValue(r, g, b, a)
            };
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public unsafe void Dispose()
        {
            CleanupSwapchain();

            if (_renderFinishedSemaphores != null && _imageAvailableSemaphores != null && _inFlightFences != null)
            {
                for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
                {
                    _vk.DestroySemaphore(_device, _renderFinishedSemaphores[i], null);
                    _vk.DestroySemaphore(_device, _imageAvailableSemaphores[i], null);
                    _vk.DestroyFence(_device, _inFlightFences[i], null);
                }
            }

            _vk.DestroyCommandPool(_device, _commandPool, null);

            if (_graphicsPipeline.HasValue)
            {
                _vk.DestroyPipeline(_device, _graphicsPipeline.Value, null);
            }
            if (_pipelineLayout.HasValue)
            {
                _vk.DestroyPipelineLayout(_device, _pipelineLayout.Value, null);
            }
            _vk.DestroyRenderPass(_device, _renderPass, null);

            _vk.DestroyDevice(_device, null);
            
            if (_debugUtils != null && _configuration.ValidationLevel != ValidationLevel.None)
            {
                _debugUtils.DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);
            }

            _vk.DestroyInstance(_instance, null);
        }

        private const int MAX_FRAMES_IN_FLIGHT = 2;
    }

    /// <summary>
    /// 队列族索引
    /// </summary>
    public struct QueueFamilyIndices
    {
        public uint? GraphicsFamily { get; set; }
        public uint? PresentFamily { get; set; }

        public bool IsComplete()
        {
            return GraphicsFamily.HasValue && PresentFamily.HasValue;
        }
    }

    /// <summary>
    /// 交换链支持详情
    /// </summary>
    public struct SwapchainSupportDetails
    {
        public SurfaceCapabilitiesKHR Capabilities { get; set; }
        public SurfaceFormatKHR[] Formats { get; set; }
        public PresentModeKHR[] PresentModes { get; set; }
    }

    /// <summary>
    /// 不安全内存管理助手
    /// </summary>
    // Removed UnsafeMarshal class as it's no longer needed with proper stack allocation and fixed statements.
}