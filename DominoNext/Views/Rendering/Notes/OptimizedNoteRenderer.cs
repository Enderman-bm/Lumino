/*using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Performance;

namespace DominoNext.Views.Rendering.Notes
{
    /// <summary>
    /// 高性能音符渲染器 - 优化大量音符渲染
    /// </summary>
    public class OptimizedNoteRenderer
    {
        private readonly RenderObjectPool _objectPool = RenderObjectPool.Instance;
        private readonly BatchNoteRenderer _batchRenderer = new();

        // 预缓存的画刷和画笔
        private readonly SolidColorBrush _normalNoteBrush;
        private readonly SolidColorBrush _selectedNoteBrush;
        private readonly SolidColorBrush _previewNoteBrush;
        private readonly Pen _normalNotePen;
        private readonly Pen _selectedNotePen;
        private readonly Pen _previewNotePen;

        // 渲染统计
        private int _lastRenderedCount = 0;

        public OptimizedNoteRenderer()
        {
            // 预缓存常用的画刷和画笔
            _normalNoteBrush = _objectPool.GetSolidBrush(Color.Parse("#4CAF50"));
            _selectedNoteBrush = _objectPool.GetSolidBrush(Color.Parse("#FF9800"));
            _previewNoteBrush = _objectPool.GetSolidBrush(Color.Parse("#81C784"), 0.6);
            
            _normalNotePen = _objectPool.GetPen(Color.Parse("#2E7D32"), 2);
            _selectedNotePen = _objectPool.GetPen(Color.Parse("#F57C00"), 2);
            _previewNotePen = _objectPool.GetPen(Color.Parse("#66BB6A"), 2);
        }

        /// <summary>
        /// 高性能渲染所有音符
        /// </summary>
        public void RenderNotes(DrawingContext context, PianoRollViewModel viewModel, 
                               Dictionary<NoteViewModel, Rect> visibleNoteCache)
        {
            if (visibleNoteCache == null || visibleNoteCache.Count == 0)
                return;

            var noteData = visibleNoteCache
                .Where(kvp => kvp.Value.Width > 0 && kvp.Value.Height > 0)
                .Select(kvp => (kvp.Key, kvp.Value));

            // 使用批处理渲染器
            _batchRenderer.BatchRenderNotes(context, noteData, viewModel);

            _lastRenderedCount = visibleNoteCache.Count;
            
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"OptimizedNoteRenderer: 渲染了 {_lastRenderedCount} 个音符");
#endif
        }

        /// <summary>
        /// 渲染预览音符（优化版本）
        /// </summary>
        public void RenderPreviewNote(DrawingContext context, PianoRollViewModel viewModel, 
                                     Func<NoteViewModel, Rect> calculateNoteRect)
        {
            if (viewModel.PreviewNote == null) 
                return;

            var previewRect = calculateNoteRect(viewModel.PreviewNote);
            if (previewRect.Width <= 0 || previewRect.Height <= 0)
                return;

            // 使用预缓存的画刷和画笔
            context.DrawRectangle(_previewNoteBrush, _previewNotePen, previewRect);

            // 只在足够大的音符上显示文本
            if (previewRect.Width > 30 && previewRect.Height > 10)
            {
                var durationText = viewModel.PreviewNote.Duration.ToString();
                DrawOptimizedText(context, durationText, previewRect);
            }
        }

        /// <summary>
        /// 优化的文本绘制
        /// </summary>
        private void DrawOptimizedText(DrawingContext context, string text, Rect rect)
        {
            // 简化的文本渲染，避免复杂的字体度量计算
            var textBrush = _objectPool.GetSolidBrush(Colors.White);
            var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
            
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                9,
                textBrush);

            var textOrigin = new Point(
                rect.X + (rect.Width - formattedText.Width) / 2,
                rect.Y + (rect.Height - formattedText.Height) / 2);

            context.DrawText(formattedText, textOrigin);
        }

        /// <summary>
        /// 获取渲染统计信息
        /// </summary>
        public int GetLastRenderedCount() => _lastRenderedCount;

        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            _lastRenderedCount = 0;
        }
    }
}*/