using System;
using System.Collections.Generic;
using Avalonia.Threading;
using DominoNext.Services.Interfaces;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 渲染同步服务 - 确保所有Canvas层同步渲染，提高拖拽流畅度
    /// </summary>
    public class RenderSyncService : IRenderSyncService
    {
        private static readonly Lazy<RenderSyncService> _instance = new(() => new RenderSyncService());
        public static RenderSyncService Instance => _instance.Value;

        private readonly List<WeakReference<IRenderSyncTarget>> _targets = new();
        private readonly object _lock = new();
        private bool _isDragging = false;
        private bool _hasPendingSync = false;

        private RenderSyncService() { }

        /// <summary>
        /// 注册需要同步渲染的目标
        /// </summary>
        public void RegisterTarget(IRenderSyncTarget target)
        {
            lock (_lock)
            {
                // 清理已释放的弱引用
                _targets.RemoveAll(wr => !wr.TryGetTarget(out _));
                
                // 添加新目标
                _targets.Add(new WeakReference<IRenderSyncTarget>(target));
            }
        }

        /// <summary>
        /// 注销渲染目标
        /// </summary>
        public void UnregisterTarget(IRenderSyncTarget target)
        {
            lock (_lock)
            {
                _targets.RemoveAll(wr => 
                {
                    if (wr.TryGetTarget(out var t))
                        return ReferenceEquals(t, target);
                    return true; // 同时清理已释放的引用
                });
            }
        }

        /// <summary>
        /// 设置拖拽状态
        /// </summary>
        public void SetDragState(bool isDragging)
        {
            _isDragging = isDragging;
        }

        /// <summary>
        /// 同步刷新所有注册的渲染目标
        /// </summary>
        public void SyncRefresh()
        {
            if (_hasPendingSync) return;
            
            _hasPendingSync = true;
            
            // 拖拽时使用更高优先级的调度
            var priority = _isDragging ? DispatcherPriority.Render : DispatcherPriority.Normal;
            
            Dispatcher.UIThread.Post(() =>
            {
                _hasPendingSync = false;
                RefreshAllTargets();
            }, priority);
        }

        /// <summary>
        /// 立即同步刷新（用于拖拽等实时操作）
        /// </summary>
        public void ImmediateSyncRefresh()
        {
            Dispatcher.UIThread.Post(RefreshAllTargets, DispatcherPriority.Send);
        }

        private void RefreshAllTargets()
        {
            lock (_lock)
            {
                var validTargets = new List<IRenderSyncTarget>();
                
                foreach (var weakRef in _targets)
                {
                    if (weakRef.TryGetTarget(out var target))
                    {
                        validTargets.Add(target);
                    }
                }
                
                // 清理无效引用
                _targets.RemoveAll(wr => !wr.TryGetTarget(out _));
                
                // 同步刷新所有有效目标
                foreach (var target in validTargets)
                {
                    try
                    {
                        target.RefreshRender();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"渲染同步失败: {ex.Message}");
                    }
                }
            }
        }
    }
}