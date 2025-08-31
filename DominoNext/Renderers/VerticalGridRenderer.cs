using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using System;

namespace DominoNext.Renderers
{
    /// <summary>
    /// 纵向网格渲染器 - 负责绘制小节线、节拍线和细分线
    /// 根据歌曲拍号动态调整网格布局
    /// </summary>
    public class VerticalGridRenderer
    {
        /// <summary>
        /// 渲染纵向网格线
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="viewModel">钢琴卷帘ViewModel</param>
        /// <param name="bounds">绘制边界</param>
        /// <param name="viewport">可视区域</param>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, Rect viewport)
        {
            if (viewModel == null) return;

            var measureWidth = viewModel.MeasureWidth;
            var beatWidth = viewModel.BeatWidth;
            var subdivisionLevel = viewModel.SubdivisionLevel;
            var startX = viewport.X;
            var endX = viewport.X + viewport.Width;
            var startY = viewport.Y;
            var endY = Math.Min(viewport.Y + viewport.Height, 128 * viewModel.KeyHeight);

            // 获取画笔
            var beatLinePen = GetResourcePen("GridLineBrush", "#1F000000", 0.8);
            var measureLinePen = GetResourcePen("MeasureLineBrush", "#FF000080", 1.2);
            var subdivisionPen = GetResourcePen("GridLineBrush", "#1F000000", 0.4);

            // 绘制细分网格线
            RenderSubdivisionLines(context, viewModel, startX, endX, startY, endY, beatWidth, subdivisionLevel, subdivisionPen);

            // 绘制节拍线
            RenderBeatLines(context, viewModel, startX, endX, startY, endY, beatWidth, subdivisionLevel, beatLinePen);

            // 绘制小节线
            RenderMeasureLines(context, viewModel, startX, endX, startY, endY, measureWidth, measureLinePen);
        }

        /// <summary>
        /// 绘制细分网格线
        /// </summary>
        private void RenderSubdivisionLines(DrawingContext context, PianoRollViewModel viewModel, 
            double startX, double endX, double startY, double endY, 
            double beatWidth, int subdivisionLevel, IPen subdivisionPen)
        {
            // 计算细分间隔
            var subdivisionCount = subdivisionLevel / viewModel.BeatsPerMeasure;
            var subdivisionWidth = beatWidth / subdivisionCount;

            // 仅在细分级别大于4且细分线不会太密集时显示
            if (subdivisionLevel > 4 && subdivisionWidth > 2)
            {
                var startSubdivision = Math.Max(0, (int)(startX / subdivisionWidth));
                var endSubdivision = (int)(endX / subdivisionWidth) + 1;

                for (int i = startSubdivision; i <= endSubdivision; i++)
                {
                    // 跳过小节线和节拍线的位置
                    if (i % subdivisionCount == 0) continue;
                    
                    var x = i * subdivisionWidth;
                    if (x >= startX && x <= endX)
                    {
                        context.DrawLine(subdivisionPen, new Point(x, startY), new Point(x, endY));
                    }
                }
            }
        }

        /// <summary>
        /// 绘制节拍线
        /// </summary>
        private void RenderBeatLines(DrawingContext context, PianoRollViewModel viewModel,
            double startX, double endX, double startY, double endY,
            double beatWidth, int subdivisionLevel, IPen beatLinePen)
        {
            // 当细分级别为4时显示节拍线
            if (subdivisionLevel == 4)
            {
                var startBeat = Math.Max(0, (int)(startX / beatWidth));
                var endBeat = (int)(endX / beatWidth) + 1;

                for (int i = startBeat; i <= endBeat; i++)
                {
                    // 跳过小节线的位置
                    if (i % viewModel.BeatsPerMeasure == 0) continue;
                    
                    var x = i * beatWidth;
                    if (x >= startX && x <= endX)
                    {
                        context.DrawLine(beatLinePen, new Point(x, startY), new Point(x, endY));
                    }
                }
            }
        }

        /// <summary>
        /// 绘制小节线
        /// </summary>
        private void RenderMeasureLines(DrawingContext context, PianoRollViewModel viewModel,
            double startX, double endX, double startY, double endY,
            double measureWidth, IPen measureLinePen)
        {
            var startMeasure = Math.Max(0, (int)(startX / measureWidth));
            var endMeasure = (int)(endX / measureWidth) + 1;

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var x = i * measureWidth;
                if (x >= startX && x <= endX)
                {
                    context.DrawLine(measureLinePen, new Point(x, startY), new Point(x, endY));
                }
            }
        }

        private IBrush GetResourceBrush(string key, string fallbackHex)
        {
            try
            {
                if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var obj) == true && obj is IBrush brush)
                    return brush;
            }
            catch { }

            return new SolidColorBrush(Color.Parse(fallbackHex));
        }

        private IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1, DashStyle? dashStyle = null)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            var pen = new Pen(brush, thickness);
            if (dashStyle != null)
                pen.DashStyle = dashStyle;
            return pen;
        }
    }
}