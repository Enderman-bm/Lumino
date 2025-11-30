using System;
using Avalonia.Threading;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// 平滑滚动管理器 - 提供缓动的滚动效果
    /// </summary>
    public class SmoothScrollManager : IDisposable
    {
        #region 配置常量
        /// <summary>
        /// 滚动动画帧率（每秒帧数）
        /// </summary>
        private const int ANIMATION_FPS = 120;
        
        /// <summary>
        /// 动画定时器间隔（毫秒）
        /// </summary>
        private const double TIMER_INTERVAL_MS = 1000.0 / ANIMATION_FPS;
        
        /// <summary>
        /// 默认平滑因子（0-1，越大越快）
        /// </summary>
        private const double DEFAULT_SMOOTHNESS = 0.15;
        
        /// <summary>
        /// 停止阈值（像素）- 当差值小于此值时停止动画
        /// </summary>
        private const double STOP_THRESHOLD = 0.5;
        
        /// <summary>
        /// 最大速度限制（像素/帧）
        /// </summary>
        private const double MAX_VELOCITY = 500.0;
        #endregion
        
        #region 私有字段
        private readonly DispatcherTimer _animationTimer;
        private readonly Action<double, double> _onScrollUpdate;
        
        private double _currentHorizontalOffset;
        private double _currentVerticalOffset;
        private double _targetHorizontalOffset;
        private double _targetVerticalOffset;
        
        private double _horizontalVelocity;
        private double _verticalVelocity;
        
        private double _smoothness = DEFAULT_SMOOTHNESS;
        private bool _isAnimating;
        private bool _disposed;
        #endregion
        
        #region 公共属性
        /// <summary>
        /// 是否正在进行平滑滚动动画
        /// </summary>
        public bool IsAnimating => _isAnimating;
        
        /// <summary>
        /// 当前水平滚动偏移量
        /// </summary>
        public double CurrentHorizontalOffset => _currentHorizontalOffset;
        
        /// <summary>
        /// 当前垂直滚动偏移量
        /// </summary>
        public double CurrentVerticalOffset => _currentVerticalOffset;
        
        /// <summary>
        /// 目标水平滚动偏移量
        /// </summary>
        public double TargetHorizontalOffset => _targetHorizontalOffset;
        
        /// <summary>
        /// 目标垂直滚动偏移量
        /// </summary>
        public double TargetVerticalOffset => _targetVerticalOffset;
        
        /// <summary>
        /// 平滑因子（0-1，越大响应越快）
        /// </summary>
        public double Smoothness
        {
            get => _smoothness;
            set => _smoothness = Math.Clamp(value, 0.01, 1.0);
        }
        
        /// <summary>
        /// 是否启用平滑滚动
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        #endregion
        
        #region 构造函数
        /// <summary>
        /// 创建平滑滚动管理器
        /// </summary>
        /// <param name="onScrollUpdate">滚动更新回调，参数为(水平偏移, 垂直偏移)</param>
        public SmoothScrollManager(Action<double, double> onScrollUpdate)
        {
            _onScrollUpdate = onScrollUpdate ?? throw new ArgumentNullException(nameof(onScrollUpdate));
            
            _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(TIMER_INTERVAL_MS)
            };
            _animationTimer.Tick += OnAnimationTick;
        }
        #endregion
        
        #region 公共方法
        /// <summary>
        /// 设置当前滚动位置（不触发动画，用于初始化或同步）
        /// </summary>
        public void SetCurrentPosition(double horizontalOffset, double verticalOffset)
        {
            _currentHorizontalOffset = horizontalOffset;
            _currentVerticalOffset = verticalOffset;
            _targetHorizontalOffset = horizontalOffset;
            _targetVerticalOffset = verticalOffset;
            _horizontalVelocity = 0;
            _verticalVelocity = 0;
        }
        
        /// <summary>
        /// 同步水平位置
        /// </summary>
        public void SyncHorizontalPosition(double offset)
        {
            _currentHorizontalOffset = offset;
            _targetHorizontalOffset = offset;
            _horizontalVelocity = 0;
        }
        
        /// <summary>
        /// 同步垂直位置
        /// </summary>
        public void SyncVerticalPosition(double offset)
        {
            _currentVerticalOffset = offset;
            _targetVerticalOffset = offset;
            _verticalVelocity = 0;
        }
        
        /// <summary>
        /// 平滑滚动到目标位置
        /// </summary>
        /// <param name="targetHorizontal">目标水平偏移（如果为null则保持当前值）</param>
        /// <param name="targetVertical">目标垂直偏移（如果为null则保持当前值）</param>
        public void ScrollTo(double? targetHorizontal, double? targetVertical)
        {
            if (targetHorizontal.HasValue)
            {
                _targetHorizontalOffset = targetHorizontal.Value;
            }
            
            if (targetVertical.HasValue)
            {
                _targetVerticalOffset = targetVertical.Value;
            }
            
            if (!IsEnabled)
            {
                // 平滑滚动禁用时直接跳转
                _currentHorizontalOffset = _targetHorizontalOffset;
                _currentVerticalOffset = _targetVerticalOffset;
                _onScrollUpdate(_currentHorizontalOffset, _currentVerticalOffset);
                return;
            }
            
            StartAnimation();
        }
        
        /// <summary>
        /// 添加水平滚动增量
        /// </summary>
        public void AddHorizontalDelta(double delta)
        {
            _targetHorizontalOffset += delta;
            
            if (!IsEnabled)
            {
                _currentHorizontalOffset = _targetHorizontalOffset;
                _onScrollUpdate(_currentHorizontalOffset, _currentVerticalOffset);
                return;
            }
            
            StartAnimation();
        }
        
        /// <summary>
        /// 添加垂直滚动增量
        /// </summary>
        public void AddVerticalDelta(double delta)
        {
            _targetVerticalOffset += delta;
            
            if (!IsEnabled)
            {
                _currentVerticalOffset = _targetVerticalOffset;
                _onScrollUpdate(_currentHorizontalOffset, _currentVerticalOffset);
                return;
            }
            
            StartAnimation();
        }
        
        /// <summary>
        /// 立即停止动画并跳转到目标位置
        /// </summary>
        public void StopAndJump()
        {
            StopAnimation();
            _currentHorizontalOffset = _targetHorizontalOffset;
            _currentVerticalOffset = _targetVerticalOffset;
            _horizontalVelocity = 0;
            _verticalVelocity = 0;
            _onScrollUpdate(_currentHorizontalOffset, _currentVerticalOffset);
        }
        
        /// <summary>
        /// 停止动画（保持当前位置）
        /// </summary>
        public void Stop()
        {
            StopAnimation();
            _targetHorizontalOffset = _currentHorizontalOffset;
            _targetVerticalOffset = _currentVerticalOffset;
            _horizontalVelocity = 0;
            _verticalVelocity = 0;
        }
        
        /// <summary>
        /// 限制目标偏移量在有效范围内
        /// </summary>
        public void ClampTargetOffsets(double minHorizontal, double maxHorizontal, double minVertical, double maxVertical)
        {
            _targetHorizontalOffset = Math.Clamp(_targetHorizontalOffset, minHorizontal, maxHorizontal);
            _targetVerticalOffset = Math.Clamp(_targetVerticalOffset, minVertical, maxVertical);
        }
        #endregion
        
        #region 私有方法
        private void StartAnimation()
        {
            if (!_isAnimating)
            {
                _isAnimating = true;
                _animationTimer.Start();
            }
        }
        
        private void StopAnimation()
        {
            if (_isAnimating)
            {
                _isAnimating = false;
                _animationTimer.Stop();
            }
        }
        
        private void OnAnimationTick(object? sender, EventArgs e)
        {
            if (_disposed) return;
            
            // 计算目标差值
            var horizontalDiff = _targetHorizontalOffset - _currentHorizontalOffset;
            var verticalDiff = _targetVerticalOffset - _currentVerticalOffset;
            
            // 检查是否需要继续动画
            if (Math.Abs(horizontalDiff) < STOP_THRESHOLD && Math.Abs(verticalDiff) < STOP_THRESHOLD)
            {
                // 到达目标，停止动画
                _currentHorizontalOffset = _targetHorizontalOffset;
                _currentVerticalOffset = _targetVerticalOffset;
                _horizontalVelocity = 0;
                _verticalVelocity = 0;
                StopAnimation();
                _onScrollUpdate(_currentHorizontalOffset, _currentVerticalOffset);
                return;
            }
            
            // 使用指数平滑（弹性缓动效果）
            // 计算期望速度
            var targetHVelocity = horizontalDiff * _smoothness;
            var targetVVelocity = verticalDiff * _smoothness;
            
            // 限制最大速度
            targetHVelocity = Math.Clamp(targetHVelocity, -MAX_VELOCITY, MAX_VELOCITY);
            targetVVelocity = Math.Clamp(targetVVelocity, -MAX_VELOCITY, MAX_VELOCITY);
            
            // 平滑速度变化（减少抖动）
            _horizontalVelocity = _horizontalVelocity * 0.5 + targetHVelocity * 0.5;
            _verticalVelocity = _verticalVelocity * 0.5 + targetVVelocity * 0.5;
            
            // 应用速度
            _currentHorizontalOffset += _horizontalVelocity;
            _currentVerticalOffset += _verticalVelocity;
            
            // 触发更新回调
            _onScrollUpdate(_currentHorizontalOffset, _currentVerticalOffset);
        }
        #endregion
        
        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            StopAnimation();
            _animationTimer.Tick -= OnAnimationTick;
        }
        #endregion
    }
}
