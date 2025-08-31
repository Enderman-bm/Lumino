using System;

namespace DominoNext.Services.Interfaces
{
    /// <summary>
    /// 渲染刷新服务接口
    /// 提供统一的渲染刷新机制，确保所有操作都能正确清除渲染缓存和影子
    /// </summary>
    public interface IRenderRefreshService
    {
        /// <summary>
        /// 强制完全刷新渲染
        /// 清除所有缓存，重建可见元素，并立即重绘
        /// </summary>
        void ForceCompleteRefresh();

        /// <summary>
        /// 实时刷新渲染（用于拖拽等连续操作）
        /// 清除关键缓存，快速重绘
        /// </summary>
        void RealtimeRefresh();

        /// <summary>
        /// 延迟刷新渲染（用于性能优化）
        /// 使用定时器合并多个刷新请求
        /// </summary>
        void ThrottledRefresh();

        /// <summary>
        /// 刷新请求事件
        /// </summary>
        event Action? OnRefreshRequested;

        /// <summary>
        /// 强制刷新请求事件
        /// </summary>
        event Action? OnForceRefreshRequested;
    }
}