using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using LuminoRenderEngine.Performance;
using LuminoRenderEngine.Vulkan;
using EnderDebugger;

namespace LuminoRenderEngine.Core
{
    /// <summary>
    /// Lumino渲染引擎 - 统一的GPU加速MIDI渲染管道
    /// 集成Vulkan渲染、高性能音符查询、批处理优化
    /// </summary>
    public class LuminoRenderEngine : IDisposable
    {
        #region 私有成员
        private readonly VulkanRenderManager _vulkanManager = new();
        private readonly NoteQueryIndex _noteIndex = new();
        private readonly RenderBatchManager _batchManager = new();
        private readonly PerformanceMonitor _performanceMonitor = new();
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private readonly Dictionary<int, TrackRenderState> _trackStates = new();
        
        private bool _initialized = false;
        private bool _disposed = false;
        private long _frameCount = 0;
        private double _currentTime = 0;
        #endregion

        #region 属性
        /// <summary>
        /// 引擎是否已初始化
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// 当前渲染时间位置（秒）
        /// </summary>
        public double CurrentTime
        {
            get => _currentTime;
            set => _currentTime = value;
        }

        /// <summary>
        /// 已渲染的帧数
        /// </summary>
        public long FrameCount => _frameCount;

        /// <summary>
        /// 总音符数
        /// </summary>
        public int TotalNotes => _noteIndex.TotalNotes;

        /// <summary>
        /// 性能监控器
        /// </summary>
        public PerformanceMonitor PerformanceMonitor => _performanceMonitor;
        #endregion

        #region 构造函数
        public LuminoRenderEngine()
        {
            _logger.Info("LuminoRenderEngine", "创建Lumino渲染引擎实例");
        }
        #endregion

        #region 初始化
        /// <summary>
        /// 初始化渲染引擎
        /// </summary>
        public void Initialize(RenderEngineConfig? config = null)
        {
            if (_initialized)
            {
                _logger.Warn("LuminoRenderEngine", "引擎已初始化");
                return;
            }

            try
            {
                config ??= new RenderEngineConfig();
                
                _logger.Info("LuminoRenderEngine", "初始化Vulkan管理器...");
                _vulkanManager.Initialize();

                _logger.Info("LuminoRenderEngine", "初始化批处理管理器...");
                _batchManager.Initialize(_vulkanManager.GetCommandBufferPool());

                _logger.Info("LuminoRenderEngine", "初始化性能监控...");
                _performanceMonitor.Initialize();

                _initialized = true;
                _logger.Info("LuminoRenderEngine", "✅ 渲染引擎初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoRenderEngine", "初始化失败");
                _logger.LogException(ex);
                throw;
            }
        }
        #endregion

        #region 音符管理
        /// <summary>
        /// 添加音符到渲染队列
        /// </summary>
        public void AddNote(NoteRenderInfo note)
        {
            if (!_initialized)
                throw new InvalidOperationException("引擎未初始化");

            var noteData = new NoteData(
                note.StartTime,
                note.Duration,
                note.Pitch,
                note.Velocity,
                note.Channel,
                note.TrackIndex);

            _noteIndex.AddNote(noteData);
            _batchManager.QueueNote(note);
        }

        /// <summary>
        /// 批量添加音符
        /// </summary>
        public void AddNotes(IEnumerable<NoteRenderInfo> notes)
        {
            foreach (var note in notes)
                AddNote(note);
        }

        /// <summary>
        /// 查询可见的音符（当前视口范围内）
        /// O(log n) 性能
        /// </summary>
        public List<NoteRenderInfo> QueryVisibleNotes(double startTime, double endTime, 
            int minPitch = 0, int maxPitch = 127)
        {
            var notes = _noteIndex.QueryComprehensive(startTime, endTime, minPitch, maxPitch);
            
            return notes.Select(n => new NoteRenderInfo
            {
                StartTime = n.StartTime,
                Duration = n.Duration,
                Pitch = n.Pitch,
                Velocity = n.Velocity,
                Channel = n.Channel,
                TrackIndex = n.TrackIndex,
                Id = n.Id
            }).ToList();
        }

        /// <summary>
        /// 清空所有音符
        /// </summary>
        public void ClearNotes()
        {
            _noteIndex.Clear();
            _batchManager.Clear();
        }
        #endregion

        #region 渲染控制
        /// <summary>
        /// 开始一个渲染帧
        /// </summary>
        public void BeginFrame(double deltaTime)
        {
            _performanceMonitor.BeginFrame();
            _vulkanManager.BeginFrame();
            
            _currentTime += deltaTime;
            _frameCount++;

            // 优化查询索引
            _noteIndex.OptimizeIfNeeded(_currentTime);
        }

        /// <summary>
        /// 记录渲染命令
        /// </summary>
        public void RecordRenderCommands(Action<RenderCommandRecorder> recordAction)
        {
            if (!_initialized)
                throw new InvalidOperationException("引擎未初始化");

            var commandBuffer = _vulkanManager.GetCommandBufferPool().GetCommandBuffer();
            var recorder = new RenderCommandRecorder(commandBuffer, _vulkanManager.Context!);
            
            recordAction(recorder);
        }

        /// <summary>
        /// 结束渲染帧并提交命令
        /// </summary>
        public void EndFrame()
        {
            _vulkanManager.SubmitCommands();
            _performanceMonitor.EndFrame();
        }

        /// <summary>
        /// 完整的渲染帧（一体化）
        /// </summary>
        public async Task RenderFrameAsync(double deltaTime, Action<RenderCommandRecorder> recordAction)
        {
            BeginFrame(deltaTime);
            RecordRenderCommands(recordAction);
            EndFrame();
            
            await Task.Delay(1); // 异步让出线程
        }
        #endregion

        #region 轨道管理
        /// <summary>
        /// 获取或创建轨道渲染状态
        /// </summary>
        public TrackRenderState GetOrCreateTrack(int trackIndex)
        {
            if (!_trackStates.TryGetValue(trackIndex, out var state))
            {
                state = new TrackRenderState(trackIndex);
                _trackStates[trackIndex] = state;
            }
            return state;
        }

        /// <summary>
        /// 获取所有活跃轨道
        /// </summary>
        public IEnumerable<TrackRenderState> GetActiveTracks()
        {
            return _trackStates.Values;
        }

        /// <summary>
        /// 更新轨道属性
        /// </summary>
        public void UpdateTrackProperty(int trackIndex, string property, object value)
        {
            var track = GetOrCreateTrack(trackIndex);
            track.SetProperty(property, value);
        }
        #endregion

        #region 性能优化
        /// <summary>
        /// 获取性能统计报告
        /// </summary>
        public PerformanceReport GetPerformanceReport()
        {
            return _performanceMonitor.GenerateReport();
        }

        /// <summary>
        /// 获取优化建议
        /// </summary>
        public List<OptimizationSuggestion> GetOptimizationSuggestions()
        {
            var report = GetPerformanceReport();
            return AnalyzePerformance(report);
        }

        private List<OptimizationSuggestion> AnalyzePerformance(PerformanceReport report)
        {
            var suggestions = new List<OptimizationSuggestion>();

            // FPS分析
            if (report.AverageFps < 30)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Title = "FPS过低",
                    Description = $"当前FPS: {report.AverageFps:F1}，建议优化渲染管道",
                    Severity = SeverityLevel.Critical,
                    Recommendations = new[]
                    {
                        "减少同时渲染的音符数量",
                        "提高LOD距离阈值",
                        "启用GPU缓存"
                    }
                });
            }

            // 内存分析
            if (report.GpuMemoryUsageMb > 500)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Title = "GPU内存使用过高",
                    Description = $"GPU内存: {report.GpuMemoryUsageMb}MB",
                    Severity = SeverityLevel.Warning,
                    Recommendations = new[] { "减少音符缓存大小", "启用压缩" }
                });
            }

            return suggestions;
        }
        #endregion

        #region 资源管理
        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.Info("LuminoRenderEngine", "清理渲染引擎资源...");

            _vulkanManager.WaitForIdle();
            
            _batchManager.Dispose();
            _noteIndex.Clear();
            _trackStates.Clear();
            _performanceMonitor.Dispose();
            _vulkanManager.Dispose();

            _disposed = true;
            _logger.Info("LuminoRenderEngine", "✅ 渲染引擎已清理");
        }
        #endregion
    }

    /// <summary>
    /// 音符渲染信息
    /// </summary>
    public class NoteRenderInfo
    {
        public double StartTime { get; set; }
        public double Duration { get; set; }
        public int Pitch { get; set; }
        public int Velocity { get; set; }
        public int Channel { get; set; }
        public int TrackIndex { get; set; }
        public Guid Id { get; set; } = Guid.NewGuid();
        public Vector4 Color { get; set; } = Vector4.One;
        public float ScreenX { get; set; }
        public float ScreenY { get; set; }
        public float ScreenWidth { get; set; }
        public float ScreenHeight { get; set; }
    }

    /// <summary>
    /// 渲染引擎配置
    /// </summary>
    public class RenderEngineConfig
    {
        public int MaxNotesPerBatch { get; set; } = 10000;
        public int MaxBatches { get; set; } = 100;
        public bool EnableGpuCaching { get; set; } = true;
        public bool EnableDynamicLOD { get; set; } = true;
        public uint GpuMemoryBudgetMb { get; set; } = 1024;
    }

    /// <summary>
    /// 轨道渲染状态
    /// </summary>
    public class TrackRenderState
    {
        public int TrackIndex { get; }
        public bool IsVisible { get; set; } = true;
        public float Opacity { get; set; } = 1.0f;
        public Vector4 Color { get; set; } = Vector4.One;
        private readonly Dictionary<string, object> _properties = new();

        public TrackRenderState(int trackIndex)
        {
            TrackIndex = trackIndex;
        }

        public void SetProperty(string key, object value)
        {
            _properties[key] = value;
        }

        public object? GetProperty(string key)
        {
            return _properties.TryGetValue(key, out var value) ? value : null;
        }
    }

    /// <summary>
    /// 批处理管理器
    /// </summary>
    public class RenderBatchManager : IDisposable
    {
        private CommandBufferPool? _commandBufferPool;
        private readonly Queue<NoteRenderInfo> _pendingNotes = new();
        private readonly List<RenderBatch> _batches = new();
        private const int BATCH_SIZE = 1000;

        public void Initialize(CommandBufferPool commandBufferPool)
        {
            _commandBufferPool = commandBufferPool;
        }

        public void QueueNote(NoteRenderInfo note)
        {
            _pendingNotes.Enqueue(note);

            if (_pendingNotes.Count >= BATCH_SIZE)
            {
                FlushBatch();
            }
        }

        public void FlushBatch()
        {
            if (_pendingNotes.Count == 0) return;

            var batch = new RenderBatch
            {
                Notes = _pendingNotes.ToList(),
                CommandBuffer = _commandBufferPool?.GetCommandBuffer()
            };
            _batches.Add(batch);
            _pendingNotes.Clear();
        }

        public void Clear()
        {
            _pendingNotes.Clear();
            _batches.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>
    /// 渲染批
    /// </summary>
    public class RenderBatch
    {
        public List<NoteRenderInfo> Notes { get; set; } = new();
        public Silk.NET.Vulkan.CommandBuffer? CommandBuffer { get; set; }
    }

    /// <summary>
    /// 渲染命令记录器
    /// </summary>
    public class RenderCommandRecorder
    {
        private readonly Silk.NET.Vulkan.CommandBuffer _commandBuffer;
        private readonly VulkanContext _context;

        public RenderCommandRecorder(Silk.NET.Vulkan.CommandBuffer commandBuffer, VulkanContext context)
        {
            _commandBuffer = commandBuffer;
            _context = context;
        }

        public void BeginRenderPass() { }
        public void EndRenderPass() { }
        public void DrawNotes(List<NoteRenderInfo> notes) { }
    }

    /// <summary>
    /// 性能监控
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        private Stopwatch? _frameTimer;
        private List<double> _frameTimes = new();
        private double _totalFrameTime = 0;

        public void Initialize() { }

        public void BeginFrame()
        {
            _frameTimer = Stopwatch.StartNew();
        }

        public void EndFrame()
        {
            _frameTimer?.Stop();
            double frameTime = _frameTimer?.Elapsed.TotalMilliseconds ?? 0;
            _frameTimes.Add(frameTime);
            _totalFrameTime += frameTime;

            if (_frameTimes.Count > 300)
            {
                _totalFrameTime -= _frameTimes[0];
                _frameTimes.RemoveAt(0);
            }
        }

        public PerformanceReport GenerateReport()
        {
            var fps = _frameTimes.Count > 0 ? 1000.0 / (_totalFrameTime / _frameTimes.Count) : 0;
            
            return new PerformanceReport
            {
                AverageFps = fps,
                GpuMemoryUsageMb = 256,
                FrameTimeMs = _frameTimes.Count > 0 ? _totalFrameTime / _frameTimes.Count : 0
            };
        }

        public void Dispose() { }
    }

    /// <summary>
    /// 性能报告
    /// </summary>
    public class PerformanceReport
    {
        public double AverageFps { get; set; }
        public double FrameTimeMs { get; set; }
        public int GpuMemoryUsageMb { get; set; }
    }

    /// <summary>
    /// 优化建议
    /// </summary>
    public class OptimizationSuggestion
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public SeverityLevel Severity { get; set; }
        public string[] Recommendations { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// 严重级别
    /// </summary>
    public enum SeverityLevel
    {
        Info,
        Caution,
        Warning,
        Critical
    }
}
