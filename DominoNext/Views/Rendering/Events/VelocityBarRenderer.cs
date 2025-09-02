using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Editor.Modules;
using DominoNext.Views.Rendering.Common;
using System;
using System.Linq;

namespace DominoNext.Views.Rendering.Events
{
    /// <summary>
    /// 力度条渲染器 - 负责绘制音符力度条
    /// </summary>
    public class VelocityBarRenderer
    {
        private const double BAR_MARGIN = 1.0;
        private const double MIN_BAR_WIDTH = 2.0;

        // 鼠标曲线渲染器实例
        private readonly MouseCurveRenderer _curveRenderer = new MouseCurveRenderer();

        // 资源画刷获取助手方法
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

        private Pen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            return new Pen(brush, thickness);
        }

        public void DrawVelocityBar(DrawingContext context, NoteViewModel note, Rect canvasBounds,
            double timeToPixelScale, VelocityRenderType renderType = VelocityRenderType.Normal,
            double scrollOffset = 0)
        {
            // 计算音符在时间轴上的位置和宽度（绝对坐标）
            var absoluteNoteX = note.GetX(timeToPixelScale);
            var noteWidth = note.GetWidth(timeToPixelScale);
            
            // 应用滚动偏移量得到屏幕坐标
            var noteX = absoluteNoteX - scrollOffset;
            
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

        public void DrawEditingPreview(DrawingContext context, Rect canvasBounds, 
            VelocityEditingModule editingModule, double timeToPixelScale, double scrollOffset = 0)
        {
            if (editingModule.EditingPath?.Any() != true) return;

            // 使用鼠标曲线渲染器绘制编辑轨迹
            var curveStyle = _curveRenderer.CreateEditingPreviewStyle();
            _curveRenderer.DrawMouseTrail(context, editingModule.EditingPath, canvasBounds, scrollOffset, curveStyle);

            // 绘制当前编辑位置的力度条预览（这部分保留在力度渲染器中，因为它是力度特定的逻辑）
            if (editingModule.CurrentEditPosition.HasValue)
            {
                DrawCurrentEditPositionPreview(context, editingModule.CurrentEditPosition.Value, 
                    canvasBounds, scrollOffset, curveStyle.Brush);
            }
        }

        /// <summary>
        /// 绘制当前编辑位置的力度条预览
        /// </summary>
        private void DrawCurrentEditPositionPreview(DrawingContext context, Point worldPosition, 
            Rect canvasBounds, double scrollOffset, IBrush previewBrush)
        {
            var screenPos = new Point(worldPosition.X - scrollOffset, worldPosition.Y);
            
            // 只在屏幕范围内绘制预览
            if (screenPos.X < -20 || screenPos.X > canvasBounds.Width + 20) return;

            var velocity = CalculateVelocityFromY(screenPos.Y, canvasBounds.Height);
            var previewHeight = CalculateBarHeight(velocity, canvasBounds.Height);
            var previewRect = new Rect(screenPos.X - 8, canvasBounds.Height - previewHeight, 16, previewHeight);
            
            // 使用稍微透明的背景
            var previewBarBrush = CreateBrushWithOpacity(previewBrush, 0.7);
            var previewPen = new Pen(previewBrush, 2, new DashStyle(new double[] { 3, 3 }, 0));
            
            context.DrawRectangle(previewBarBrush, previewPen, previewRect);
            
            // 显示当前力度值
            DrawVelocityValue(context, previewRect, velocity, true);
        }

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

        private double CalculateOpacity(int velocity)
        {
            // 基于力度值计算透明度，确保可见性
            return Math.Max(0.4, velocity / 127.0);
        }

        private double CalculateBarHeight(int velocity, double maxHeight)
        {
            // 将MIDI力度值(0-127)映射到条形高度
            var normalizedVelocity = Math.Max(0, Math.Min(127, velocity)) / 127.0;
            return normalizedVelocity * maxHeight;
        }

        public static int CalculateVelocityFromY(double y, double maxHeight)
        {
            // 从Y坐标反推力度值 - 公开此方法供其他类使用
            var normalizedY = Math.Max(0, Math.Min(1, (maxHeight - y) / maxHeight));
            var velocity = Math.Max(1, Math.Min(127, (int)Math.Round(normalizedY * 127)));
            
            return velocity;
        }

        private IBrush CreateBrushWithOpacity(IBrush originalBrush, double opacity)
        {
            if (originalBrush is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                return new SolidColorBrush(color, opacity);
            }
            return originalBrush;
        }

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
    /// 力度条渲染类型
    /// </summary>
    public enum VelocityRenderType
    {
        Normal,
        Selected,
        Editing,
        Dragging
    }
}