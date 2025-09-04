using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DominoNext.ViewModels.Editor.Components
{
    /// <summary>
    /// 钢琴卷帘视口管理组件 - 负责滚动、视口尺寸等
    /// 符合单一职责原则，专注于视口和滚动相关的状态管理
    /// </summary>
    public partial class PianoRollViewport : ObservableObject
    {
        #region 滚动相关属性
        [ObservableProperty] private double _currentScrollOffset = 0.0;
        [ObservableProperty] private double _verticalScrollOffset = 0.0;
        [ObservableProperty] private double _timelinePosition;
        #endregion

        #region 视口尺寸
        [ObservableProperty] private double _viewportWidth = 800.0;
        [ObservableProperty] private double _viewportHeight = 400.0;
        [ObservableProperty] private double _verticalViewportSize = 400.0;
        [ObservableProperty] private double _maxScrollExtent = 5000.0;
        #endregion

        #region 构造函数
        public PianoRollViewport()
        {
            PropertyChanged += OnPropertyChanged;
        }
        #endregion

        #region 滚动管理方法
        /// <summary>
        /// 设置视口尺寸
        /// </summary>
        public void SetViewportSize(double width, double height)
        {
            ViewportWidth = width;
            ViewportHeight = height;
            VerticalViewportSize = height;
            
            // 确保当前滚动位置在有效范围内
            ValidateAndClampScrollOffsets();
        }

        /// <summary>
        /// 更新视口尺寸以适应事件视图
        /// </summary>
        public void UpdateViewportForEventView(bool isEventViewVisible)
        {
            VerticalViewportSize = isEventViewVisible ? ViewportHeight * 0.75 : ViewportHeight;
            ValidateAndClampScrollOffsets();
        }

        /// <summary>
        /// 验证并限制滚动偏移量在有效范围内
        /// </summary>
        public void ValidateAndClampScrollOffsets()
        {
            // 垂直滚动范围：0 到合理的最大值
            if (VerticalScrollOffset < 0)
            {
                VerticalScrollOffset = 0;
            }

            // 水平滚动范围：0 到 MaxScrollExtent - ViewportWidth
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
        /// 更新最大滚动范围
        /// </summary>
        public void UpdateMaxScrollExtent(double contentWidth)
        {
            // 至少要能滚动到内容的末尾
            MaxScrollExtent = Math.Max(contentWidth, ViewportWidth * 2);
            
            // 确保当前滚动偏移量不超过最大范围
            ValidateAndClampScrollOffsets();
        }

        /// <summary>
        /// 获取有效的垂直滚动最大值
        /// </summary>
        public double GetEffectiveVerticalScrollMax(double totalHeight)
        {
            return Math.Max(0, totalHeight - VerticalViewportSize);
        }

        /// <summary>
        /// 设置垂直滚动偏移量（带约束）
        /// </summary>
        public void SetVerticalScrollOffset(double offset, double totalHeight)
        {
            var maxOffset = GetEffectiveVerticalScrollMax(totalHeight);
            VerticalScrollOffset = Math.Max(0, Math.Min(offset, maxOffset));
        }

        /// <summary>
        /// 设置水平滚动偏移量（带约束）
        /// </summary>
        public void SetHorizontalScrollOffset(double offset)
        {
            var maxOffset = Math.Max(0, MaxScrollExtent - ViewportWidth);
            CurrentScrollOffset = Math.Max(0, Math.Min(offset, maxOffset));
        }
        #endregion

        #region 计算属性
        /// <summary>
        /// 实际渲染高度 - 考虑事件视图占用的空间
        /// </summary>
        public double GetActualRenderHeight(bool isEventViewVisible)
        {
            return isEventViewVisible ? ViewportHeight * 0.75 : ViewportHeight;
        }

        /// <summary>
        /// 有效滚动范围
        /// </summary>
        public double GetEffectiveScrollableHeight(double totalHeight, bool isEventViewVisible)
        {
            var renderHeight = GetActualRenderHeight(isEventViewVisible);
            return Math.Max(0, totalHeight - renderHeight);
        }
        #endregion

        #region 属性变更处理
        private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewportWidth):
                case nameof(ViewportHeight):
                    ValidateAndClampScrollOffsets();
                    break;
            }
        }
        #endregion
    }
}