using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// ���پ����ӿڹ������ - ����������ӿڳߴ��
    /// ���ϵ�һְ��ԭ��רע���ӿں͹�����ص�״̬����
    /// </summary>
    public partial class PianoRollViewport : ObservableObject
    {
        #region �����������
        [ObservableProperty] private double _currentScrollOffset = 0.0;
        [ObservableProperty] private double _verticalScrollOffset = 0.0;
        [ObservableProperty] private double _timelinePosition;
        #endregion

        #region �ӿڳߴ�
        [ObservableProperty] private double _viewportWidth = 800.0;
        [ObservableProperty] private double _viewportHeight = 400.0;
        [ObservableProperty] private double _verticalViewportSize = 400.0;
        [ObservableProperty] private double _maxScrollExtent = 5000.0;
        #endregion

        #region ���ݿ���׷��
        /// <summary>
        /// ʵ�����ݿ��ȣ����ڹ�����Χ����
        /// </summary>
        [ObservableProperty] private double _contentWidth = 5000.0;
        #endregion

        #region ���캯��
        public PianoRollViewport()
        {
            PropertyChanged += OnPropertyChanged;
        }
        #endregion

        #region ������������
        /// <summary>
        /// �����ӿڳߴ�
        /// </summary>
        public void SetViewportSize(double width, double height)
        {
            ViewportWidth = width;
            ViewportHeight = height;
            VerticalViewportSize = height;
            
            // ���¼��������Χ����Ӧ�µ��ӿڳߴ�
            RecalculateScrollExtent();
            
            // ȷ����ǰ����λ������Ч��Χ��
            ValidateAndClampScrollOffsets();
        }

        /// <summary>
        /// �����ӿڳߴ�����Ӧ�¼���ͼ
        /// </summary>
        public void UpdateViewportForEventView(bool isEventViewVisible)
        {
            VerticalViewportSize = isEventViewVisible ? ViewportHeight * 0.75 : ViewportHeight;
            ValidateAndClampScrollOffsets();
        }

        /// <summary>
        /// ��֤�����ƹ���ƫ��������Ч��Χ��
        /// </summary>
        public void ValidateAndClampScrollOffsets()
        {
            // ��ֱ������Χ��0 �����������ֵ
            if (VerticalScrollOffset < 0)
            {
                VerticalScrollOffset = 0;
            }

            // ˮƽ������Χ��0 �� MaxScrollExtent - ViewportWidth
            // ����Ҫȷ�����ٿ��Թ��������ݵ�ĩβ
            var maxHorizontalScroll = Math.Max(0, MaxScrollExtent - ViewportWidth);
            if (CurrentScrollOffset > maxHorizontalScroll)
            {
                CurrentScrollOffset = maxHorizontalScroll;
            }
            else if (CurrentScrollOffset < 0)
            {
                CurrentScrollOffset = 0;
            }
        }

        /// <summary>
        /// ������������Χ
        /// </summary>
        public void UpdateMaxScrollExtent(double contentWidth)
        {
            ContentWidth = contentWidth;
            RecalculateScrollExtent();
        }

        /// <summary>
        /// ���¼��������Χ
        /// </summary>
        private void RecalculateScrollExtent()
        {
            // ������ΧӦ���ܹ���ȫ������������
            // ��������Χ = ���ݿ��ȣ��������Թ��������һ��Ԫ��
            // ����һ��С�Ļ�������ȷ���û�����
            var bufferWidth = ViewportWidth * 0.1; // 10%���ӿڿ�����Ϊ����
            MaxScrollExtent = Math.Max(ContentWidth + bufferWidth, ViewportWidth);
            
            // ȷ����ǰ����ƫ�����������µ����Χ
            ValidateAndClampScrollOffsets();
            
            // �������Ը���֪ͨ
            OnPropertyChanged(nameof(MaxScrollExtent));
        }

        /// <summary>
        /// ��ȡ��Ч�Ĵ�ֱ�������ֵ
        /// </summary>
        public double GetEffectiveVerticalScrollMax(double totalHeight)
        {
            // ʹ��ʵ�ʵĴ�ֱ���ӿڳߴ����㻬�����
            // ȷ���������ܹ������������ݵ����в����һ��
            return Math.Max(0, totalHeight - VerticalViewportSize);
        }

        /// <summary>
        /// ���ô�ֱ����ƫ��������Լ����
        /// </summary>
        public void SetVerticalScrollOffset(double offset, double totalHeight)
        {
            var maxOffset = GetEffectiveVerticalScrollMax(totalHeight);
            VerticalScrollOffset = Math.Max(0, Math.Min(offset, maxOffset));
        }

        /// <summary>
        /// ����ˮƽ����ƫ��������Լ����
        /// </summary>
        public void SetHorizontalScrollOffset(double offset)
        {
            var maxOffset = Math.Max(0, MaxScrollExtent - ViewportWidth);
            CurrentScrollOffset = Math.Max(0, Math.Min(offset, maxOffset));
        }

        /// <summary>
        /// ��ȡ�ɹ�����ˮƽ��Χ
        /// </summary>
        public double GetHorizontalScrollableRange()
        {
            return Math.Max(0, MaxScrollExtent - ViewportWidth);
        }

        /// <summary>
        /// ��ȡ��ǰ����λ�õİٷֱ� (0-1)
        /// </summary>
        public double GetScrollPercentage()
        {
            var scrollableRange = GetHorizontalScrollableRange();
            return scrollableRange > 0 ? CurrentScrollOffset / scrollableRange : 0;
        }

        /// <summary>
        /// ���ݰٷֱ����ù���λ��
        /// </summary>
        public void SetScrollByPercentage(double percentage)
        {
            percentage = Math.Max(0, Math.Min(1, percentage));
            var scrollableRange = GetHorizontalScrollableRange();
            SetHorizontalScrollOffset(scrollableRange * percentage);
        }
        #endregion

        #region ��������
        /// <summary>
        /// ʵ����Ⱦ�߶� - �����¼���ͼռ�õĿռ�
        /// </summary>
        public double GetActualRenderHeight(bool isEventViewVisible)
        {
            return isEventViewVisible ? ViewportHeight * 0.75 : ViewportHeight;
        }

        /// <summary>
        /// ��Ч������Χ
        /// </summary>
        public double GetEffectiveScrollableHeight(double totalHeight, bool isEventViewVisible)
        {
            var renderHeight = GetActualRenderHeight(isEventViewVisible);
            return Math.Max(0, totalHeight - renderHeight);
        }
        #endregion

        #region ���Ա������
        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewportWidth):
                case nameof(ViewportHeight):
                    RecalculateScrollExtent();
                    ValidateAndClampScrollOffsets();
                    break;
                case nameof(ContentWidth):
                    RecalculateScrollExtent();
                    break;
            }
        }
        #endregion
    }
}