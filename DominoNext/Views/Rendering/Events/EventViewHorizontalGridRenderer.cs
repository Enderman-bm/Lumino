using System;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Utils;

namespace DominoNext.Views.Rendering.Events
{
    /// <summary>
    /// 事件视图水平网格线渲染器 - 绘制事件视图的水平分割线
    /// 将事件视图高度分为4等份，在1/4、1/2、3/4处绘制横线
    /// 优化策略：内部缓存计算结果，总是执行绘制以确保稳定性
    /// </summary>
    public class EventViewHorizontalGridRenderer
    {
        // 缓存上次渲染的参数，用于优化性能
        private double _lastBoundsHeight = double.NaN;
        private double _lastBoundsWidth = double.NaN;

        // 缓存计算结果
        private double[] _cachedHorizontalLinePositions = new double[3];
        private bool _cacheValid = false;

        // 使用动态画笔获取，确保与主题状态同步
        private IPen HorizontalLinePen => RenderingUtils.GetResourcePen("GridLineBrush", "#FFBAD2F2", 1);

        /// <summary>
        /// 渲染事件视图水平网格线（稳定版本 - 总是绘制，内部优化计算）
        /// </summary>
        public void RenderEventViewHorizontalGrid(DrawingContext context, PianoRollViewModel viewModel, Rect bounds)
        {
            // 检查是否需要重新计算横线位置
            bool needsRecalculation = !_cacheValid ||
                !AreEqual(_lastBoundsHeight, bounds.Height) ||
                !AreEqual(_lastBoundsWidth, bounds.Width);

            double[] horizontalLinePositions;

            if (needsRecalculation)
            {
                // 重新计算横线位置
                var quarterHeight = bounds.Height / 4.0;
                
                for (int i = 0; i < 3; i++)
                {
                    _cachedHorizontalLinePositions[i] = (i + 1) * quarterHeight;
                }

                // 更新缓存
                _lastBoundsHeight = bounds.Height;
                _lastBoundsWidth = bounds.Width;
                _cacheValid = true;
                
                horizontalLinePositions = _cachedHorizontalLinePositions;
            }
            else
            {
                // 使用缓存值
                horizontalLinePositions = _cachedHorizontalLinePositions;
            }

            // 总是执行绘制，确保显示稳定
            // 使用动态获取的画笔，确保与主题同步
            var pen = HorizontalLinePen;
            
            for (int i = 0; i < 3; i++)
            {
                var y = horizontalLinePositions[i];
                context.DrawLine(pen,
                    new Point(0, y), 
                    new Point(bounds.Width, y));
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
    }
}