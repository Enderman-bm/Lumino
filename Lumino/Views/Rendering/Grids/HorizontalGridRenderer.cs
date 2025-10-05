using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Adapters;

namespace Lumino.Views.Rendering.Grids
{
    /// <summary>
    /// 水平网格线渲染器 - 性能优化版本，支持画笔复用
    /// 支持不同键盘数模式（128键、256键切换）
    /// 优化性能：内部缓存计算结果，仅执行切换触发确保稳定性
    /// </summary>
    public class HorizontalGridRenderer
    {
        // 缓存上次渲染的参数，用于优化性能
        private double _lastVerticalScrollOffset = double.NaN;
        private double _lastVerticalZoom = double.NaN;
        private double _lastKeyHeight = double.NaN;
        private double _lastBoundsWidth = double.NaN;

        // 计算结果缓存
        private int _cachedVisibleStartKey;
        private int _cachedVisibleEndKey;
        private bool _cacheValid = false;

        // 画笔缓存 - 复用Pen对象
        private Pen? _cachedOctaveBoundaryPen;
        private Pen? _cachedKeyDividerPen;
        private readonly Dictionary<bool, IBrush> _keyRowBrushCache = new(); // 黑白键背景画笔缓存

        /// <summary>
        /// 获取缓存的八度分界线画笔
        /// </summary>
        private Pen GetOctaveBoundaryPen()
        {
            return _cachedOctaveBoundaryPen ??= new Pen(RenderingUtils.GetResourceBrush("BorderLineBlackBrush", "#FF000000"), 1.5);
        }

        /// <summary>
        /// 获取缓存的键分隔线画笔
        /// </summary>
        private Pen GetKeyDividerPen()
        {
            return _cachedKeyDividerPen ??= new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", "#FFbad2f2"), 0.5);
        }

        /// <summary>
        /// 渲染水平网格线，稳定版本 - 性能优化（内部优化计算）
        /// </summary>
        public void RenderHorizontalGrid(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, double verticalScrollOffset)
        {
            RenderHorizontalGrid(context, null, viewModel, bounds, verticalScrollOffset);
        }

        /// <summary>
        /// 渲染水平网格线，支持Vulkan适配器
        /// </summary>
        public void RenderHorizontalGrid(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Rect bounds, double verticalScrollOffset)
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
                // 重新计算可见的键盘范围
                visibleStartKey = (int)(verticalScrollOffset / keyHeight);
                visibleEndKey = (int)((verticalScrollOffset + bounds.Height) / keyHeight) + 1;

                visibleStartKey = Math.Max(0, visibleStartKey);
                visibleEndKey = Math.Min(128, visibleEndKey); // 默认128键，后续扩展为配置项

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
                // 使用缓存值
                visibleStartKey = _cachedVisibleStartKey;
                visibleEndKey = _cachedVisibleEndKey;
            }

            // 稳定执行绘制，确保显示稳定
            for (int i = visibleStartKey; i < visibleEndKey; i++)
            {
                var midiNote = 127 - i; // MIDI音符号
                var y = i * keyHeight - verticalScrollOffset;
                
                RenderKeyRow(context, bounds, midiNote, y, keyHeight, viewModel);
                RenderKeyDivider(context, bounds, midiNote, y, keyHeight);
            }
        }

        /// <summary>
        /// 比较两个double值是否相等（处理精度问题）
        /// </summary>
        private static bool AreEqual(double a, double b, double tolerance = 1e-10)
        {
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            if (double.IsNaN(a) || double.IsNaN(b)) return false;
            return Math.Abs(a - b) < tolerance;
        }

        /// <summary>
        /// 渲染键背景
        /// </summary>
        private void RenderKeyRow(DrawingContext context, Rect bounds, int midiNote, double y, double keyHeight, PianoRollViewModel viewModel)
        {
            var isBlackKey = viewModel.IsBlackKey(midiNote);
            var rowRect = new Rect(0, y, bounds.Width, keyHeight);
            
            var rowBrush = GetCachedKeyRowBrush(isBlackKey, viewModel);
            context.DrawRectangle(rowBrush, null, rowRect);
        }

        /// <summary>
        /// 渲染键分隔线
        /// </summary>
        private void RenderKeyDivider(DrawingContext context, Rect bounds, int midiNote, double y, double keyHeight)
        {
            // 判断是否是八度分界线（B到C之间）
            var isOctaveBoundary = midiNote % 12 == 0;

            // 绘制水平分界线 - 使用缓存的画笔
            var pen = isOctaveBoundary ? GetOctaveBoundaryPen() : GetKeyDividerPen();
            
            context.DrawLine(pen, new Point(0, y + keyHeight), new Point(bounds.Width, y + keyHeight));
        }

        /// <summary>
        /// 获取缓存的键背景画笔
        /// </summary>
        private IBrush GetCachedKeyRowBrush(bool isBlackKey, PianoRollViewModel viewModel)
        {
            if (!_keyRowBrushCache.TryGetValue(isBlackKey, out var brush))
            {
                brush = isBlackKey ? CreateBlackKeyRowBrush(viewModel) : GetWhiteKeyRowBrush(viewModel);
                _keyRowBrushCache[isBlackKey] = brush;
            }
            return brush;
        }

        /// <summary>
        /// 获取白键背景画笔
        /// </summary>
        private IBrush GetWhiteKeyRowBrush(PianoRollViewModel viewModel)
        {
            return RenderingUtils.GetResourceBrush("KeyWhiteBrush", "#FFFFFFFF");
        }

        /// <summary>
        /// 创建黑键背景画笔（动态计算）
        /// </summary>
        private IBrush CreateBlackKeyRowBrush(PianoRollViewModel viewModel)
        {
            // 基于主背景色动态计算，黑键行的颜色
            var mainBg = RenderingUtils.GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");
            
            if (mainBg is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                var brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255.0;
                
                if (brightness < 0.5) // 深色主题
                {
                    // 深色主题：使键背景略微亮一些
                    return new SolidColorBrush(Color.FromArgb(
                        255,
                        (byte)Math.Min(255, color.R + 15),
                        (byte)Math.Min(255, color.G + 15),
                        (byte)Math.Min(255, color.B + 15)
                    ));
                }
                else // 浅色主题
                {
                    // 浅色主题：使键背景略微暗一些
                    return new SolidColorBrush(Color.FromArgb(
                        255,
                        (byte)Math.Max(0, color.R - 25),
                        (byte)Math.Max(0, color.G - 25),
                        (byte)Math.Max(0, color.B - 25)
                    ));
                }
            }
            
            // 默认的预备颜色
            return RenderingUtils.GetResourceBrush("AppBackgroundBrush", "#FFedf3fe");
        }

        /// <summary>
        /// 清除缓存（主题变更时调用）
        /// </summary>
        public void ClearCache()
        {
            _cacheValid = false;
            _cachedOctaveBoundaryPen = null;
            _cachedKeyDividerPen = null;
            _keyRowBrushCache.Clear();
        }
    }
}