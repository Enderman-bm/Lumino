using System;
using System.Collections.Generic;
using Avalonia.Threading;
using DominoNext.Services.Interfaces;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 渲染同步服务 - 确保各Canvas协同渲染，特别是拖拽和滚动时
    /// 实时版本：移除所有延迟，确保立即同步渲染
    /// </summary>
    public class RenderSyncService : IRenderSyncService
    {
        private static readonly Lazy<RenderSyncService> _instance = new(() => new RenderSyncService());
        public static RenderSyncService Instance => _instance.Value;

        private readonly List<WeakReference<IRenderSyncTarget>> _targets = new();
        private readonly object _lock = new();
        private bool _isDragging = false;
        private readonly HashSet<IRenderSyncTarget> _currentRefreshTargets = new();

        private RenderSyncService() 
        {
            System.Diagnostics.Debug.WriteLine("RenderSyncService 初始化 - 实时同步版本");
        }

        /// <summary>
        /// 注册需要同步渲染的目标
        /// </summary>
        public void RegisterTarget(IRenderSyncTarget target)
        {
            lock (_lock)
            {
                // 清理已释放的弱引用
                _targets.RemoveAll(wr => !wr.TryGetTarget(out _));
                
                // 添加目标
                _targets.Add(new WeakReference<IRenderSyncTarget>(target));
                System.Diagnostics.Debug.WriteLine($"注册渲染目标，当前总数: {_targets.Count}");
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
                    return true; // 同时清理已释放的弱引用
                });
                
                // 从当前刷新目标中移除
                _currentRefreshTargets.Remove(target);
                System.Diagnostics.Debug.WriteLine($"注销渲染目标，当前总数: {_targets.Count}");
            }
        }

        /// <summary>
        /// 设置拖拽状态
        /// </summary>
        public void SetDragState(bool isDragging)
        {
            var oldDragging = _isDragging;
            _isDragging = isDragging;
            
            if (oldDragging != isDragging)
            {
                System.Diagnostics.Debug.WriteLine($"拖拽状态变化: {isDragging}");
                
                // 拖拽状态变化时立即同步刷新
                if (!isDragging && oldDragging)
                {
                    ImmediateSyncRefresh();
                }
            }
        }

        /// <summary>
        /// 同步刷新所有注册的渲染目标 - 立即执行，无延迟
        /// </summary>
        public void SyncRefresh()
        {
            // 立即执行，不做任何延迟判断
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    RefreshAllTargets();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"同步渲染失败: {ex.Message}");
                }
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// 立即同步刷新，用于拖拽等实时操作 - 最高优先级，无延迟
        /// </summary>
        public void ImmediateSyncRefresh()
        {
            // 立即执行，最高优先级
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    RefreshAllTargets();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"立即渲染失败: {ex.Message}");
                }
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// 选择性刷新特定目标 - 立即执行
        /// </summary>
        public void SelectiveRefresh(IRenderSyncTarget specificTarget)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // 只刷新特定目标
                    lock (_lock)
                    {
                        if (!_currentRefreshTargets.Contains(specificTarget))
                        {
                            _currentRefreshTargets.Add(specificTarget);
                            try
                            {
                                specificTarget.RefreshRender();
                            }
                            finally
                            {
                                _currentRefreshTargets.Remove(specificTarget);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"选择性渲染失败: {ex.Message}");
                }
            }, DispatcherPriority.Render);
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
                
                // 同步刷新所有有效目标，避免重复刷新
                int refreshedCount = 0;
                foreach (var target in validTargets)
                {
                    if (!_currentRefreshTargets.Contains(target))
                    {
                        _currentRefreshTargets.Add(target);
                        try
                        {
                            target.RefreshRender();
                            refreshedCount++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"目标渲染失败: {ex.Message}");
                        }
                        finally
                        {
                            _currentRefreshTargets.Remove(target);
                        }
                    }
                }
                
                if (refreshedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"实时刷新了 {refreshedCount} 个渲染目标");
                }
            }
        }
    }
}