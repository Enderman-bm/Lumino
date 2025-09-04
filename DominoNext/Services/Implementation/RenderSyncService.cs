using System;
using System.Collections.Generic;
using Avalonia.Threading;
using DominoNext.Services.Interfaces;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 渲染同步服务 - 确保多个Canvas协同渲染，避免拖拽撕裂
    /// 修复：防止不必要的交叉刷新和颜色状态不一致问题
    /// </summary>
    public class RenderSyncService : IRenderSyncService
    {
        private static readonly Lazy<RenderSyncService> _instance = new(() => new RenderSyncService());
        public static RenderSyncService Instance => _instance.Value;

        private readonly List<WeakReference<IRenderSyncTarget>> _targets = new();
        private readonly object _lock = new();
        private bool _isDragging = false;
        private bool _hasPendingSync = false;
        private readonly HashSet<IRenderSyncTarget> _currentRefreshTargets = new();

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
                
                // 添加目标
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
                    return true; // 同时清理已释放的弱引用
                });
                
                // 从当前刷新目标中移除
                _currentRefreshTargets.Remove(target);
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
        /// 修复：避免重复刷新和不必要的交叉触发
        /// </summary>
        public void SyncRefresh()
        {
            // 防止重复刷新
            if (_hasPendingSync) return;
            
            _hasPendingSync = true;
            
            // 统一使用 Normal 优先级，避免渲染顺序不一致
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _hasPendingSync = false;
                    RefreshAllTargets();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"同步渲染失败: {ex.Message}");
                    _hasPendingSync = false; // 确保在异常情况下重置标志
                }
            }, DispatcherPriority.Normal);
        }

        /// <summary>
        /// 立即同步刷新，用于拖拽等实时交互
        /// </summary>
        public void ImmediateSyncRefresh()
        {
            if (_hasPendingSync) return; // 避免重复的立即刷新
            
            _hasPendingSync = true;
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _hasPendingSync = false;
                    RefreshAllTargets();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"立即渲染失败: {ex.Message}");
                    _hasPendingSync = false;
                }
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// 选择性刷新特定目标，避免不必要的全局刷新
        /// </summary>
        public void SelectiveRefresh(IRenderSyncTarget specificTarget)
        {
            if (_hasPendingSync) return;
            
            _hasPendingSync = true;
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _hasPendingSync = false;
                    
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
                    _hasPendingSync = false;
                }
            }, DispatcherPriority.Normal);
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
                
                // 同步刷新所有有效目标，但避免重复刷新
                foreach (var target in validTargets)
                {
                    if (!_currentRefreshTargets.Contains(target))
                    {
                        _currentRefreshTargets.Add(target);
                        try
                        {
                            target.RefreshRender();
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
            }
        }
    }
}