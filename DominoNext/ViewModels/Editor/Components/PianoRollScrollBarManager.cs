using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DominoNext.ViewModels.Editor;

namespace DominoNext.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘的滚动条管理器，负责连接自定义滚动条与PianoRoll系统的集成
    /// 现在严格按照歌曲长度+8小节的标准管理滚动条
    /// </summary>
    public partial class PianoRollScrollBarManager : ObservableObject
    {
        #region 滚动条实例
        /// <summary>
        /// 水平滚动条ViewModel
        /// </summary>
        public CustomScrollBarViewModel HorizontalScrollBar { get; }

        /// <summary>
        /// 垂直滚动条ViewModel
        /// </summary>
        public CustomScrollBarViewModel VerticalScrollBar { get; }
        #endregion

        #region 私有字段
        private PianoRollViewModel? _pianoRollViewModel;
        private bool _isUpdatingFromPianoRoll = false;
        private bool _isUpdatingFromScrollBar = false;
        #endregion

        #region 构造函数
        public PianoRollScrollBarManager()
        {
            // 创建水平和垂直滚动条
            HorizontalScrollBar = new CustomScrollBarViewModel(ScrollBarOrientation.Horizontal);
            VerticalScrollBar = new CustomScrollBarViewModel(ScrollBarOrientation.Vertical);

            // 订阅滚动条事件
            SubscribeToScrollBarEvents();
        }
        #endregion

        #region 初始化
        /// <summary>
        /// 设置PianoRoll视图模型，建立双向绑定
        /// </summary>
        public void SetPianoRollViewModel(PianoRollViewModel pianoRollViewModel)
        {
            if (_pianoRollViewModel != null)
            {
                UnsubscribeFromPianoRoll();
            }

            _pianoRollViewModel = pianoRollViewModel;
            
            if (_pianoRollViewModel != null)
            {
                SubscribeToPianoRoll();
                InitializeScrollBars();
                
                // 确保滚动条状态正确初始化
                ForceUpdateScrollBars();
            }
        }
        #endregion

        #region 事件订阅
        private void SubscribeToScrollBarEvents()
        {
            // 水平滚动条事件
            HorizontalScrollBar.ValueChanged += OnHorizontalScrollValueChanged;
            HorizontalScrollBar.ViewportSizeChanged += OnHorizontalViewportSizeChanged;

            // 垂直滚动条事件
            VerticalScrollBar.ValueChanged += OnVerticalScrollValueChanged;
            VerticalScrollBar.ViewportSizeChanged += OnVerticalViewportSizeChanged;
        }

        private void SubscribeToPianoRoll()
        {
            if (_pianoRollViewModel != null)
            {
                _pianoRollViewModel.PropertyChanged += OnPianoRollPropertyChanged;
            }
        }

        private void UnsubscribeFromPianoRoll()
        {
            if (_pianoRollViewModel != null)
            {
                _pianoRollViewModel.PropertyChanged -= OnPianoRollPropertyChanged;
            }
        }
        #endregion

        #region 滚动条初始化
        private void InitializeScrollBars()
        {
            if (_pianoRollViewModel == null) return;

            _isUpdatingFromPianoRoll = true;

            try
            {
                // 使用新的严格计算方法初始化水平滚动条
                UpdateHorizontalScrollBarParameters();

                // 初始化垂直滚动条
                var verticalMax = Math.Max(_pianoRollViewModel.TotalHeight, _pianoRollViewModel.ViewportHeight);
                VerticalScrollBar.SetParameters(
                    minimum: 0,
                    maximum: verticalMax,
                    value: _pianoRollViewModel.VerticalScrollOffset,
                    viewportSize: _pianoRollViewModel.ViewportHeight
                );
                
                System.Diagnostics.Debug.WriteLine("[ScrollBarManager] 滚动条初始化完成");
            }
            finally
            {
                _isUpdatingFromPianoRoll = false;
            }
        }
        #endregion

        #region PianoRoll属性变化处理
        private void OnPianoRollPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isUpdatingFromScrollBar) return;

            _isUpdatingFromPianoRoll = true;

            try
            {
                switch (e.PropertyName)
                {
                    case nameof(PianoRollViewModel.CurrentScrollOffset):
                        HorizontalScrollBar.SetValueDirect(_pianoRollViewModel?.CurrentScrollOffset ?? 0);
                        LogScrollState("滚动偏移变化");
                        break;

                    case nameof(PianoRollViewModel.VerticalScrollOffset):
                        VerticalScrollBar.SetValueDirect(_pianoRollViewModel?.VerticalScrollOffset ?? 0);
                        break;

                    case nameof(PianoRollViewModel.ViewportWidth):
                    case nameof(PianoRollViewModel.ViewportHeight):
                        UpdateHorizontalScrollBarParameters();
                        UpdateVerticalScrollBarParameters();
                        LogScrollState("视口尺寸变化");
                        break;

                    case nameof(PianoRollViewModel.MaxScrollExtent):
                        UpdateHorizontalScrollBarParameters();
                        LogScrollState("最大滚动范围变化");
                        break;

                    case nameof(PianoRollViewModel.TotalHeight):
                        UpdateVerticalScrollBarParameters();
                        break;

                    case nameof(PianoRollViewModel.Zoom):
                        UpdateHorizontalScrollBarParameters();
                        LogScrollState("水平缩放变化");
                        break;

                    case nameof(PianoRollViewModel.VerticalZoom):
                        UpdateVerticalScrollBarParameters();
                        break;
                }
            }
            finally
            {
                _isUpdatingFromPianoRoll = false;
            }
        }

        /// <summary>
        /// 更新水平滚动条参数 - 使用新的严格计算标准
        /// </summary>
        private void UpdateHorizontalScrollBarParameters()
        {
            if (_pianoRollViewModel == null) return;

            // 获取音符结束位置
            var noteEndPositions = _pianoRollViewModel.GetAllNotes().Select(n => n.StartPosition + n.Duration);
            
            // 计算歌曲有效长度
            var effectiveSongLength = _pianoRollViewModel.Calculations.CalculateEffectiveSongLength(
                noteEndPositions, 
                _pianoRollViewModel.HasMidiFileDuration ? _pianoRollViewModel.MidiFileDuration : null
            );
            
            // 计算滚动条总长度（像素）
            var scrollbarTotalLength = _pianoRollViewModel.Calculations.CalculateScrollbarTotalLengthInPixels(effectiveSongLength);
            
            // 视口宽度
            var viewportWidth = _pianoRollViewModel.ViewportWidth;
            
            // 滚动条参数设置
            // Maximum = 滚动条总长度，这样滚动条的范围就是从0到总长度
            // ViewportSize = 当前视口宽度，这样滚动条拖拽块的大小就正确反映视口比例
            // Value = 当前滚动偏移
            HorizontalScrollBar.SetParameters(
                minimum: 0,
                maximum: scrollbarTotalLength,  // 严格等于滚动条总长度
                value: _pianoRollViewModel.CurrentScrollOffset,
                viewportSize: viewportWidth     // 视口宽度决定拖拽块大小
            );
            
            System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] 水平滚动条参数更新:");
            System.Diagnostics.Debug.WriteLine($"  歌曲有效长度: {effectiveSongLength:F2} 四分音符");
            System.Diagnostics.Debug.WriteLine($"  滚动条总长度: {scrollbarTotalLength:F1} 像素");
            System.Diagnostics.Debug.WriteLine($"  视口宽度: {viewportWidth:F1} 像素");
            System.Diagnostics.Debug.WriteLine($"  当前滚动偏移: {_pianoRollViewModel.CurrentScrollOffset:F1} 像素");
            System.Diagnostics.Debug.WriteLine($"  视口比例: {(viewportWidth / scrollbarTotalLength):P2}");
        }

        private void UpdateVerticalScrollBarParameters()
        {
            if (_pianoRollViewModel == null) return;

            var verticalMax = Math.Max(_pianoRollViewModel.TotalHeight, _pianoRollViewModel.ViewportHeight);
            VerticalScrollBar.SetParameters(
                minimum: 0,
                maximum: verticalMax,
                value: _pianoRollViewModel.VerticalScrollOffset,
                viewportSize: _pianoRollViewModel.ViewportHeight
            );
        }

        /// <summary>
        /// 记录滚动状态用于调试
        /// </summary>
        private void LogScrollState(string context)
        {
            if (_pianoRollViewModel == null) return;

            var noteEndPositions = _pianoRollViewModel.GetAllNotes().Select(n => n.StartPosition + n.Duration);
            var viewportRatio = _pianoRollViewModel.Calculations.CalculateViewportRatio(
                _pianoRollViewModel.ViewportWidth, 
                noteEndPositions,
                _pianoRollViewModel.HasMidiFileDuration ? _pianoRollViewModel.MidiFileDuration : null
            );
            var scrollRatio = _pianoRollViewModel.Calculations.CalculateScrollPositionRatio(
                _pianoRollViewModel.CurrentScrollOffset,
                _pianoRollViewModel.ViewportWidth,
                noteEndPositions,
                _pianoRollViewModel.HasMidiFileDuration ? _pianoRollViewModel.MidiFileDuration : null
            );

            System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] {context} - 视口比例: {viewportRatio:P2}, 滚动比例: {scrollRatio:P2}");
        }
        #endregion

        #region 滚动条事件处理
        private void OnHorizontalScrollValueChanged(double value)
        {
            if (_isUpdatingFromPianoRoll || _pianoRollViewModel == null) return;

            _isUpdatingFromScrollBar = true;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] 水平滚动条值变化: {value:F1}");
                _pianoRollViewModel.SetCurrentScrollOffset(value);
                LogScrollState("滚动条拖拽");
            }
            finally
            {
                _isUpdatingFromScrollBar = false;
            }
        }

        private void OnVerticalScrollValueChanged(double value)
        {
            if (_isUpdatingFromPianoRoll || _pianoRollViewModel == null) return;

            _isUpdatingFromScrollBar = true;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] 垂直滚动条值变化: {value:F1}");
                _pianoRollViewModel.SetVerticalScrollOffset(value);
            }
            finally
            {
                _isUpdatingFromScrollBar = false;
            }
        }

        private void OnHorizontalViewportSizeChanged(double viewportSize)
        {
            if (_isUpdatingFromPianoRoll || _pianoRollViewModel == null) return;

            _isUpdatingFromScrollBar = true;

            try
            {
                // 水平滚动条的ViewportSize变化对应缩放变化
                // 当ViewportSize变小时，表示要显示更多内容（缩小）
                // 当ViewportSize变大时，表示要显示更少内容（放大）
                
                var noteEndPositions = _pianoRollViewModel.GetAllNotes().Select(n => n.StartPosition + n.Duration);
                var scrollbarTotalLength = _pianoRollViewModel.Calculations.CalculateContentWidth(
                    noteEndPositions,
                    _pianoRollViewModel.HasMidiFileDuration ? _pianoRollViewModel.MidiFileDuration : null
                );
                
                // 计算新的缩放比例
                // 当前实际视口宽度保持不变，但逻辑视口大小发生变化
                var currentRealViewportWidth = _pianoRollViewModel.ViewportWidth;
                var newZoomFactor = currentRealViewportWidth / Math.Max(1, viewportSize);
                
                // 限制缩放范围
                newZoomFactor = Math.Max(0.1, Math.Min(10.0, newZoomFactor));
                
                // 转换为滑块值（0-100）
                var sliderValue = ZoomToSliderValue(newZoomFactor);
                _pianoRollViewModel.SetZoomSliderValue(sliderValue);
                
                System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] 水平ViewportSize变化: {viewportSize:F1} -> 缩放: {newZoomFactor:F3} -> 滑块: {sliderValue:F1}");
            }
            finally
            {
                _isUpdatingFromScrollBar = false;
            }
        }

        private void OnVerticalViewportSizeChanged(double viewportSize)
        {
            if (_isUpdatingFromPianoRoll || _pianoRollViewModel == null) return;

            _isUpdatingFromScrollBar = true;

            try
            {
                // 垂直滚动条的ViewportSize变化对应垂直缩放变化
                var baseViewportSize = _pianoRollViewModel.ViewportHeight;
                var newVerticalZoomFactor = baseViewportSize / Math.Max(1, viewportSize);
                
                // 限制缩放范围
                newVerticalZoomFactor = Math.Max(0.1, Math.Min(10.0, newVerticalZoomFactor));
                
                // 转换为滑块值（0-100）
                var sliderValue = ZoomToSliderValue(newVerticalZoomFactor);
                _pianoRollViewModel.SetVerticalZoomSliderValue(sliderValue);
                
                System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] 垂直ViewportSize变化: {viewportSize:F1} -> 缩放: {newVerticalZoomFactor:F3}");
            }
            finally
            {
                _isUpdatingFromScrollBar = false;
            }
        }
        #endregion

        #region 缩放转换辅助方法
        /// <summary>
        /// 缩放系数转换为滑块值（0-100）
        /// </summary>
        private static double ZoomToSliderValue(double zoomFactor)
        {
            // 缩放系数范围： 0.1x 到 10x
            // 滑块值 0 对应 0.1x，滑块值 100 对应 10x
            // 使用对数缩放以获得更好的用户体验
            
            var minZoom = 0.1;
            var maxZoom = 10.0;
            
            var normalizedZoom = Math.Max(minZoom, Math.Min(maxZoom, zoomFactor));
            var logMin = Math.Log(minZoom);
            var logMax = Math.Log(maxZoom);
            var logZoom = Math.Log(normalizedZoom);
            
            var sliderValue = ((logZoom - logMin) / (logMax - logMin)) * 100;
            return Math.Max(0, Math.Min(100, sliderValue));
        }

        /// <summary>
        /// 滑块值（0-100）转换为缩放系数
        /// </summary>
        private static double SliderValueToZoom(double sliderValue)
        {
            var minZoom = 0.1;
            var maxZoom = 10.0;
            
            var normalizedSlider = Math.Max(0, Math.Min(100, sliderValue)) / 100.0;
            var logMin = Math.Log(minZoom);
            var logMax = Math.Log(maxZoom);
            var logZoom = logMin + (normalizedSlider * (logMax - logMin));
            
            return Math.Exp(logZoom);
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 设置滚动条轨道长度
        /// </summary>
        public void SetScrollBarTrackLengths(double horizontalLength, double verticalLength)
        {
            HorizontalScrollBar.SetTrackLength(horizontalLength);
            VerticalScrollBar.SetTrackLength(verticalLength);
        }

        /// <summary>
        /// 强制重新计算并更新所有滚动相关的属性
        /// </summary>
        public void ForceUpdateScrollBars()
        {
            if (_pianoRollViewModel == null) 
            {
                System.Diagnostics.Debug.WriteLine("[ScrollBarManager] 强制更新失败，PianoRollViewModel为null");
                return;
            }

            _isUpdatingFromPianoRoll = true;

            try
            {
                UpdateHorizontalScrollBarParameters();
                UpdateVerticalScrollBarParameters();
                
                System.Diagnostics.Debug.WriteLine("[ScrollBarManager] 强制更新滚动条完成");
                LogScrollState("强制更新");
            }
            finally
            {
                _isUpdatingFromPianoRoll = false;
            }
        }

        /// <summary>
        /// 获取当前滚动条状态的诊断信息
        /// </summary>
        public string GetScrollBarDiagnostics()
        {
            if (_pianoRollViewModel == null)
                return "PianoRollViewModel未连接";

            var noteEndPositions = _pianoRollViewModel.GetAllNotes().Select(n => n.StartPosition + n.Duration);
            var effectiveSongLength = _pianoRollViewModel.Calculations.CalculateEffectiveSongLength(
                noteEndPositions,
                _pianoRollViewModel.HasMidiFileDuration ? _pianoRollViewModel.MidiFileDuration : null
            );
            var scrollbarTotalLength = _pianoRollViewModel.Calculations.CalculateScrollbarTotalLengthInPixels(effectiveSongLength);
            var viewportRatio = _pianoRollViewModel.Calculations.CalculateViewportRatio(
                _pianoRollViewModel.ViewportWidth,
                noteEndPositions,
                _pianoRollViewModel.HasMidiFileDuration ? _pianoRollViewModel.MidiFileDuration : null
            );
            var scrollRatio = _pianoRollViewModel.Calculations.CalculateScrollPositionRatio(
                _pianoRollViewModel.CurrentScrollOffset,
                _pianoRollViewModel.ViewportWidth,
                noteEndPositions,
                _pianoRollViewModel.HasMidiFileDuration ? _pianoRollViewModel.MidiFileDuration : null
            );

            return $"滚动条诊断信息:\n" +
                   $"歌曲有效长度: {effectiveSongLength:F2} 四分音符\n" +
                   $"滚动条总长度: {scrollbarTotalLength:F1} 像素\n" +
                   $"视口宽度: {_pianoRollViewModel.ViewportWidth:F1} 像素\n" +
                   $"视口比例: {viewportRatio:P2}\n" +
                   $"当前滚动偏移: {_pianoRollViewModel.CurrentScrollOffset:F1} 像素\n" +
                   $"滚动位置比例: {scrollRatio:P2}\n" +
                   $"滚动条Maximum: {HorizontalScrollBar.Maximum:F1}\n" +
                   $"滚动条ViewportSize: {HorizontalScrollBar.ViewportSize:F1}\n" +
                   $"滚动条Value: {HorizontalScrollBar.Value:F1}";
        }
        #endregion

        #region 清理
        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            UnsubscribeFromPianoRoll();
            _pianoRollViewModel = null;
        }
        #endregion
    }
}