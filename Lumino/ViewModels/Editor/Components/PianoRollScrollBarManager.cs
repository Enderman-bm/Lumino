using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumino.ViewModels.Editor;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// ���پ����Ĺ����������������������Զ����������PianoRollϵͳ�ļ���
    /// �����ϸ��ո�������+8С�ڵı�׼����������
    /// </summary>
    public partial class PianoRollScrollBarManager : ObservableObject
    {
        #region ������ʵ��
        /// <summary>
        /// ˮƽ������ViewModel
        /// </summary>
        public CustomScrollBarViewModel HorizontalScrollBar { get; }

        /// <summary>
        /// ��ֱ������ViewModel
        /// </summary>
        public CustomScrollBarViewModel VerticalScrollBar { get; }
        #endregion

        #region ˽���ֶ�
        private PianoRollViewModel? _pianoRollViewModel;
        private bool _isUpdatingFromPianoRoll = false;
        private bool _isUpdatingFromScrollBar = false;
        private bool _recentlyExtended = false; // �Ƿ�刚刚延长过，防止连续触发
        #endregion

        #region ���캯��
        public PianoRollScrollBarManager()
        {
            // ����ˮƽ�ʹ�ֱ������
            HorizontalScrollBar = new CustomScrollBarViewModel(ScrollBarOrientation.Horizontal);
            VerticalScrollBar = new CustomScrollBarViewModel(ScrollBarOrientation.Vertical);

            // ���Ĺ������¼�
            SubscribeToScrollBarEvents();
        }
        #endregion

        #region ��ʼ��
        /// <summary>
        /// ����PianoRoll��ͼģ�ͣ�����˫���
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
                
                // ȷ��������״̬��ȷ��ʼ��
                ForceUpdateScrollBars();
            }
        }
        #endregion

        #region �¼�����
        private void SubscribeToScrollBarEvents()
        {
            // ˮƽ�������¼�
            HorizontalScrollBar.ValueChanged += OnHorizontalScrollValueChanged;
            HorizontalScrollBar.ViewportSizeChanged += OnHorizontalViewportSizeChanged;

            // ��ֱ�������¼�
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

        #region ��������ʼ��
        private void InitializeScrollBars()
        {
            if (_pianoRollViewModel == null) return;

            _isUpdatingFromPianoRoll = true;

            try
            {
                // ʹ���µ��ϸ���㷽����ʼ��ˮƽ������
                UpdateHorizontalScrollBarParameters();

                // ��ʼ����ֱ������
                var verticalMax = Math.Max(_pianoRollViewModel.TotalHeight, _pianoRollViewModel.ViewportHeight);
                VerticalScrollBar.SetParameters(
                    minimum: 0,
                    maximum: verticalMax,
                    value: _pianoRollViewModel.VerticalScrollOffset,
                    viewportSize: _pianoRollViewModel.ViewportHeight
                );
                
                System.Diagnostics.Debug.WriteLine("[ScrollBarManager] ��������ʼ�����");
            }
            finally
            {
                _isUpdatingFromPianoRoll = false;
            }
        }
        #endregion

        #region PianoRoll���Ա仯����
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
                        LogScrollState("����ƫ�Ʊ仯");
                        break;

                    case nameof(PianoRollViewModel.VerticalScrollOffset):
                        VerticalScrollBar.SetValueDirect(_pianoRollViewModel?.VerticalScrollOffset ?? 0);
                        break;

                    case nameof(PianoRollViewModel.ViewportWidth):
                    case nameof(PianoRollViewModel.ViewportHeight):
                        UpdateHorizontalScrollBarParameters();
                        UpdateVerticalScrollBarParameters();
                        LogScrollState("�ӿڳߴ�仯");
                        break;

                    case nameof(PianoRollViewModel.MaxScrollExtent):
                        UpdateHorizontalScrollBarParameters();
                        LogScrollState("��������Χ�仯");
                        break;

                    case nameof(PianoRollViewModel.TotalHeight):
                        UpdateVerticalScrollBarParameters();
                        break;

                    case nameof(PianoRollViewModel.Zoom):
                        UpdateHorizontalScrollBarParameters();
                        LogScrollState("ˮƽ���ű仯");
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
        /// ����ˮƽ���������� - ʹ���µ��ϸ�����׼
        /// </summary>
        private void UpdateHorizontalScrollBarParameters()
        {
            if (_pianoRollViewModel == null) return;

            // ��ȡ��������λ��
            var noteEndPositions = _pianoRollViewModel.GetAllNotes().Select(n => n.StartPosition + n.Duration);
            
            // ���������Ч����
            var effectiveSongLength = _pianoRollViewModel.Calculations.CalculateEffectiveSongLength(
                noteEndPositions, 
                _pianoRollViewModel.HasMidiFileDuration ? _pianoRollViewModel.MidiFileDuration : null
            );
            
            // ����������ܳ��ȣ����أ�
            var scrollbarTotalLength = _pianoRollViewModel.Calculations.CalculateScrollbarTotalLengthInPixels(effectiveSongLength);
            
            // �ӿڿ���
            var viewportWidth = _pianoRollViewModel.ViewportWidth;
            
            // ��������������
            // Maximum = �������ܳ��ȣ������������ķ�Χ���Ǵ�0���ܳ���
            // ViewportSize = ��ǰ�ӿڿ��ȣ�������������ק��Ĵ�С����ȷ��ӳ�ӿڱ���
            // Value = ��ǰ����ƫ��
            HorizontalScrollBar.SetParameters(
                minimum: 0,
                maximum: scrollbarTotalLength,  // �ϸ���ڹ������ܳ���
                value: _pianoRollViewModel.CurrentScrollOffset,
                viewportSize: viewportWidth     // �ӿڿ��Ⱦ�����ק���С
            );
            
            System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] ˮƽ��������������:");
            System.Diagnostics.Debug.WriteLine($"  ������Ч����: {effectiveSongLength:F2} �ķ�����");
            System.Diagnostics.Debug.WriteLine($"  �������ܳ���: {scrollbarTotalLength:F1} ����");
            System.Diagnostics.Debug.WriteLine($"  �ӿڿ���: {viewportWidth:F1} ����");
            System.Diagnostics.Debug.WriteLine($"  ��ǰ����ƫ��: {_pianoRollViewModel.CurrentScrollOffset:F1} ����");
            System.Diagnostics.Debug.WriteLine($"  �ӿڱ���: {(viewportWidth / scrollbarTotalLength):P2}");
        }

        private void UpdateVerticalScrollBarParameters()
        {
            if (_pianoRollViewModel == null) return;

            // 使用实际可渲染区域高度作为视口大小，而不是整个视口高度
            // 这样可以确保滚动距离自适应卷帘的实际可用高度
            var verticalMax = Math.Max(_pianoRollViewModel.TotalHeight, _pianoRollViewModel.VerticalViewportSize);
            VerticalScrollBar.SetParameters(
                minimum: 0,
                maximum: verticalMax,
                value: _pianoRollViewModel.VerticalScrollOffset,
                viewportSize: _pianoRollViewModel.VerticalViewportSize
            );
        }

        /// <summary>
        /// ��¼����״̬���ڵ���
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

            System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] {context} - �ӿڱ���: {viewportRatio:P2}, ��������: {scrollRatio:P2}");
        }
        #endregion

        #region �������¼�����
        private void OnHorizontalScrollValueChanged(double value)
        {
            if (_isUpdatingFromPianoRoll || _pianoRollViewModel == null) return;

            _isUpdatingFromScrollBar = true;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] ˮƽ������ֵ�仯: {value:F1}");
                _pianoRollViewModel.SetCurrentScrollOffset(value);
                
                // 检查是否需要重置延长标志（当用户明显改变滚动方向时）
                CheckAndResetExtensionFlag(value);
                
                // 检查是否滚动到末尾，如果是则自动延长小节
                CheckAndExtendPianoRollIfNeeded(value);
                
                LogScrollState("��������ק");
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
                System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] ��ֱ������ֵ�仯: {value:F1}");
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
                // ˮƽ��������ViewportSize�仯��Ӧ���ű仯
                // ��ViewportSize��Сʱ����ʾҪ��ʾ�������ݣ���С��
                // ��ViewportSize���ʱ����ʾҪ��ʾ�������ݣ��Ŵ�
                
                var noteEndPositions = _pianoRollViewModel.GetAllNotes().Select(n => n.StartPosition + n.Duration);
                var scrollbarTotalLength = _pianoRollViewModel.Calculations.CalculateContentWidth(
                    noteEndPositions,
                    _pianoRollViewModel.HasMidiFileDuration ? _pianoRollViewModel.MidiFileDuration : null
                );
                
                // �����µ����ű���
                // ��ǰʵ���ӿڿ��ȱ��ֲ��䣬���߼��ӿڴ�С�����仯
                var currentRealViewportWidth = _pianoRollViewModel.ViewportWidth;
                var newZoomFactor = currentRealViewportWidth / Math.Max(1, viewportSize);
                
                // �������ŷ�Χ
                newZoomFactor = Math.Max(0.1, Math.Min(10.0, newZoomFactor));
                
                // ת��Ϊ����ֵ��0-100��
                var sliderValue = ZoomToSliderValue(newZoomFactor);
                _pianoRollViewModel.SetZoomSliderValue(sliderValue);
                
                System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] ˮƽViewportSize�仯: {viewportSize:F1} -> ����: {newZoomFactor:F3} -> ����: {sliderValue:F1}");
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
                // 垂直视口大小变化对应垂直缩放变化
                // 使用实际可渲染区域高度作为基准，确保缩放比例正确
                var baseViewportSize = _pianoRollViewModel.VerticalViewportSize;
                var newVerticalZoomFactor = baseViewportSize / Math.Max(1, viewportSize);
                
                // 限制缩放范围
                newVerticalZoomFactor = Math.Max(0.1, Math.Min(10.0, newVerticalZoomFactor));
                
                // 转换为滑块值(0-100)
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

        #region ����ת����������
        /// <summary>
        /// ����ϵ��ת��Ϊ����ֵ��0-100��
        /// </summary>
        private static double ZoomToSliderValue(double zoomFactor)
        {
            // ����ϵ����Χ�� 0.1x �� 10x
            // ����ֵ 0 ��Ӧ 0.1x������ֵ 100 ��Ӧ 10x
            // ʹ�ö��������Ի�ø��õ��û�����
            
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
        /// ����ֵ��0-100��ת��Ϊ����ϵ��
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

        #region ��������
        /// <summary>
        /// ���ù������������
        /// </summary>
        public void SetScrollBarTrackLengths(double horizontalLength, double verticalLength)
        {
            HorizontalScrollBar.SetTrackLength(horizontalLength);
            VerticalScrollBar.SetTrackLength(verticalLength);
        }

        /// <summary>
        /// ǿ�����¼��㲢�������й�����ص�����
        /// </summary>
        public void ForceUpdateScrollBars()
        {
            if (_pianoRollViewModel == null) 
            {
                System.Diagnostics.Debug.WriteLine("[ScrollBarManager] ǿ�Ƹ���ʧ�ܣ�PianoRollViewModelΪnull");
                return;
            }

            _isUpdatingFromPianoRoll = true;

            try
            {
                UpdateHorizontalScrollBarParameters();
                UpdateVerticalScrollBarParameters();
                
                System.Diagnostics.Debug.WriteLine("[ScrollBarManager] ǿ�Ƹ��¹��������");
                LogScrollState("ǿ�Ƹ���");
            }
            finally
            {
                _isUpdatingFromPianoRoll = false;
            }
        }

        /// <summary>
        /// ��ȡ��ǰ������״̬�������Ϣ
        /// </summary>
        public string GetScrollBarDiagnostics()
        {
            if (_pianoRollViewModel == null)
                return "PianoRollViewModelδ����";

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

            return $"�����������Ϣ:\n" +
                   $"������Ч����: {effectiveSongLength:F2} �ķ�����\n" +
                   $"�������ܳ���: {scrollbarTotalLength:F1} ����\n" +
                   $"�ӿڿ���: {_pianoRollViewModel.ViewportWidth:F1} ����\n" +
                   $"�ӿڱ���: {viewportRatio:P2}\n" +
                   $"��ǰ����ƫ��: {_pianoRollViewModel.CurrentScrollOffset:F1} ����\n" +
                   $"����λ�ñ���: {scrollRatio:P2}\n" +
                   $"������Maximum: {HorizontalScrollBar.Maximum:F1}\n" +
                   $"������ViewportSize: {HorizontalScrollBar.ViewportSize:F1}\n" +
                   $"������Value: {HorizontalScrollBar.Value:F1}";
        }
        #endregion

        #region ����
        /// <summary>
        /// ������Դ
        /// </summary>
        public void Cleanup()
        {
            UnsubscribeFromPianoRoll();
            _pianoRollViewModel = null;
        }

        private void CheckAndExtendPianoRollIfNeeded(double scrollValue)
        {
            if (_pianoRollViewModel == null) return;

            // 如果刚刚延长过，暂时跳过检查，防止连续触发
            if (_recentlyExtended) return;

            // 计算滚动条的滚动比例
            var scrollRange = HorizontalScrollBar.ScrollableRange;
            if (scrollRange <= 0) return;

            var scrollRatio = (scrollValue - HorizontalScrollBar.Minimum) / scrollRange;

            // 如果滚动到95%以上的位置，认为滚动到末尾
            const double extendThreshold = 0.95;
            if (scrollRatio >= extendThreshold)
            {
                System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] 检测到滚动到末尾 (比例: {scrollRatio:P2})，自动延长钢琴卷帘");
                _pianoRollViewModel.ExtendPianoRollMeasures();
                
                // 设置标志，防止连续触发
                _recentlyExtended = true;
                
                // 强制更新滚动条参数，因为延长后滚动范围可能变化
                ForceUpdateScrollBars();
            }
        }

        /// <summary>
        /// 检查是否需要重置延长标志
        /// 当用户明显离开末尾区域时重置标志
        /// </summary>
        /// <param name="currentValue">当前的滚动条值</param>
        private void CheckAndResetExtensionFlag(double currentValue)
        {
            if (!_recentlyExtended) return;

            // 计算滚动条的滚动比例
            var scrollRange = HorizontalScrollBar.ScrollableRange;
            if (scrollRange <= 0) return;

            var currentRatio = (currentValue - HorizontalScrollBar.Minimum) / scrollRange;

            // 如果当前位置低于85%（明显离开末尾区域），重置标志
            const double resetThreshold = 0.85;
            if (currentRatio < resetThreshold)
            {
                System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] 检测到离开末尾区域，重置延长标志 (当前: {currentRatio:P2})");
                _recentlyExtended = false;
            }
        }
        #endregion
    }
}