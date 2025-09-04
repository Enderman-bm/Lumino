using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.ViewModels.Editor.Modules;
using DominoNext.Views.Rendering.Tools;
using DominoNext.Views.Rendering.Utils;
using System;
using System.Linq;

namespace DominoNext.Views.Rendering.Events
{
    /// <summary>
    /// 力度条渲染器 - 绘制音符的力度条形图
    /// </summary>
    public class VelocityBarRenderer
    {
        private const double BAR_MARGIN = 1.0;
        private const double MIN_BAR_WIDTH = 2.0;

        // 鼠标曲线渲染器实例
        private readonly MouseCurveRenderer _curveRenderer = new MouseCurveRenderer();

        public void DrawVelocityBar(DrawingContext context, NoteViewModel note, Rect canvasBounds,
            double timeToPixelScale, VelocityRenderType renderType = VelocityRenderType.Normal,
            double scrollOffset = 0)
        {
            // 计算音符在时间轴上的位置和宽度（世界坐标）
            var absoluteNoteX = note.GetX(timeToPixelScale);
            var noteWidth = note.GetWidth(timeToPixelScale);
            
            // 应用滚动偏移得到画面坐标
            var noteX = absoluteNoteX - scrollOffset;
            
            // 确保音符在画布范围内
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

            // 如果条形足够大，绘制力度值
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

            // 绘制当前编辑位置的力度条预览（外部会调用其他渲染器处理，因为这涉及特定的业务逻辑）
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
            var previewBarBrush = RenderingUtils.CreateBrushWithOpacity(previewBrush, 0.7);
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
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocitySelectedBrush", "#FFFF9800"), opacity),
                    RenderingUtils.GetResourcePen("VelocitySelectedPenBrush", "#FFF57C00", 1)
                ),
                VelocityRenderType.Editing => (
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocityEditingBrush", "#FFFF5722"), opacity),
                    RenderingUtils.GetResourcePen("VelocityEditingPenBrush", "#FFD84315", 2)
                ),
                VelocityRenderType.Dragging => (
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocityDraggingBrush", "#FF2196F3"), opacity),
                    RenderingUtils.GetResourcePen("VelocityDraggingPenBrush", "#FF1976D2", 1)
                ),
                _ => ( // Normal
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocityBrush", "#FF4CAF50"), opacity),
                    RenderingUtils.GetResourcePen("VelocityPenBrush", "#FF2E7D32", 1)
                )
            };
        }

        private double CalculateOpacity(int velocity)
        {
            // 根据力度值计算透明度，确保可见性
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
            // 从Y坐标反算力度值 - 这是个反向计算，其他地方使用
            var normalizedY = Math.Max(0, Math.Min(1, (maxHeight - y) / maxHeight));
            var velocity = Math.Max(1, Math.Min(127, (int)Math.Round(normalizedY * 127)));
            
            return velocity;
        }

        private void DrawVelocityValue(DrawingContext context, Rect barRect, int velocity, bool isPreview = false)
        {
            var textBrush = RenderingUtils.GetResourceBrush("VelocityTextBrush", "#FFFFFFFF");
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