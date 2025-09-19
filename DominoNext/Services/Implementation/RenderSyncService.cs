using System;
using System.Collections.Generic;
using Avalonia.Threading;
using Lumino.Services.Interfaces;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// ��Ⱦͬ������ - ȷ����CanvasЭͬ��Ⱦ���ر�����ק�͹���ʱ
    /// ʵʱ�汾���Ƴ������ӳ٣�ȷ������ͬ����Ⱦ
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
            System.Diagnostics.Debug.WriteLine("RenderSyncService ��ʼ�� - ʵʱͬ���汾");
        }

        /// <summary>
        /// ע����Ҫͬ����Ⱦ��Ŀ��
        /// </summary>
        public void RegisterTarget(IRenderSyncTarget target)
        {
            lock (_lock)
            {
                // �������ͷŵ�������
                _targets.RemoveAll(wr => !wr.TryGetTarget(out _));
                
                // ����Ŀ��
                _targets.Add(new WeakReference<IRenderSyncTarget>(target));
                System.Diagnostics.Debug.WriteLine($"ע����ȾĿ�꣬��ǰ����: {_targets.Count}");
            }
        }

        /// <summary>
        /// ע����ȾĿ��
        /// </summary>
        public void UnregisterTarget(IRenderSyncTarget target)
        {
            lock (_lock)
            {
                _targets.RemoveAll(wr => 
                {
                    if (wr.TryGetTarget(out var t))
                        return ReferenceEquals(t, target);
                    return true; // ͬʱ�������ͷŵ�������
                });
                
                // �ӵ�ǰˢ��Ŀ�����Ƴ�
                _currentRefreshTargets.Remove(target);
                System.Diagnostics.Debug.WriteLine($"ע����ȾĿ�꣬��ǰ����: {_targets.Count}");
            }
        }

        /// <summary>
        /// ������ק״̬
        /// </summary>
        public void SetDragState(bool isDragging)
        {
            var oldDragging = _isDragging;
            _isDragging = isDragging;
            
            if (oldDragging != isDragging)
            {
                System.Diagnostics.Debug.WriteLine($"��ק״̬�仯: {isDragging}");
                
                // ��ק״̬�仯ʱ����ͬ��ˢ��
                if (!isDragging && oldDragging)
                {
                    ImmediateSyncRefresh();
                }
            }
        }

        /// <summary>
        /// ͬ��ˢ������ע�����ȾĿ�� - ����ִ�У����ӳ�
        /// </summary>
        public void SyncRefresh()
        {
            // ����ִ�У������κ��ӳ��ж�
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    RefreshAllTargets();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ͬ����Ⱦʧ��: {ex.Message}");
                }
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// ����ͬ��ˢ�£�������ק��ʵʱ���� - ������ȼ������ӳ�
        /// </summary>
        public void ImmediateSyncRefresh()
        {
            // ����ִ�У�������ȼ�
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    RefreshAllTargets();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"������Ⱦʧ��: {ex.Message}");
                }
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// ѡ����ˢ���ض�Ŀ�� - ����ִ��
        /// </summary>
        public void SelectiveRefresh(IRenderSyncTarget specificTarget)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // ֻˢ���ض�Ŀ��
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
                    System.Diagnostics.Debug.WriteLine($"ѡ������Ⱦʧ��: {ex.Message}");
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
                
                // ������Ч����
                _targets.RemoveAll(wr => !wr.TryGetTarget(out _));
                
                // ͬ��ˢ��������ЧĿ�꣬�����ظ�ˢ��
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
                            System.Diagnostics.Debug.WriteLine($"Ŀ����Ⱦʧ��: {ex.Message}");
                        }
                        finally
                        {
                            _currentRefreshTargets.Remove(target);
                        }
                    }
                }
                
                if (refreshedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"ʵʱˢ���� {refreshedCount} ����ȾĿ��");
                }
            }
        }
    }
}