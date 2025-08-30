using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Editor.Modules;
using System;
using System.Linq;

namespace DominoNext.Renderers
{
    /// <summary>
    /// 项目用途：
    /// 本文件用于在 MVVM 架构下的视图层渲染音符力度条。
    /// VelocityBarRenderer 负责将 ViewModel（如 NoteViewModel、VelocityEditingModule）中的数据以图形方式呈现到 UI 上，
    /// 实现力度条的可视化、编辑预览等功能，提升用户交互体验。
    /// 在 MVVM 中，渲染器负责将 ViewModel 的数据转换为界面元素，保持数据与界面逻辑的分离。
    /// </summary>
    public class VelocityBarRenderer
    {
        private const double BAR_MARGIN = 1.0;
        private const double MIN_BAR_WIDTH = 2.0;

        /// <summary>
        /// 获取资源画刷，优先从应用资源查找，找不到则使用备用颜色。
        /// 在 MVVM 中用于根据主题或样式动态渲染界面。
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

        /// <summary>
        /// 获取资源画笔，优先从应用资源查找，找不到则使用备用颜色。
        /// 在 MVVM 中用于根据主题或样式动态渲染界面。
        /// </summary>
        private IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            return new Pen(brush, thickness);
        }

        /// <summary>
        /// 绘制单个音符的力度条。
        /// 根据 NoteViewModel 的数据，将力度条渲染到指定画布区域。
        /// 在 MVVM 中用于将 ViewModel 的音符数据可视化到界面。
        /// </summary>
        public void DrawVelocityBar(DrawingContext context, NoteViewModel note, Rect canvasBounds,
            double zoom, double pixelsPerTick, VelocityRenderType renderType = VelocityRenderType.Normal)
        {
            // 计算音符在时间轴上的位置和宽度
            var noteX = note.GetX(zoom, pixelsPerTick);
            var noteWidth = note.GetWidth(zoom, pixelsPerTick);
            
            // 确保力度条在画布范围内
            if (noteX + noteWidth < 0 || noteX > canvasBounds.Width) return;
            
            // 计算力度条的尺寸
            var barWidth = Math.Max(MIN_BAR_WIDTH, noteWidth - BAR_MARGIN * 2);
            var barHeight = CalculateBarHeight(note.Velocity, canvasBounds.Height);
            
            var barX = noteX + BAR_MARGIN;
            var barY = canvasBounds.Height - barHeight;
            
            var barRect = new Rect(barX, barY, barWidth, barHeight);

            // 根据渲染类型获取样式
            var (brush, pen) = GetStyleForRenderType(renderType, note.Velocity);

            // 绘制力度条
            context.DrawRectangle(brush, pen, barRect);

            // 如果力度条足够宽，绘制力度值
            if (barWidth > 30 && renderType == VelocityRenderType.Selected)
            {
                DrawVelocityValue(context, barRect, note.Velocity);
            }
        }

        /// <summary>
        /// 绘制力度编辑预览。
        /// 根据 VelocityEditingModule 的编辑路径和当前编辑位置，渲染预览效果。
        /// 在 MVVM 中用于将编辑模块的状态以图形方式反馈到界面。
        /// </summary>
        public void DrawEditingPreview(DrawingContext context, Rect canvasBounds, 
            VelocityEditingModule editingModule, double zoom, double pixelsPerTick)
        {
            if (editingModule.EditingPath?.Any() != true) return;

            var previewBrush = GetResourceBrush("VelocityEditingPreviewBrush", "#80FF5722");
            var previewPen = new Pen(previewBrush, 2, new DashStyle(new double[] { 3, 3 }, 0));

            // 绘制连续的编辑路径
            var points = editingModule.EditingPath.Select(p => new Point(p.X, p.Y)).ToArray();
            if (points.Length > 1)
            {
                // 使用几何路径来绘制更平滑的线条
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(points[0], false);
                    
                    // 如果点很多，使用贝塞尔曲线平滑
                    if (points.Length > 3)
                    {
                        for (int i = 1; i < points.Length - 2; i += 2)
                        {
                            var controlPoint1 = points[i];
                            var controlPoint2 = i + 1 < points.Length ? points[i + 1] : points[i];
                            var endPoint = i + 2 < points.Length ? points[i + 2] : points[^1];
                            
                            ctx.CubicBezierTo(controlPoint1, controlPoint2, endPoint);
                        }
                    }
                    else
                    {
                        // 点较少时直接连线
                        for (int i = 1; i < points.Length; i++)
                        {
                            ctx.LineTo(points[i]);
                        }
                    }
                }
                
                context.DrawGeometry(null, previewPen, geometry);
            }

            // 绘制当前编辑位置的力度条预览
            if (editingModule.CurrentEditPosition.HasValue)
            {
                var pos = editingModule.CurrentEditPosition.Value;
                var velocity = CalculateVelocityFromY(pos.Y, canvasBounds.Height);
                
                var previewHeight = CalculateBarHeight(velocity, canvasBounds.Height);
                var previewRect = new Rect(pos.X - 8, canvasBounds.Height - previewHeight, 16, previewHeight);
                
                // 使用稍微透明的背景
                var previewBarBrush = CreateBrushWithOpacity(previewBrush, 0.7);
                context.DrawRectangle(previewBarBrush, previewPen, previewRect);
                
                // 显示当前力度值
                DrawVelocityValue(context, previewRect, velocity, true);
            }

            // 在路径的关键点绘制小圆点，显示实际的采样点
            if (points.Length > 0)
            {
                var dotBrush = CreateBrushWithOpacity(previewBrush, 0.8);
                var dotSize = 3.0;
                
                // 只绘制部分点以避免过于密集
                var step = Math.Max(1, points.Length / 20); // 最多显示20个点
                for (int i = 0; i < points.Length; i += step)
                {
                    var point = points[i];
                    var dotRect = new Rect(point.X - dotSize/2, point.Y - dotSize/2, dotSize, dotSize);
                    context.DrawEllipse(dotBrush, null, dotRect);
                }
            }
        }

        /// <summary>
        /// 根据渲染类型和力度值获取力度条的画刷和画笔样式。
        /// 在 MVVM 中用于根据不同状态（选中、编辑等）动态渲染界面。
        /// </summary>
        private (IBrush brush, IPen pen) GetStyleForRenderType(VelocityRenderType renderType, int velocity)
        {
            var opacity = CalculateOpacity(velocity);

            return renderType switch
            {
                VelocityRenderType.Selected => (
                    CreateBrushWithOpacity(GetResourceBrush("VelocitySelectedBrush", "#FFFF9800"), opacity),
                    GetResourcePen("VelocitySelectedPenBrush", "#FFF57C00", 1)
                ),
                VelocityRenderType.Editing => (
                    CreateBrushWithOpacity(GetResourceBrush("VelocityEditingBrush", "#FFFF5722"), opacity),
                    GetResourcePen("VelocityEditingPenBrush", "#FFD84315", 2)
                ),
                VelocityRenderType.Dragging => (
                    CreateBrushWithOpacity(GetResourceBrush("VelocityDraggingBrush", "#FF2196F3"), opacity),
                    GetResourcePen("VelocityDraggingPenBrush", "#FF1976D2", 1)
                ),
                _ => ( // Normal
                    CreateBrushWithOpacity(GetResourceBrush("VelocityBrush", "#FF4CAF50"), opacity),
                    GetResourcePen("VelocityPenBrush", "#FF2E7D32", 1)
                )
            };
        }

        /// <summary>
        /// 根据力度值计算透明度。
        /// 在 MVVM 中用于根据数据动态调整界面元素的可见性。
        /// </summary>
        private double CalculateOpacity(int velocity)
        {
            // 基于力度值计算透明度，确保可见性
            return Math.Max(0.4, velocity / 127.0);
        }

        /// <summary>
        /// 根据力度值和最大高度计算力度条的高度。
        /// 在 MVVM 中用于将数据映射为界面元素尺寸。
        /// </summary>
        private double CalculateBarHeight(int velocity, double maxHeight)
        {
            // 将MIDI力度值(0-127)映射到条形高度
            var normalizedVelocity = Math.Max(0, Math.Min(127, velocity)) / 127.0;
            return normalizedVelocity * maxHeight;
        }

        /// <summary>
        /// 从 Y 坐标反推力度值。
        /// 公开静态方法，供其他类根据界面坐标计算力度。
        /// 在 MVVM 中用于将用户操作的界面坐标转换为数据。
        /// </summary>
        public static int CalculateVelocityFromY(double y, double maxHeight)
        {
            // 从Y坐标反推力度值 - 公开此方法供其他类使用
            var normalizedY = Math.Max(0, Math.Min(1, (maxHeight - y) / maxHeight));
            var velocity = Math.Max(1, Math.Min(127, (int)Math.Round(normalizedY * 127)));
            
            return velocity;
        }

        /// <summary>
        /// 创建指定透明度的画刷。
        /// 在 MVVM 中用于根据数据动态调整界面元素的样式。
        /// </summary>
        private IBrush CreateBrushWithOpacity(IBrush originalBrush, double opacity)
        {
            if (originalBrush is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                return new SolidColorBrush(color, opacity);
            }
            return originalBrush;
        }

        /// <summary>
        /// 在力度条上绘制力度值文本。
        /// 在 MVVM 中用于将数据以文本形式显示在界面。
        /// </summary>
        private void DrawVelocityValue(DrawingContext context, Rect barRect, int velocity, bool isPreview = false)
        {
            var textBrush = GetResourceBrush("VelocityTextBrush", "#FFFFFFFF");
            var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
            
            var formattedText = new FormattedText(
                velocity.ToString(),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                isPreview ? 12 : 10, // 预览时稍大一些
                textBrush);

            var textX = barRect.X + (barRect.Width - formattedText.Width) / 2;
            var textY = isPreview ? barRect.Y - 15 : barRect.Y + 2; // 预览时显示在条形上方

            context.DrawText(formattedText, new Point(textX, textY));
        }
    }

    /// <summary>
    /// 力度条渲染类型枚举。
    /// 用于区分不同的渲染状态（普通、选中、编辑、拖拽）。
    /// 在 MVVM 中用于根据 ViewModel 状态切换界面样式。
    /// </summary>
    public enum VelocityRenderType
    {
        Normal,
        Selected,
        Editing,
        Dragging
    }
}