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

            var verticalMax = Math.Max(_pianoRollViewModel.TotalHeight, _pianoRollViewModel.ViewportHeight);
            VerticalScrollBar.SetParameters(
                minimum: 0,
                maximum: verticalMax,
                value: _pianoRollViewModel.VerticalScrollOffset,
                viewportSize: _pianoRollViewModel.ViewportHeight
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
                // ��ֱ��������ViewportSize�仯��Ӧ��ֱ���ű仯
                var baseViewportSize = _pianoRollViewModel.ViewportHeight;
                var newVerticalZoomFactor = baseViewportSize / Math.Max(1, viewportSize);
                
                // �������ŷ�Χ
                newVerticalZoomFactor = Math.Max(0.1, Math.Min(10.0, newVerticalZoomFactor));
                
                // ת��Ϊ����ֵ��0-100��
                var sliderValue = ZoomToSliderValue(newVerticalZoomFactor);
                _pianoRollViewModel.SetVerticalZoomSliderValue(sliderValue);
                
                System.Diagnostics.Debug.WriteLine($"[ScrollBarManager] ��ֱViewportSize�仯: {viewportSize:F1} -> ����: {newVerticalZoomFactor:F3}");
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
        #endregion
    }
}