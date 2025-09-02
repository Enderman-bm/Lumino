using System;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Views.Rendering.Grids
{
    /// <summary>
    /// 水平网格线渲染器 - 负责绘制钢琴键区域和分割线
    /// 支持不同的钢琴模式（128键或256键切换）
    /// 优化策略：内部缓存计算结果，但总是执行绘制以确保稳定性
    /// </summary>
    public class HorizontalGridRenderer
    {
        // 缓存上次渲染的参数，用于优化计算
        private double _lastVerticalScrollOffset = double.NaN;
        private double _lastVerticalZoom = double.NaN;
        private double _lastKeyHeight = double.NaN;
        private double _lastBoundsWidth = double.NaN;

        // 缓存计算结果
        private int _cachedVisibleStartKey;
        private int _cachedVisibleEndKey;
        private bool _cacheValid = false;

        /// <summary>
        /// 渲染水平网格线（稳定版本 - 总是绘制，内部优化计算）
        /// </summary>
        public void RenderHorizontalGrid(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, double verticalScrollOffset)
        {
            var keyHeight = viewModel.KeyHeight;
            var verticalZoom = viewModel.VerticalZoom;

            // 检查是否需要重新计算可见范围
            bool needsRecalculation = !_cacheValid ||
                !AreEqual(_lastVerticalScrollOffset, verticalScrollOffset) ||
                !AreEqual(_lastVerticalZoom, verticalZoom) ||
                !AreEqual(_lastKeyHeight, keyHeight) ||
                !AreEqual(_lastBoundsWidth, bounds.Width);

            int visibleStartKey, visibleEndKey;

            if (needsRecalculation)
            {
                // 重新计算可见的键范围
                visibleStartKey = (int)(verticalScrollOffset / keyHeight);
                visibleEndKey = (int)((verticalScrollOffset + bounds.Height) / keyHeight) + 1;

                visibleStartKey = Math.Max(0, visibleStartKey);
                visibleEndKey = Math.Min(128, visibleEndKey); // 默认128键，可扩展为配置项

                // 更新缓存
                _cachedVisibleStartKey = visibleStartKey;
                _cachedVisibleEndKey = visibleEndKey;
                _lastVerticalScrollOffset = verticalScrollOffset;
                _lastVerticalZoom = verticalZoom;
                _lastKeyHeight = keyHeight;
                _lastBoundsWidth = bounds.Width;
                _cacheValid = true;
            }
            else
            {
                // 使用缓存的值
                visibleStartKey = _cachedVisibleStartKey;
                visibleEndKey = _cachedVisibleEndKey;
            }

            // 总是执行绘制，确保显示稳定
            for (int i = visibleStartKey; i < visibleEndKey; i++)
            {
                var midiNote = 127 - i; // MIDI音符号
                var y = i * keyHeight - verticalScrollOffset;
                
                RenderKeyRow(context, bounds, midiNote, y, keyHeight, viewModel);
                RenderKeyDivider(context, bounds, midiNote, y, keyHeight);
            }
        }

        /// <summary>
        /// 比较两个double值是否相等（处理浮点精度问题）
        /// </summary>
        private static bool AreEqual(double a, double b, double tolerance = 1e-10)
        {
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            if (double.IsNaN(a) || double.IsNaN(b)) return false;
            return Math.Abs(a - b) < tolerance;
        }

        /// <summary>
        /// 渲染单个键行背景
        /// </summary>
        private void RenderKeyRow(DrawingContext context, Rect bounds, int midiNote, double y, double keyHeight, PianoRollViewModel viewModel)
        {
            var isBlackKey = viewModel.IsBlackKey(midiNote);
            var rowRect = new Rect(0, y, bounds.Width, keyHeight);
            
            var rowBrush = isBlackKey ? GetBlackKeyRowBrush(viewModel) : GetWhiteKeyRowBrush(viewModel);
            context.DrawRectangle(rowBrush, null, rowRect);
        }

        /// <summary>
        /// 渲染键分割线
        /// </summary>
        private void RenderKeyDivider(DrawingContext context, Rect bounds, int midiNote, double y, double keyHeight)
        {
            // 判断是否是八度分界线（B和C之间）
            var isOctaveBoundary = midiNote % 12 == 0;

            // 绘制水平分界线
            var pen = isOctaveBoundary 
                ? GetOctaveBoundaryPen()
                : GetKeyDividerPen();
            
            context.DrawLine(pen, new Point(0, y + keyHeight), new Point(bounds.Width, y + keyHeight));
        }

        /// <summary>
        /// 获取白键行背景画刷
        /// </summary>
        private IBrush GetWhiteKeyRowBrush(PianoRollViewModel viewModel)
        {
            return GetResourceBrush("KeyWhiteBrush", "#FFFFFFFF");
        }

        /// <summary>
        /// 获取优化的黑键行背景画刷
        /// </summary>
        private IBrush GetBlackKeyRowBrush(PianoRollViewModel viewModel)
        {
            // 根据主背景色的亮度动态调整黑键行的颜色
            var mainBg = GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");
            
            if (mainBg is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                var brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255.0;
                
                if (brightness < 0.5) // 深色主题
                {
                    // 深色主题：使黑键行稍微亮一些
                    return new SolidColorBrush(Color.FromArgb(
                        255,
                        (byte)Math.Min(255, color.R + 15),
                        (byte)Math.Min(255, color.G + 15),
                        (byte)Math.Min(255, color.B + 15)
                    ));
                }
                else // 浅色主题
                {
                    // 浅色主题：使黑键行稍微暗一些
                    return new SolidColorBrush(Color.FromArgb(
                        255,
                        (byte)Math.Max(0, color.R - 25),
                        (byte)Math.Max(0, color.G - 25),
                        (byte)Math.Max(0, color.B - 25)
                    ));
                }
            }
            
            // 回退到预设颜色
            return GetResourceBrush("AppBackgroundBrush", "#FFedf3fe");
        }

        /// <summary>
        /// 获取八度分界线画笔
        /// </summary>
        private Pen GetOctaveBoundaryPen()
        {
            var brush = GetResourceBrush("BorderLineBlackBrush", "#FF000000");
            return new Pen(brush, 1.5);
        }

        /// <summary>
        /// 获取普通键分割线画笔
        /// </summary>
        private Pen GetKeyDividerPen()
        {
            var brush = GetResourceBrush("GridLineBrush", "#FFbad2f2");
            return new Pen(brush, 0.5);
        }

        /// <summary>
        /// 资源画刷获取助手方法
        /// </summary>
        private IBrush GetResourceBrush(string key, string fallbackHex)
        {
            try
            {
                if (Application.Current?.Resources.TryGetResource(key, null, out var obj) == true && obj is IBrush brush)
                    return brush;
            }
            catch { }

            try
            {
                return new SolidColorBrush(Color.Parse(fallbackHex));
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
    }
}