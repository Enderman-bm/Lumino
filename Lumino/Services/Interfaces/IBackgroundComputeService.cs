using System;
using System.Threading.Tasks;
using Avalonia;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// 后台计算服务接口
    /// </summary>
    public interface IBackgroundComputeService : IDisposable
    {
        /// <summary>
        /// 异步计算可见音符
        /// </summary>
        Task<VisibleNotesResult> ComputeVisibleNotesAsync(ComputeVisibleNotesRequest request);

        /// <summary>
        /// 异步预计算几何信息
        /// </summary>
        Task<GeometryPrecomputeResult> PrecomputeGeometryAsync(GeometryPrecomputeRequest request);

        /// <summary>
        /// 异步计算渲染批次
        /// </summary>
        Task<RenderBatchResult> ComputeRenderBatchesAsync(RenderBatchRequest request);

        /// <summary>
        /// 获取计算统计
        /// </summary>
        BackgroundComputeStats GetStats();

        /// <summary>
        /// 取消所有正在进行的计算
        /// </summary>
        void CancelAll();
    }

    /// <summary>
    /// 计算可见音符请求
    /// </summary>
    public struct ComputeVisibleNotesRequest
    {
        public object AllNotes; // 可能是Dictionary或List
        public Rect Viewport;
        public double ZoomLevel;
        public int MaxNotes;
    }

    /// <summary>
    /// 可见音符结果
    /// </summary>
    public struct VisibleNotesResult
    {
        public object VisibleNotes; // 可能是Dictionary或List
        public int TotalNotes;
        public int VisibleCount;
        public TimeSpan ComputeTime;
    }

    /// <summary>
    /// 几何预计算请求
    /// </summary>
    public struct GeometryPrecomputeRequest
    {
        public object Notes;
        public double BaseQuarterNoteWidth;
        public double KeyHeight;
    }

    /// <summary>
    /// 几何预计算结果
    /// </summary>
    public struct GeometryPrecomputeResult
    {
        public bool Success;
        public TimeSpan ComputeTime;
    }

    /// <summary>
    /// 渲染批次请求
    /// </summary>
    public struct RenderBatchRequest
    {
        public object VisibleNotes;
        public double ZoomLevel;
    }

    /// <summary>
    /// 渲染批次结果
    /// </summary>
    public struct RenderBatchResult
    {
        public object Batches; // 批次数据
        public int BatchCount;
        public TimeSpan ComputeTime;
    }

    /// <summary>
    /// 后台计算统计
    /// </summary>
    public class BackgroundComputeStats
    {
        public int ActiveTasks;
        public int CompletedTasks;
        public TimeSpan TotalComputeTime;
        public double AverageComputeTimeMs;
    }
}