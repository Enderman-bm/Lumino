using System;
using Avalonia.Platform;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// Vulkan渲染服务接口
    /// </summary>
    public interface IVulkanRenderService
    {
        /// <summary>
        /// 是否支持Vulkan
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// 是否启用Vulkan渲染
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 初始化Vulkan渲染器
        /// </summary>
        void Initialize();

        /// <summary>
        /// 创建Vulkan渲染表面
        /// </summary>
        object CreateRenderSurface(IPlatformHandle handle);

        /// <summary>
        /// 开始渲染帧
        /// </summary>
        void BeginFrame();

        /// <summary>
        /// 结束渲染帧
        /// </summary>
        void EndFrame();

        /// <summary>
        /// 清理资源
        /// </summary>
        void Cleanup();

        /// <summary>
        /// 获取渲染统计信息
        /// </summary>
        VulkanRenderStats GetStats();

        /// <summary>
        /// 获取渲染统计信息（兼容接口）
        /// </summary>
        RenderStats GetRenderStats();

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 获取渲染上下文
        /// </summary>
        object GetRenderContext();
    }

    /// <summary>
    /// Vulkan渲染统计信息
    /// </summary>
    public class VulkanRenderStats
    {
        public int FrameCount { get; set; }
        public double FrameTime { get; set; }
        public int DrawCalls { get; set; }
        public int VerticesRendered { get; set; }
        public long MemoryUsed { get; set; }
    }

    /// <summary>
    /// 通用渲染统计信息
    /// </summary>
    public class RenderStats
    {
        public int TotalFrames { get; set; }
        public int ErrorCount { get; set; }
        public double AverageFrameTime { get; set; }
        public double LastFrameTime { get; set; }
    }
}