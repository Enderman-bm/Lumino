using System;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Maths;
using EnderDebugger;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// Vulkan离屏渲染器 - 将Vulkan渲染到离屏帧缓冲区，然后复制到CPU可访问的缓冲区
    /// 这允许Vulkan渲染结果与Avalonia集成
    /// </summary>
    public class VulkanOffscreenRenderer : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;
        private readonly PhysicalDevice _physicalDevice;
        private readonly Queue _graphicsQueue;
        private readonly CommandPool _commandPool;
        private readonly uint _graphicsQueueFamilyIndex;

        // 离屏渲染资源
        private Image _offscreenImage;
        private DeviceMemory _offscreenImageMemory;
        private ImageView _offscreenImageView;
        private Framebuffer _offscreenFramebuffer;
        private RenderPass _offscreenRenderPass;

        // CPU可访问的暂存缓冲区
        private Silk.NET.Vulkan.Buffer _stagingBuffer;
        private DeviceMemory _stagingBufferMemory;
        private IntPtr _mappedMemory;

        // 同步对象
        private Fence _renderFence;
        private CommandBuffer _commandBuffer;

        // 渲染尺寸
        private uint _width;
        private uint _height;
        private bool _initialized = false;
        private bool _disposed = false;

        // 像素格式
        private const Format OFFSCREEN_FORMAT = Format.B8G8R8A8Unorm;
        private const uint BYTES_PER_PIXEL = 4;

        public uint Width => _width;
        public uint Height => _height;
        public bool IsInitialized => _initialized;
        public RenderPass OffscreenRenderPass => _offscreenRenderPass;
        public Framebuffer OffscreenFramebuffer => _offscreenFramebuffer;

        public VulkanOffscreenRenderer(VulkanManager vulkanManager)
        {
            if (vulkanManager == null)
                throw new ArgumentNullException(nameof(vulkanManager));

            _vk = vulkanManager.GetVk();
            _device = vulkanManager.GetDevice();
            _physicalDevice = vulkanManager.GetPhysicalDevice();
            _graphicsQueue = vulkanManager.GetGraphicsQueue();
            _commandPool = vulkanManager.GetCommandPool();
            _graphicsQueueFamilyIndex = vulkanManager.GetGraphicsQueueFamilyIndex();
        }

        /// <summary>
        /// 初始化离屏渲染资源
        /// </summary>
        public unsafe bool Initialize(uint width, uint height)
        {
            if (_initialized)
            {
                // 如果尺寸相同，不需要重新初始化
                if (_width == width && _height == height)
                    return true;

                // 尺寸改变，需要重新创建资源
                Cleanup();
            }

            if (width == 0 || height == 0)
            {
                EnderLogger.Instance.Warn("VulkanOffscreenRenderer", "无效的尺寸，跳过初始化");
                return false;
            }

            _width = width;
            _height = height;

            try
            {
                EnderLogger.Instance.Info("VulkanOffscreenRenderer", $"初始化离屏渲染器: {width}x{height}");

                CreateOffscreenRenderPass();
                CreateOffscreenImage();
                CreateOffscreenFramebuffer();
                CreateStagingBuffer();
                CreateCommandBuffer();
                CreateSyncObjects();

                _initialized = true;
                EnderLogger.Instance.Info("VulkanOffscreenRenderer", "离屏渲染器初始化成功");
                return true;
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanOffscreenRenderer", "初始化失败");
                Cleanup();
                return false;
            }
        }

        /// <summary>
        /// 创建离屏渲染通道
        /// </summary>
        private unsafe void CreateOffscreenRenderPass()
        {
            var colorAttachment = new AttachmentDescription
            {
                Format = OFFSCREEN_FORMAT,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.TransferSrcOptimal // 用于复制到CPU
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

            // 子通道依赖
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

            fixed (RenderPass* renderPassPtr = &_offscreenRenderPass)
            {
                if (_vk.CreateRenderPass(_device, ref renderPassInfo, null, renderPassPtr) != Result.Success)
                {
                    throw new Exception("创建离屏渲染通道失败");
                }
            }

            EnderLogger.Instance.Debug("VulkanOffscreenRenderer", "离屏渲染通道创建成功");
        }

        /// <summary>
        /// 创建离屏图像
        /// </summary>
        private unsafe void CreateOffscreenImage()
        {
            // 创建图像
            var imageInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Format = OFFSCREEN_FORMAT,
                Extent = new Extent3D(_width, _height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = SampleCountFlags.Count1Bit,
                Tiling = ImageTiling.Optimal,
                Usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
                SharingMode = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined
            };

            fixed (Image* imagePtr = &_offscreenImage)
            {
                if (_vk.CreateImage(_device, ref imageInfo, null, imagePtr) != Result.Success)
                {
                    throw new Exception("创建离屏图像失败");
                }
            }

            // 获取内存需求
            _vk.GetImageMemoryRequirements(_device, _offscreenImage, out var memRequirements);

            // 分配内存
            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
            };

            fixed (DeviceMemory* memoryPtr = &_offscreenImageMemory)
            {
                if (_vk.AllocateMemory(_device, ref allocInfo, null, memoryPtr) != Result.Success)
                {
                    throw new Exception("分配离屏图像内存失败");
                }
            }

            _vk.BindImageMemory(_device, _offscreenImage, _offscreenImageMemory, 0);

            // 创建图像视图
            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _offscreenImage,
                ViewType = ImageViewType.Type2D,
                Format = OFFSCREEN_FORMAT,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            fixed (ImageView* viewPtr = &_offscreenImageView)
            {
                if (_vk.CreateImageView(_device, ref viewInfo, null, viewPtr) != Result.Success)
                {
                    throw new Exception("创建离屏图像视图失败");
                }
            }

            EnderLogger.Instance.Debug("VulkanOffscreenRenderer", "离屏图像创建成功");
        }

        /// <summary>
        /// 创建离屏帧缓冲区
        /// </summary>
        private unsafe void CreateOffscreenFramebuffer()
        {
            var attachment = _offscreenImageView;

            var framebufferInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _offscreenRenderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = _width,
                Height = _height,
                Layers = 1
            };

            fixed (Framebuffer* framebufferPtr = &_offscreenFramebuffer)
            {
                if (_vk.CreateFramebuffer(_device, ref framebufferInfo, null, framebufferPtr) != Result.Success)
                {
                    throw new Exception("创建离屏帧缓冲区失败");
                }
            }

            EnderLogger.Instance.Debug("VulkanOffscreenRenderer", "离屏帧缓冲区创建成功");
        }

        /// <summary>
        /// 创建暂存缓冲区用于CPU读取
        /// </summary>
        private unsafe void CreateStagingBuffer()
        {
            var bufferSize = _width * _height * BYTES_PER_PIXEL;

            var bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = bufferSize,
                Usage = BufferUsageFlags.TransferDstBit,
                SharingMode = SharingMode.Exclusive
            };

            fixed (Silk.NET.Vulkan.Buffer* bufferPtr = &_stagingBuffer)
            {
                if (_vk.CreateBuffer(_device, ref bufferInfo, null, bufferPtr) != Result.Success)
                {
                    throw new Exception("创建暂存缓冲区失败");
                }
            }

            // 获取内存需求
            _vk.GetBufferMemoryRequirements(_device, _stagingBuffer, out var memRequirements);

            // 分配主机可见内存
            var allocInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits,
                    MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
            };

            fixed (DeviceMemory* memoryPtr = &_stagingBufferMemory)
            {
                if (_vk.AllocateMemory(_device, ref allocInfo, null, memoryPtr) != Result.Success)
                {
                    throw new Exception("分配暂存缓冲区内存失败");
                }
            }

            _vk.BindBufferMemory(_device, _stagingBuffer, _stagingBufferMemory, 0);

            // 映射内存
            void* mappedPtr;
            if (_vk.MapMemory(_device, _stagingBufferMemory, 0, bufferSize, 0, &mappedPtr) != Result.Success)
            {
                throw new Exception("映射暂存缓冲区内存失败");
            }
            _mappedMemory = (IntPtr)mappedPtr;

            EnderLogger.Instance.Debug("VulkanOffscreenRenderer", $"暂存缓冲区创建成功: {bufferSize} 字节");
        }

        /// <summary>
        /// 创建命令缓冲区
        /// </summary>
        private unsafe void CreateCommandBuffer()
        {
            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            fixed (CommandBuffer* cmdBufferPtr = &_commandBuffer)
            {
                if (_vk.AllocateCommandBuffers(_device, ref allocInfo, cmdBufferPtr) != Result.Success)
                {
                    throw new Exception("分配命令缓冲区失败");
                }
            }

            EnderLogger.Instance.Debug("VulkanOffscreenRenderer", "命令缓冲区创建成功");
        }

        /// <summary>
        /// 创建同步对象
        /// </summary>
        private unsafe void CreateSyncObjects()
        {
            var fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit
            };

            fixed (Fence* fencePtr = &_renderFence)
            {
                if (_vk.CreateFence(_device, ref fenceInfo, null, fencePtr) != Result.Success)
                {
                    throw new Exception("创建同步栅栏失败");
                }
            }

            EnderLogger.Instance.Debug("VulkanOffscreenRenderer", "同步对象创建成功");
        }

        /// <summary>
        /// 开始离屏渲染
        /// </summary>
        public unsafe void BeginRender(float clearR = 0, float clearG = 0, float clearB = 0, float clearA = 0)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("离屏渲染器未初始化");
            }

            // 等待上一帧完成
            _vk.WaitForFences(_device, 1, ref _renderFence, true, ulong.MaxValue);
            _vk.ResetFences(_device, 1, ref _renderFence);

            // 重置命令缓冲区
            _vk.ResetCommandBuffer(_commandBuffer, 0);

            // 开始记录命令
            var beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };

            if (_vk.BeginCommandBuffer(_commandBuffer, ref beginInfo) != Result.Success)
            {
                throw new Exception("开始记录命令缓冲区失败");
            }

            // 开始渲染通道
            var clearValue = new ClearValue
            {
                Color = new ClearColorValue(clearR, clearG, clearB, clearA)
            };

            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _offscreenRenderPass,
                Framebuffer = _offscreenFramebuffer,
                RenderArea = new Rect2D
                {
                    Offset = new Offset2D(0, 0),
                    Extent = new Extent2D(_width, _height)
                },
                ClearValueCount = 1,
                PClearValues = &clearValue
            };

            _vk.CmdBeginRenderPass(_commandBuffer, &renderPassInfo, SubpassContents.Inline);

            // 设置视口和裁剪
            var viewport = new Viewport
            {
                X = 0,
                Y = 0,
                Width = _width,
                Height = _height,
                MinDepth = 0,
                MaxDepth = 1
            };
            _vk.CmdSetViewport(_commandBuffer, 0, 1, &viewport);

            var scissor = new Rect2D
            {
                Offset = new Offset2D(0, 0),
                Extent = new Extent2D(_width, _height)
            };
            _vk.CmdSetScissor(_commandBuffer, 0, 1, &scissor);
        }

        /// <summary>
        /// 获取当前命令缓冲区用于渲染
        /// </summary>
        public CommandBuffer GetCommandBuffer()
        {
            return _commandBuffer;
        }

        /// <summary>
        /// 结束离屏渲染并将结果复制到暂存缓冲区
        /// </summary>
        public unsafe void EndRender()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("离屏渲染器未初始化");
            }

            // 结束渲染通道
            _vk.CmdEndRenderPass(_commandBuffer);

            // 图像布局已经是 TransferSrcOptimal（由渲染通道设置）

            // 复制图像到暂存缓冲区
            var region = new BufferImageCopy
            {
                BufferOffset = 0,
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = new Extent3D(_width, _height, 1)
            };

            _vk.CmdCopyImageToBuffer(_commandBuffer, _offscreenImage, ImageLayout.TransferSrcOptimal,
                _stagingBuffer, 1, &region);

            // 结束命令缓冲区
            if (_vk.EndCommandBuffer(_commandBuffer) != Result.Success)
            {
                throw new Exception("结束命令缓冲区失败");
            }

            // 提交命令 - 使用局部变量并获取其指针
            var cmdBuffer = _commandBuffer;
            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &cmdBuffer
            };

            if (_vk.QueueSubmit(_graphicsQueue, 1, ref submitInfo, _renderFence) != Result.Success)
            {
                throw new Exception("提交命令队列失败");
            }

            // 等待渲染完成
            _vk.WaitForFences(_device, 1, ref _renderFence, true, ulong.MaxValue);
        }

        /// <summary>
        /// 将渲染结果复制到目标字节数组
        /// </summary>
        public unsafe void CopyToBuffer(byte[] destination)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("离屏渲染器未初始化");
            }

            var expectedSize = (int)(_width * _height * BYTES_PER_PIXEL);
            if (destination.Length < expectedSize)
            {
                throw new ArgumentException($"目标缓冲区太小: 需要 {expectedSize}，实际 {destination.Length}");
            }

            Marshal.Copy(_mappedMemory, destination, 0, expectedSize);
        }

        /// <summary>
        /// 将渲染结果复制到目标指针
        /// </summary>
        public unsafe void CopyToPointer(IntPtr destination, int destinationSize)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("离屏渲染器未初始化");
            }

            var expectedSize = (int)(_width * _height * BYTES_PER_PIXEL);
            if (destinationSize < expectedSize)
            {
                throw new ArgumentException($"目标缓冲区太小: 需要 {expectedSize}，实际 {destinationSize}");
            }

            // 直接内存复制
            System.Buffer.MemoryCopy(_mappedMemory.ToPointer(), destination.ToPointer(), destinationSize, expectedSize);
        }

        /// <summary>
        /// 获取渲染结果的原始指针
        /// </summary>
        public IntPtr GetMappedMemory()
        {
            return _mappedMemory;
        }

        /// <summary>
        /// 获取渲染结果的字节大小
        /// </summary>
        public int GetBufferSize()
        {
            return (int)(_width * _height * BYTES_PER_PIXEL);
        }

        /// <summary>
        /// 查找合适的内存类型
        /// </summary>
        private unsafe uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
        {
            _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);

            for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                if ((typeFilter & (1 << (int)i)) != 0 &&
                    (memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
                {
                    return i;
                }
            }

            throw new Exception($"找不到合适的内存类型: filter={typeFilter}, properties={properties}");
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private unsafe void Cleanup()
        {
            if (_device.Handle == 0)
                return;

            _vk.DeviceWaitIdle(_device);

            if (_renderFence.Handle != 0)
            {
                _vk.DestroyFence(_device, _renderFence, null);
                _renderFence = default;
            }

            if (_commandBuffer.Handle != 0)
            {
                _vk.FreeCommandBuffers(_device, _commandPool, 1, ref _commandBuffer);
                _commandBuffer = default;
            }

            if (_mappedMemory != IntPtr.Zero)
            {
                _vk.UnmapMemory(_device, _stagingBufferMemory);
                _mappedMemory = IntPtr.Zero;
            }

            if (_stagingBuffer.Handle != 0)
            {
                _vk.DestroyBuffer(_device, _stagingBuffer, null);
                _stagingBuffer = default;
            }

            if (_stagingBufferMemory.Handle != 0)
            {
                _vk.FreeMemory(_device, _stagingBufferMemory, null);
                _stagingBufferMemory = default;
            }

            if (_offscreenFramebuffer.Handle != 0)
            {
                _vk.DestroyFramebuffer(_device, _offscreenFramebuffer, null);
                _offscreenFramebuffer = default;
            }

            if (_offscreenImageView.Handle != 0)
            {
                _vk.DestroyImageView(_device, _offscreenImageView, null);
                _offscreenImageView = default;
            }

            if (_offscreenImage.Handle != 0)
            {
                _vk.DestroyImage(_device, _offscreenImage, null);
                _offscreenImage = default;
            }

            if (_offscreenImageMemory.Handle != 0)
            {
                _vk.FreeMemory(_device, _offscreenImageMemory, null);
                _offscreenImageMemory = default;
            }

            if (_offscreenRenderPass.Handle != 0)
            {
                _vk.DestroyRenderPass(_device, _offscreenRenderPass, null);
                _offscreenRenderPass = default;
            }

            _initialized = false;
            _width = 0;
            _height = 0;

            EnderLogger.Instance.Debug("VulkanOffscreenRenderer", "资源清理完成");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Cleanup();
            GC.SuppressFinalize(this);
        }

        ~VulkanOffscreenRenderer()
        {
            Dispose();
        }
    }
}
