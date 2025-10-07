using System;
using System.Threading.Tasks;

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
        /// 检查是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 初始化渲染服务
        /// </summary>
        /// <param name="windowHandle">窗口句柄</param>
        /// <returns>是否初始化成功</returns>
        bool Initialize(nint windowHandle);

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
        /// 将渲染命令加入队列
        /// </summary>
        /// <param name="command">要执行的渲染命令</param>
        void EnqueueRenderCommand(Action command);

        /// <summary>
        /// 异步执行渲染命令
        /// </summary>
        /// <param name="command">要执行的渲染命令</param>
        /// <returns>任务</returns>
        Task EnqueueRenderCommandAsync(Action command);

        /// <summary>
        /// 获取渲染统计信息
        /// </summary>
        /// <returns>渲染统计信息</returns>
        VulkanRenderStats GetStats();

        /// <summary>
        /// 获取渲染统计信息（兼容接口）
        /// </summary>
        /// <returns>渲染统计信息</returns>
        RenderStats GetRenderStats();

        /// <summary>
        /// 获取渲染上下文
        /// </summary>
        /// <returns>渲染上下文</returns>
        object GetRenderContext();
    }

    /// <summary>
    /// Vulkan渲染统计信息
    /// </summary>
    public class VulkanRenderStats
    {
        public long TotalFrames { get; set; }
        public long TotalDrawCalls { get; set; }
        public long TotalVertices { get; set; }
        public double GpuUtilization { get; set; }
        public long MemoryUsage { get; set; }
        public int ActiveTextures { get; set; }
        public int ActiveBuffers { get; set; }
        public double LastFrameTime { get; set; }
        public double AverageFrameTime { get; set; }
        public double FrameRate { get; set; }
    }

    /// <summary>
    /// 渲染统计信息
    /// </summary>
    public class RenderStats
    {
        public int TotalFrames { get; set; }
        public int ErrorCount { get; set; }
        public double AverageFrameTime { get; set; }
        public double LastFrameTime { get; set; }
    }
}