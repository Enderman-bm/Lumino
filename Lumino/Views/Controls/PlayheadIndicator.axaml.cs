using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using EnderDebugger;

namespace Lumino.Views.Controls
{
    /// <summary>
    /// 演奏指示线控件 - 显示实时播放位置
    /// 支持拖拽调整播放位置、样式自定义等
    /// </summary>
    public partial class PlayheadIndicator : UserControl
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private bool _isDragging = false;

        #region 依赖属性
        /// <summary>
        /// 指示线X坐标
        /// </summary>
        public static readonly StyledProperty<double> PlayheadXProperty =
            AvaloniaProperty.Register<PlayheadIndicator, double>(nameof(PlayheadX), 0.0);

        public double PlayheadX
        {
            get => GetValue(PlayheadXProperty);
            set => SetValue(PlayheadXProperty, value);
        }

        /// <summary>
        /// 指示线颜色
        /// </summary>
        public static readonly StyledProperty<string> ColorProperty =
            AvaloniaProperty.Register<PlayheadIndicator, string>(nameof(Color), "#00FF00");

        public string Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// 拖拽完成事件
        /// </summary>
        public event EventHandler<PlayheadDragEventArgs>? PlayheadDragged;

        #endregion

        public PlayheadIndicator()
        {
            InitializeComponent();

            // 注册属性变化处理
            PlayheadXProperty.Changed.AddClassHandler<PlayheadIndicator>((x, e) => x.OnPlayheadXChanged(e));

            // 输入事件
            this.AddHandler(PointerPressedEvent, OnPointerPressedHandler, handledEventsToo: true);
            this.AddHandler(PointerMovedEvent, OnPointerMovedHandler, handledEventsToo: true);
            this.AddHandler(PointerReleasedEvent, OnPointerReleasedHandler, handledEventsToo: true);
        }

        /// <summary>
        /// PlayheadX属性变化处理
        /// </summary>
        private void OnPlayheadXChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is double x)
            {
                this.Margin = new Thickness(x, 0, 0, 0);
            }
        }

        /// <summary>
        /// 指针按下 - 开始拖拽
        /// </summary>
        private void OnPointerPressedHandler(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                this.Cursor = new Cursor(StandardCursorType.Hand);
                _logger.Debug("PlayheadIndicator", "开始拖拽演奏指示线");
            }
        }

        /// <summary>
        /// 指针移动
        /// </summary>
        private void OnPointerMovedHandler(object? sender, PointerEventArgs e)
        {
            if (_isDragging && this.Parent is Panel panel)
            {
                var position = e.GetPosition(panel);
                double newX = Math.Max(0, Math.Min(position.X, panel.Bounds.Width));

                // 更新X坐标
                PlayheadX = newX;

                // 触发拖拽事件
                PlayheadDragged?.Invoke(this, new PlayheadDragEventArgs { NewX = newX });
            }
        }

        /// <summary>
        /// 指针释放 - 停止拖拽
        /// </summary>
        private void OnPointerReleasedHandler(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.Cursor = new Cursor(StandardCursorType.Arrow);
                _logger.Debug("PlayheadIndicator", "停止拖拽演奏指示线");
            }
        }
    }

    /// <summary>
    /// 演奏指示线拖拽事件参数
    /// </summary>
    public class PlayheadDragEventArgs : EventArgs
    {
        public double NewX { get; set; }
    }
}
