using System;
using DominoNext.Services.Interfaces;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 渲染刷新服务实现
    /// 提供统一的渲染刷新机制，解决拖拽时的影子问题
    /// </summary>
    public class RenderRefreshService : IRenderRefreshService
    {
        private readonly System.Timers.Timer _throttleTimer;
        private bool _hasPendingRefresh = false;
        private const double ThrottleInterval = 16.67; // 约60FPS

        public RenderRefreshService()
        {
            _throttleTimer = new System.Timers.Timer(ThrottleInterval);
            _throttleTimer.Elapsed += OnThrottleTimerElapsed;
            _throttleTimer.AutoReset = false;
        }

        /// <summary>
        /// 强制完全刷新渲染
        /// 清除所有缓存，重建可见元素，并立即重绘
        /// 用于创建新音符、删除音符等需要完全重建的操作
        /// </summary>
        public void ForceCompleteRefresh()
        {
            System.Diagnostics.Debug.WriteLine("RenderRefreshService: 强制完全刷新");
            
            // 立即触发强制刷新事件
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnForceRefreshRequested?.Invoke();
            }, Avalonia.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// 实时刷新渲染（用于拖拽等连续操作）
        /// 清除关键缓存，快速重绘
        /// 确保拖拽时的实时性和流畅性
        /// </summary>
        public void RealtimeRefresh()
        {
            System.Diagnostics.Debug.WriteLine("RenderRefreshService: 实时刷新");
            
            // 立即触发刷新，不使用延迟
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnRefreshRequested?.Invoke();
            }, Avalonia.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// 延迟刷新渲染（用于性能优化）
        /// 使用定时器合并多个刷新请求
        /// 用于不需要立即反馈的操作
        /// </summary>
        public void ThrottledRefresh()
        {
            _hasPendingRefresh = true;
            if (!_throttleTimer.Enabled)
            {
                _throttleTimer.Start();
            }
        }

        private void OnThrottleTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_hasPendingRefresh)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_hasPendingRefresh)
                    {
                        OnRefreshRequested?.Invoke();
                        _hasPendingRefresh = false;
                    }
                });
            }
        }

        /// <summary>
        /// 刷新请求事件
        /// </summary>
        public event Action? OnRefreshRequested;

        /// <summary>
        /// 强制刷新请求事件
        /// </summary>
        public event Action? OnForceRefreshRequested;

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            _throttleTimer?.Dispose();
        }
    }
}