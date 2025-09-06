/*using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Performance;

namespace DominoNext.Views.Rendering.Performance
{
    /// <summary>
    /// 虚拟化渲染器 - 专门处理大量音符的高效渲染
    /// 实现空间分区、层级细节等高级渲染技术
    /// </summary>
    public class VirtualizedRenderer
    {
        private readonly RenderObjectPool _objectPool = RenderObjectPool.Instance;
        private readonly BatchNoteRenderer _batchRenderer = new();
        private readonly GpuTextureRenderer _textureRenderer = new();
        
        // 空间分区网格
        private readonly Dictionary<(int, int), List<NoteViewModel>> _spatialGrid = new();
        private const int GridCellSize = 128; // 每个网格单元的像素大小
        
        // 层级细节阈值
        private const double LowDetailThreshold = 0.3;
        private const double MediumDetailThreshold = 0.7;
        private const int MaxNotesForFullDetail = 2000;
        private const int MaxNotesForMediumDetail = 5000;

        /// <summary>
        /// 高性能虚拟化渲染
        /// </summary>
        public void VirtualizedRender(DrawingContext context, PianoRollViewModel viewModel, Rect viewport)
        {
            if (viewModel?.CurrentTrackNotes == null) return;

            var totalNotes = viewModel.CurrentTrackNotes.Count;
            var zoom = viewModel.Zoom;
            
            // 根据音符数量和缩放级别选择渲染策略
            if (totalNotes > MaxNotesForMediumDetail && zoom < LowDetailThreshold)
            {
                RenderUltraLowDetail(context, viewModel, viewport);
            }
            else if (totalNotes > MaxNotesForFullDetail && zoom < MediumDetailThreshold)
            {
                RenderLowDetail(context, viewModel, viewport);
            }
            else if (totalNotes > MaxNotesForFullDetail)
            {
                RenderMediumDetail(context, viewModel, viewport);
            }
            else
            {
                RenderFullDetail(context, viewModel, viewport);
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"虚拟化渲染: {totalNotes} 音符, 缩放: {zoom:F2}, 视口: {viewport}");
#endif
        }

        /// <summary>
        /// 超低细节渲染 - 用于极大量音符和极低缩放
        /// </summary>
        private void RenderUltraLowDetail(DrawingContext context, PianoRollViewModel viewModel, Rect viewport)
        {
            var visibleNotes = GetVisibleNotesOptimized(viewModel, viewport);
            
            // 使用密度图渲染
            RenderDensityMap(context, visibleNotes, viewModel, viewport);
        }

        /// <summary>
        /// 低细节渲染 - 简化的矩形渲染
        /// </summary>
        private void RenderLowDetail(DrawingContext context, PianoRollViewModel viewModel, Rect viewport)
        {
            var visibleNotes = GetVisibleNotesOptimized(viewModel, viewport);
            var brush = _objectPool.GetSolidBrush(Color.Parse("#4CAF50"), 0.7);
            
            foreach (var note in visibleNotes)
            {
                var rect = CalculateNoteRect(note, viewModel);
                if (rect.Width > 0.5 && rect.Height > 0.5)
                {
                    context.DrawRectangle(brush, null, rect);
                }
            }
        }

        /// <summary>
        /// 中等细节渲染 - 带选择状态的渲染
        /// </summary>
        private void RenderMediumDetail(DrawingContext context, PianoRollViewModel viewModel, Rect viewport)
        {
            var visibleNotes = GetVisibleNotesOptimized(viewModel, viewport);
            var normalBrush = _objectPool.GetSolidBrush(Color.Parse("#4CAF50"), 0.8);
            var selectedBrush = _objectPool.GetSolidBrush(Color.Parse("#FF9800"), 0.8);
            var pen = _objectPool.GetPen(Color.Parse("#2E7D32"), 1);
            
            foreach (var note in visibleNotes)
            {
                var rect = CalculateNoteRect(note, viewModel);
                if (rect.Width > 0.5 && rect.Height > 0.5)
                {
                    var brush = note.IsSelected ? selectedBrush : normalBrush;
                    context.DrawRectangle(brush, pen, rect);
                }
            }
        }

        /// <summary>
        /// 完整细节渲染 - 使用批处理渲染器
        /// </summary>
        private void RenderFullDetail(DrawingContext context, PianoRollViewModel viewModel, Rect viewport)
        {
            var visibleNotes = GetVisibleNotesOptimized(viewModel, viewport);
            var noteData = visibleNotes.Select(note => (note, CalculateNoteRect(note, viewModel)));
            
            _batchRenderer.BatchRenderNotes(context, noteData, viewModel);
        }

        /// <summary>
        /// 密度图渲染 - 用于极大量音符
        /// </summary>
        private void RenderDensityMap(DrawingContext context, IEnumerable<NoteViewModel> notes, 
                                    PianoRollViewModel viewModel, Rect viewport)
        {
            var densityGrid = new Dictionary<(int, int), int>();
            var cellSize = 8; // 密度网格单元大小
            
            // 统计每个网格单元的音符密度
            foreach (var note in notes)
            {
                var rect = CalculateNoteRect(note, viewModel);
                var gridX = (int)(rect.X / cellSize);
                var gridY = (int)(rect.Y / cellSize);
                var key = (gridX, gridY);
                
                densityGrid[key] = densityGrid.GetValueOrDefault(key, 0) + 1;
            }
            
            // 渲染密度图
            var maxDensity = densityGrid.Values.Max();
            foreach (var kvp in densityGrid)
            {
                var (gridX, gridY) = kvp.Key;
                var density = kvp.Value;
                var opacity = Math.Min(1.0, (double)density / maxDensity);
                
                var brush = _objectPool.GetSolidBrush(Color.Parse("#4CAF50"), opacity);
                var rect = new Rect(gridX * cellSize, gridY * cellSize, cellSize, cellSize);
                
                context.DrawRectangle(brush, null, rect);
            }
        }

        /// <summary>
        /// 优化的可见音符获取 - 使用空间分区
        /// </summary>
        private IEnumerable<NoteViewModel> GetVisibleNotesOptimized(PianoRollViewModel viewModel, Rect viewport)
        {
            if (viewModel.CurrentTrackNotes == null) return Enumerable.Empty<NoteViewModel>();
            
            // 更新空间分区网格
            UpdateSpatialGrid(viewModel);
            
            // 计算视口覆盖的网格单元
            var startGridX = (int)((viewport.X - viewModel.CurrentScrollOffset) / GridCellSize);
            var endGridX = (int)((viewport.Right - viewModel.CurrentScrollOffset) / GridCellSize) + 1;
            var startGridY = (int)((viewport.Y - viewModel.VerticalScrollOffset) / GridCellSize);
            var endGridY = (int)((viewport.Bottom - viewModel.VerticalScrollOffset) / GridCellSize) + 1;
            
            var visibleNotes = new HashSet<NoteViewModel>();
            
            // 收集覆盖网格单元中的音符
            for (int x = startGridX; x <= endGridX; x++)
            {
                for (int y = startGridY; y <= endGridY; y++)
                {
                    if (_spatialGrid.TryGetValue((x, y), out var cellNotes))
                    {
                        foreach (var note in cellNotes)
                        {
                            visibleNotes.Add(note);
                        }
                    }
                }
            }
            
            // 精确的视口裁剪
            var expandedViewport = viewport.Inflate(50);
            return visibleNotes.Where(note =>
            {
                var rect = CalculateNoteRect(note, viewModel);
                return rect.Intersects(expandedViewport);
            });
        }

        /// <summary>
        /// 更新空间分区网格
        /// </summary>
        private void UpdateSpatialGrid(PianoRollViewModel viewModel)
        {
            _spatialGrid.Clear();
            
            if (viewModel.CurrentTrackNotes == null) return;
            
            foreach (var note in viewModel.CurrentTrackNotes)
            {
                var rect = CalculateNoteRect(note, viewModel);
                var gridX = (int)(rect.X / GridCellSize);
                var gridY = (int)(rect.Y / GridCellSize);
                var key = (gridX, gridY);
                
                if (!_spatialGrid.TryGetValue(key, out var cellNotes))
                {
                    cellNotes = new List<NoteViewModel>();
                    _spatialGrid[key] = cellNotes;
                }
                
                cellNotes.Add(note);
            }
        }

        /// <summary>
        /// 计算音符矩形
        /// </summary>
        private Rect CalculateNoteRect(NoteViewModel note, PianoRollViewModel viewModel)
        {
            var absoluteX = note.GetX(viewModel.TimeToPixelScale);
            var absoluteY = note.GetY(viewModel.KeyHeight);
            var width = Math.Max(4, note.GetWidth(viewModel.TimeToPixelScale));
            var height = Math.Max(2, note.GetHeight(viewModel.KeyHeight) - 1);

            var x = absoluteX - viewModel.CurrentScrollOffset;
            var y = absoluteY - viewModel.VerticalScrollOffset;

            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// 获取渲染统计信息
        /// </summary>
        public VirtualizedRenderStats GetRenderStats()
        {
            var textureStats = _textureRenderer.GetCacheStats();
            return new VirtualizedRenderStats
            {
                SpatialGridCells = _spatialGrid.Count,
                TextureCacheHits = textureStats.hits,
                TextureCacheMisses = textureStats.misses,
                TextureCacheHitRate = textureStats.hitRate
            };
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            _spatialGrid.Clear();
            _textureRenderer.CleanupTextureCache();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _textureRenderer?.Dispose();
            _spatialGrid.Clear();
        }
    }

    /// <summary>
    /// 虚拟化渲染统计信息
    /// </summary>
    public struct VirtualizedRenderStats
    {
        public int SpatialGridCells { get; set; }
        public int TextureCacheHits { get; set; }
        public int TextureCacheMisses { get; set; }
        public double TextureCacheHitRate { get; set; }
    }
}*/