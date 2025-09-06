/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Views.Rendering.Performance
{
    /// <summary>
    /// 高性能批处理渲染器 - 将相同样式的音符批量绘制
    /// </summary>
    public class BatchNoteRenderer
    {
        private readonly RenderObjectPool _objectPool = RenderObjectPool.Instance;
        
        // 预定义的渲染样式
        private readonly struct RenderStyle
        {
            public readonly Color Color;
            public readonly double Opacity;
            public readonly bool HasBorder;
            public readonly Color BorderColor;
            public readonly double BorderThickness;

            public RenderStyle(Color color, double opacity, bool hasBorder = true, 
                             Color borderColor = default, double borderThickness = 1.0)
            {
                Color = color;
                Opacity = opacity;
                HasBorder = hasBorder;
                BorderColor = borderColor == default ? Color.FromRgb(0, 0, 0) : borderColor;
                BorderThickness = borderThickness;
            }

            public override bool Equals(object? obj)
            {
                return obj is RenderStyle style &&
                       Color.Equals(style.Color) &&
                       Math.Abs(Opacity - style.Opacity) < 0.001 &&
                       HasBorder == style.HasBorder &&
                       BorderColor.Equals(style.BorderColor) &&
                       Math.Abs(BorderThickness - style.BorderThickness) < 0.001;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Color, Opacity, HasBorder, BorderColor, BorderThickness);
            }
        }

        // 渲染批次数据
        private readonly struct RenderBatch
        {
            public readonly RenderStyle Style;
            public readonly List<Rect> Rects;

            public RenderBatch(RenderStyle style, List<Rect> rects)
            {
                Style = style;
                Rects = rects;
            }
        }

        /// <summary>
        /// 批量渲染音符
        /// </summary>
        public void BatchRenderNotes(DrawingContext context, IEnumerable<(NoteViewModel note, Rect rect)> noteData, 
                                    PianoRollViewModel viewModel)
        {
            // 按样式分组音符
            var batches = GroupNotesByStyle(noteData, viewModel);
            
            // 批量渲染每个样式组
            foreach (var batch in batches)
            {
                RenderBatchInternal(context, batch);
            }

            // 返回临时列表到对象池
            foreach (var batch in batches)
            {
                _objectPool.ReturnRectList(batch.Rects);
            }
        }

        /// <summary>
        /// 异步准备渲染数据（在后台线程中执行）
        /// </summary>
        public async Task<List<(NoteViewModel note, Rect rect)>> PrepareRenderDataAsync(
            IEnumerable<NoteViewModel> notes, 
            PianoRollViewModel viewModel,
            Rect viewport,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new List<(NoteViewModel, Rect)>();
                var expandedViewport = viewport.Inflate(50); // 稍微扩展视口以提供缓冲

                foreach (var note in notes)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var rect = CalculateNoteRect(note, viewModel);
                    if (rect.Intersects(expandedViewport) && rect.Width > 0.5 && rect.Height > 0.5)
                    {
                        result.Add((note, rect));
                    }
                }

                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// 按渲染样式分组音符
        /// </summary>
        private List<RenderBatch> GroupNotesByStyle(IEnumerable<(NoteViewModel note, Rect rect)> noteData, 
                                                   PianoRollViewModel viewModel)
        {
            var styleGroups = new Dictionary<RenderStyle, List<Rect>>();

            foreach (var (note, rect) in noteData)
            {
                var style = GetRenderStyle(note, viewModel);
                
                if (!styleGroups.TryGetValue(style, out var rects))
                {
                    rects = _objectPool.GetRectList();
                    styleGroups[style] = rects;
                }
                
                rects.Add(rect);
            }

            return styleGroups.Select(kvp => new RenderBatch(kvp.Key, kvp.Value)).ToList();
        }

        /// <summary>
        /// 获取音符的渲染样式
        /// </summary>
        private RenderStyle GetRenderStyle(NoteViewModel note, PianoRollViewModel viewModel)
        {
            var opacity = Math.Max(0.7, note.Velocity / 127.0);
            
            // 检查是否正在被操作
            bool isBeingManipulated = 
                (viewModel.DragState.IsDragging && viewModel.DragState.DraggingNotes.Contains(note)) ||
                (viewModel.ResizeState.IsResizing && viewModel.ResizeState.ResizingNotes.Contains(note));

            if (isBeingManipulated)
            {
                opacity = Math.Min(1.0, opacity * 1.1);
            }

            if (note.IsSelected)
            {
                return new RenderStyle(
                    Color.Parse("#FF9800"), 
                    opacity, 
                    true, 
                    Color.Parse("#F57C00"), 
                    2.0);
            }
            else
            {
                return new RenderStyle(
                    Color.Parse("#4CAF50"), 
                    opacity, 
                    true, 
                    Color.Parse("#2E7D32"), 
                    2.0);
            }
        }

        /// <summary>
        /// 渲染单个批次（重命名避免重复定义）
        /// </summary>
        private void RenderBatchInternal(DrawingContext context, RenderBatch batch)
        {
            if (batch.Rects.Count == 0) return;

            var brush = _objectPool.GetSolidBrush(batch.Style.Color, batch.Style.Opacity);
            var pen = batch.Style.HasBorder 
                ? _objectPool.GetPen(batch.Style.BorderColor, batch.Style.BorderThickness)
                : null;

            // 批量绘制矩形
            foreach (var rect in batch.Rects)
            {
                context.DrawRectangle(brush, pen, rect);
            }
        }

        /// <summary>
        /// 计算音符矩形（优化版本）
        /// </summary>
        private Rect CalculateNoteRect(NoteViewModel note, PianoRollViewModel viewModel)
        {
            // 使用缓存的计算结果
            var absoluteX = note.GetX(viewModel.TimeToPixelScale);
            var absoluteY = note.GetY(viewModel.KeyHeight);
            var width = Math.Max(4, note.GetWidth(viewModel.TimeToPixelScale));
            var height = Math.Max(2, note.GetHeight(viewModel.KeyHeight) - 1);

            // 应用滚动偏移量
            var x = absoluteX - viewModel.CurrentScrollOffset;
            var y = absoluteY - viewModel.VerticalScrollOffset;

            return new Rect(x, y, width, height);
        }
    }
}*/