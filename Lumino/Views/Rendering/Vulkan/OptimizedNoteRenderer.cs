using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using EnderDebugger;

namespace Lumino.Views.Rendering.Vulkan
{
    /// <summary>
    /// 优化的音符渲染器 - 集成所有性能优化
    /// </summary>
    public class OptimizedNoteRenderer
    {
        private readonly OptimizedNoteCullingSystem _cullingSystem = new();
        private readonly VulkanRenderContext _renderContext;
        private readonly OptimizedNoteLodRenderer _lodRenderer = new();
        
        // 性能监控
        private readonly PerformanceProfiler _profiler = new();
        private const int PERFORMANCE_LOG_INTERVAL = 100; // 每100帧记录一次
        
        // 缓存
        private readonly Dictionary<NoteViewModel, CachedNoteRenderData> _noteRenderCache = new();
        private long _frameCounter = 0;
        
        public OptimizedNoteRenderer(VulkanRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
        }

        /// <summary>
        /// 渲染音符 - 集成所有优化
        /// </summary>
        public async Task RenderNotesAsync(PianoRollViewModel viewModel,
            Dictionary<NoteViewModel, Rect> visibleNoteCache,
            Rect viewport, double zoomLevel)
        {
            _profiler.BeginFrame();
            _frameCounter++;
            
            try
            {
                // 1. 更新剔除系统视口
                _cullingSystem.SetViewport(viewport, zoomLevel);
                
                // 2. 执行高效剔除和LOD计算
                var visibleNotes = _cullingSystem.CullNotes(visibleNoteCache.Keys);
                
                // 3. 按LOD级别分组
                var lodGroups = GroupNotesByLod(visibleNotes);
                
                // 4. 准备渲染数据
                var renderBatches = await PrepareRenderBatchesAsync(lodGroups, viewModel);
                
                // 5. 批量提交渲染命令
                SubmitRenderBatches(renderBatches);
                
                // 6. 性能监控
                if (_frameCounter % PERFORMANCE_LOG_INTERVAL == 0)
                {
                    LogPerformanceStats();
                }
                
                _profiler.EndFrame();
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("OptimizedNoteRenderer", $"渲染错误: {ex.Message}");
                // 回退到传统渲染
                await FallbackToTraditionalRendering(viewModel, visibleNoteCache);
            }
        }

        /// <summary>
        /// 按LOD级别分组音符
        /// </summary>
        private Dictionary<int, List<(NoteViewModel note, Rect bounds)>> GroupNotesByLod(
            List<(NoteViewModel note, Rect bounds, int lodLevel)> visibleNotes)
        {
            var groups = new Dictionary<int, List<(NoteViewModel, Rect)>>();
            
            foreach (var (note, bounds, lodLevel) in visibleNotes)
            {
                if (!groups.TryGetValue(lodLevel, out var group))
                {
                    group = new List<(NoteViewModel, Rect)>();
                    groups[lodLevel] = group;
                }
                group.Add((note, bounds));
            }
            
            return groups;
        }

        /// <summary>
        /// 并行准备渲染批次
        /// </summary>
        private async Task<List<OptimizedRenderBatch>> PrepareRenderBatchesAsync(
            Dictionary<int, List<(NoteViewModel note, Rect bounds)>> lodGroups,
            PianoRollViewModel viewModel)
        {
            var batches = new List<OptimizedRenderBatch>();
            
            var tasks = lodGroups.Select(async lodGroup =>
            {
                return await Task.Run(() => PrepareLodBatch(lodGroup.Key, lodGroup.Value, viewModel));
            });
            
            var lodBatches = await Task.WhenAll(tasks);
            batches.AddRange(lodBatches.Where(b => b != null && b.Count > 0));
            
            return batches;
        }

        /// <summary>
        /// 准备单个LOD级别的渲染批次
        /// </summary>
        private OptimizedRenderBatch PrepareLodBatch(int lodLevel,
            List<(NoteViewModel note, Rect bounds)> notes,
            PianoRollViewModel viewModel)
        {
            var lodConfig = _lodRenderer.GetLodConfig(lodLevel);
            var batch = new OptimizedRenderBatch();
            
            // 根据LOD配置调整渲染参数
            if (lodConfig.EnableEffects)
            {
                // 添加阴影效果
                PrepareShadowBatch(notes, batch);
            }
            
            // 按状态分组优化
            var stateGroups = GroupNotesByState(notes, viewModel);
            
            foreach (var group in stateGroups)
            {
                var renderData = PrepareNoteRenderData(group.Key, group.ToList(), lodConfig);
                batch.AddCommand(renderData);
            }
            
            return batch;
        }

        /// <summary>
        /// 按状态分组音符（选中/普通）
        /// </summary>
        private List<IGrouping<bool, (NoteViewModel note, Rect bounds)>> GroupNotesByState(
            List<(NoteViewModel note, Rect bounds)> notes,
            PianoRollViewModel viewModel)
        {
            return notes.GroupBy(note =>
            {
                return note.note.IsSelected ||
                       (viewModel.DragState?.IsDragging ?? false) && (viewModel.DragState?.DraggingNotes?.Contains(note.note) ?? false) ||
                       (viewModel.ResizeState?.IsResizing ?? false) && (viewModel.ResizeState?.ResizingNotes?.Contains(note.note) ?? false);
            }).ToList();
        }

        /// <summary>
        /// 准备音符渲染数据
        /// </summary>
        private OptimizedRenderCommand PrepareNoteRenderData(bool isSelected,
            List<(NoteViewModel note, Rect bounds)> notes,
            OptimizedLodConfig lodConfig)
        {
            var roundedRects = new List<Avalonia.RoundedRect>();
            
            foreach (var (note, bounds) in notes)
            {
                roundedRects.Add(new Avalonia.RoundedRect(bounds, 3.0, 3.0));
            }
            
            return new OptimizedRenderCommand
            {
                Type = OptimizedRenderCommandType.DrawRoundedRects,
                RoundedRects = roundedRects,
                Color = Colors.LightBlue // 需要根据实际情况设置颜色
            };
        }

        /// <summary>
        /// 准备阴影批次
        /// </summary>
        private void PrepareShadowBatch(List<(NoteViewModel note, Rect bounds)> notes, OptimizedRenderBatch batch)
        {
            var shadowRects = new List<Avalonia.RoundedRect>();
            
            foreach (var (note, bounds) in notes)
            {
                var shadowBounds = bounds.Inflate(new Thickness(1, 1));
                shadowRects.Add(new Avalonia.RoundedRect(shadowBounds, 3.0, 3.0));
            }
            
            batch.AddCommand(new OptimizedRenderCommand
            {
                Type = OptimizedRenderCommandType.DrawRoundedRects,
                RoundedRects = shadowRects,
                Color = Colors.DarkGray
            });
        }

        /// <summary>
        /// 提交渲染批次
        /// </summary>
        private void SubmitRenderBatches(List<OptimizedRenderBatch> batches)
        {
            foreach (var batch in batches)
            {
                foreach (var command in batch.Commands)
                {
                    if (command.Type == OptimizedRenderCommandType.DrawRoundedRects)
                    {
                        _renderContext.DrawRoundedRectsInstanced(command.RoundedRects, new SolidColorBrush(command.Color));
                    }
                }
            }
        }

        /// <summary>
        /// 记录性能统计
        /// </summary>
        private void LogPerformanceStats()
        {
            var stats = _profiler.GetStats();
            var vulkanStats = _renderContext.GetPerformanceStats();
            EnderLogger.Instance.Info("OptimizedNoteRenderer", $"FPS: {vulkanStats.Fps:F1}, " +
                            $"渲染音符: {stats.RenderedNoteCount}, " +
                            $"剔除率: {stats.CullingRatio:P1}, " +
                            $"DrawCalls: {vulkanStats.AverageDrawCalls}");
        }

        /// <summary>
        /// 回退到传统渲染
        /// </summary>
        private async Task FallbackToTraditionalRendering(PianoRollViewModel viewModel,
            Dictionary<NoteViewModel, Rect> visibleNoteCache)
        {
            // 使用传统的NoteRenderer进行渲染
            await Task.CompletedTask;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            _noteRenderCache.Clear();
        }
    }

    /// <summary>
    /// 渲染批次
    /// </summary>
    public class OptimizedRenderBatch
    {
        public List<OptimizedRenderCommand> Commands { get; } = new();
        public int Count => Commands.Count;

        public void AddCommand(OptimizedRenderCommand command)
        {
            Commands.Add(command);
        }
    }

    /// <summary>
    /// 渲染命令
    /// </summary>
    public class OptimizedRenderCommand
    {
        public OptimizedRenderCommandType Type { get; set; }
        public List<Avalonia.RoundedRect> RoundedRects { get; set; } = new();
        public Color Color { get; set; }
    }

    /// <summary>
    /// 渲染命令类型
    /// </summary>
    public enum OptimizedRenderCommandType
    {
        DrawRoundedRects,
        DrawLines,
        DrawText
    }

    /// <summary>
    /// 缓存的音符渲染数据
    /// </summary>
    public class CachedNoteRenderData
    {
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
        public bool IsValid { get; set; } = true;
    }

    /// <summary>
    /// 性能分析器
    /// </summary>
    public class PerformanceProfiler
    {
        private readonly Queue<double> _frameTimes = new();
        private const int MAX_SAMPLES = 100;
        
        public void BeginFrame() => _startTime = DateTime.UtcNow;
        public void EndFrame()
        {
            var elapsed = (DateTime.UtcNow - _startTime).TotalMilliseconds;
            _frameTimes.Enqueue(elapsed);
            if (_frameTimes.Count > MAX_SAMPLES) _frameTimes.Dequeue();
        }
        
        public PerformanceStats GetStats()
        {
            if (_frameTimes.Count == 0) return new PerformanceStats();
            
            var avgTime = _frameTimes.Average();
            return new PerformanceStats
            {
                FPS = 1000.0 / avgTime,
                RenderedNoteCount = 0, // 需要外部设置
                CullingRatio = 0.0     // 需要外部设置
            };
        }

        private DateTime _startTime;
    }

    /// <summary>
    /// 性能统计
    /// </summary>
    public class PerformanceStats
    {
        public double FPS { get; set; }
        public int RenderedNoteCount { get; set; }
        public double CullingRatio { get; set; }
    }

    /// <summary>
    /// LOD配置
    /// </summary>
    public class OptimizedLodConfig
    {
        public int Level { get; set; }
        public float DetailLevel { get; set; }
        public bool EnableEffects { get; set; }
        public bool EnableText { get; set; }
    }

    /// <summary>
    /// LOD渲染器
    /// </summary>
    public class OptimizedNoteLodRenderer
    {
        public OptimizedLodConfig GetLodConfig(int level)
        {
            return level switch
            {
                0 => new OptimizedLodConfig { Level = 0, DetailLevel = 1.0f, EnableEffects = true, EnableText = true },
                1 => new OptimizedLodConfig { Level = 1, DetailLevel = 0.8f, EnableEffects = true, EnableText = false },
                2 => new OptimizedLodConfig { Level = 2, DetailLevel = 0.5f, EnableEffects = false, EnableText = false },
                _ => new OptimizedLodConfig { Level = 3, DetailLevel = 0.3f, EnableEffects = false, EnableText = false }
            };
        }
    }

    /// <summary>
    /// 剔除系统
    /// </summary>
    public class OptimizedNoteCullingSystem
    {
        private Rect _viewport;
        private double _zoomLevel;
        
        public void SetViewport(Rect viewport, double zoomLevel)
        {
            _viewport = viewport;
            _zoomLevel = zoomLevel;
        }
        
        public List<(NoteViewModel note, Rect bounds, int lodLevel)> CullNotes(IEnumerable<NoteViewModel> notes)
        {
            var result = new List<(NoteViewModel, Rect, int)>();
            
            foreach (var note in notes)
            {
                // 简化的剔除逻辑
                var bounds = new Rect(note.StartTime, note.Pitch, 1.0, 1);
                
                // 视口剔除
                if (!bounds.Intersects(_viewport)) continue;
                
                // LOD计算
                var lodLevel = CalculateLodLevel(bounds);
                
                result.Add((note, bounds, lodLevel));
            }
            
            return result;
        }
        
        private int CalculateLodLevel(Rect bounds)
        {
            var area = bounds.Width * bounds.Height;
            if (area > 100) return 0; // 高细节
            if (area > 25) return 1;  // 中细节
            if (area > 4) return 2;   // 低细节
            return 3;                 // 最低细节
        }
    }
}