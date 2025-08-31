using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Views.Controls.Editing.Rendering
{
    /// <summary>
    /// 音符渲染器
    /// </summary>
    public class NoteRenderer
    {
        private readonly Color _noteColor = Color.Parse("#4CAF50");
        private readonly IPen _noteBorderPen = new Pen(new SolidColorBrush(Color.Parse("#2E7D32")), 2);
        private readonly Color _selectedNoteColor = Color.Parse("#FF9800");
        private readonly IPen _selectedNoteBorderPen = new Pen(new SolidColorBrush(Color.Parse("#F57C00")), 2);
        private readonly Color _previewNoteColor = Color.Parse("#81C784");
        private readonly IPen _previewNoteBorderPen = new Pen(new SolidColorBrush(Color.Parse("#66BB6A")), 2);

        /// <summary>
        /// 渲染所有音符
        /// </summary>
        public void RenderNotes(DrawingContext context, PianoRollViewModel viewModel, Dictionary<NoteViewModel, Rect> visibleNoteCache)
        {
            int drawnNotes = 0;
            var processedNotes = new HashSet<NoteViewModel>();
            
            foreach (var kvp in visibleNoteCache)
            {
                var note = kvp.Key;
                var cachedRect = kvp.Value;

                // 修复：拖拽或调整大小时不渲染原始位置的音符，避免留下影子
                bool isBeingDragged = (viewModel.DragState.IsDragging && viewModel.DragState.DraggingNotes.Contains(note));
                bool isBeingResized = (viewModel.ResizeState.IsResizing && viewModel.ResizeState.ResizingNotes.Contains(note));
                
                // 如果音符正在被拖拽或调整大小，跳过渲染原始位置，并标记为已处理
                if (isBeingDragged || isBeingResized)
                {
                    processedNotes.Add(note);
                    continue;
                }

                // 对于未被操作的音符，使用缓存的矩形位置
                if (cachedRect.Width > 0 && cachedRect.Height > 0)
                {
                    DrawNote(context, note, cachedRect, false);
                    drawnNotes++;
                    processedNotes.Add(note);
                }
            }

            // 处理正在被拖拽的音符（实时计算位置，避免重复渲染）
            if (viewModel.DragState.IsDragging)
            {
                foreach (var note in viewModel.DragState.DraggingNotes)
                {
                    if (!processedNotes.Contains(note)) // 避免重复渲染
                    {
                        // 实时计算被拖拽音符的新位置并渲染
                        var currentRect = CalculateNoteRect(note, viewModel);
                        if (currentRect.Width > 0 && currentRect.Height > 0)
                        {
                            DrawNote(context, note, currentRect, true);
                            drawnNotes++;
                        }
                    }
                }
            }

            // 处理正在被调整大小的音符（实时计算位置，避免重复渲染）
            if (viewModel.ResizeState.IsResizing)
            {
                foreach (var note in viewModel.ResizeState.ResizingNotes)
                {
                    if (!processedNotes.Contains(note)) // 避免重复渲染
                    {
                        // 实时计算被调整大小音符的新位置并渲染
                        var currentRect = CalculateNoteRect(note, viewModel);
                        if (currentRect.Width > 0 && currentRect.Height > 0)
                        {
                            DrawNote(context, note, currentRect, true);
                            drawnNotes++;
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"Rendered {drawnNotes} visible notes");
        }

        /// <summary>
        /// 实时计算音符矩形位置
        /// </summary>
        private Rect CalculateNoteRect(NoteViewModel note, PianoRollViewModel viewModel)
        {
            var x = note.GetX(viewModel.Zoom, viewModel.PixelsPerTick);
            var y = note.GetY(viewModel.KeyHeight);
            var width = Math.Max(4, note.GetWidth(viewModel.Zoom, viewModel.PixelsPerTick));
            var height = Math.Max(2, note.GetHeight(viewModel.KeyHeight) - 1);

            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// 渲染预览音符
        /// </summary>
        public void RenderPreviewNote(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            if (viewModel.PreviewNote == null) return;

            var previewRect = calculateNoteRect(viewModel.PreviewNote);
            if (previewRect.Width > 0 && previewRect.Height > 0)
            {
                var brush = new SolidColorBrush(_previewNoteColor, 0.6);
                context.DrawRectangle(brush, _previewNoteBorderPen, previewRect);

                var durationText = viewModel.PreviewNote.Duration.ToString();
                if (previewRect.Width > 30 && previewRect.Height > 10)
                {
                    DrawNoteText(context, durationText, previewRect, 9);
                }
            }
        }

        /// <summary>
        /// 绘制单个音符
        /// </summary>
        private void DrawNote(DrawingContext context, NoteViewModel note, Rect rect, bool isBeingManipulated = false)
        {
            var opacity = Math.Max(0.7, note.Velocity / 127.0);
            
            // 正在操作时使用更高的透明度，让音符更加可见
            if (isBeingManipulated)
            {
                opacity = Math.Min(1.0, opacity * 1.1); // 提高了被拖拽音符的透明度，增强视觉反馈
            }

            IBrush brush;
            IPen pen;

            if (note.IsSelected)
            {
                // 选中音符使用更醒目的颜色
                brush = new SolidColorBrush(_selectedNoteColor, opacity);
                pen = _selectedNoteBorderPen;
            }
            else
            {
                brush = new SolidColorBrush(_noteColor, opacity);
                pen = _noteBorderPen;
            }

            // 为拖拽中的音符添加轻微阴影效果，增强视觉反馈
            if (isBeingManipulated)
            {
                var shadowOffset = new Vector(1, 1);
                var shadowRect = rect.Translate(shadowOffset);
                var shadowBrush = new SolidColorBrush(Colors.Black, 0.2);
                context.DrawRectangle(shadowBrush, null, shadowRect);
            }

            context.DrawRectangle(brush, pen, rect);
        }

        /// <summary>
        /// 在音符上绘制文本
        /// </summary>
        private void DrawNoteText(DrawingContext context, string text, Rect noteRect, double fontSize)
        {
            var typeface = new Typeface(FontFamily.Default);
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black);

            var textPosition = new Point(
                noteRect.X + (noteRect.Width - formattedText.Width) / 2,
                noteRect.Y + (noteRect.Height - formattedText.Height) / 2);

            var textBounds = new Rect(
                textPosition.X - 2,
                textPosition.Y - 1,
                formattedText.Width + 4,
                formattedText.Height + 2);
            context.DrawRectangle(new SolidColorBrush(Colors.White, 0.8), null, textBounds);

            context.DrawText(formattedText, textPosition);
        }
    }
}