using DominoNext.Services.Interfaces;

namespace DominoNext.Services.Interfaces
{
    /// <summary>
    /// 渲染同步服务接口
    /// </summary>
    public interface IRenderSyncService
    {
        /// <summary>
        /// 注册需要同步渲染的目标
        /// </summary>
        void RegisterTarget(IRenderSyncTarget target);

        /// <summary>
        /// 注销渲染目标
        /// </summary>
        void UnregisterTarget(IRenderSyncTarget target);

        /// <summary>
        /// 设置拖拽状态
        /// </summary>
        void SetDragState(bool isDragging);

        /// <summary>
        /// 同步刷新所有注册的渲染目标
        /// </summary>
        void SyncRefresh();

        /// <summary>
        /// 立即同步刷新（用于拖拽等实时操作）
        /// </summary>
        void ImmediateSyncRefresh();
    }

    /// <summary>
    /// 渲染同步目标接口
    /// </summary>
    public interface IRenderSyncTarget
    {
        /// <summary>
        /// 刷新渲染
        /// </summary>
        void RefreshRender();
    }
}