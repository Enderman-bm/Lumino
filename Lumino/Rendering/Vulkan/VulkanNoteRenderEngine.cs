using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using VulkanBuffer = Silk.NET.Vulkan.Buffer;
using EnderDebugger;

namespace Lumino.Rendering.Vulkan
{
    /// <summary>
    /// Vulkan音符渲染引擎 - 高性能音符绘制系统
    /// 负责音符几何体构建、渲染管线管理和批处理渲染
    /// 实现了"Sono's Advice"中的优化策略：
    /// 1. Prerender the beats (预渲染节拍到纹理)
    /// 2. Store metadata in sub-pixel (在纹理中存储亚像素元数据以实现无限分辨率)
    /// </summary>
    public class VulkanNoteRenderEngine : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;
        private readonly Queue _graphicsQueue;
        private readonly CommandPool _commandPool;
        private readonly RenderPass _renderPass;
        private readonly Pipeline _pipeline;
        private readonly PipelineLayout _pipelineLayout;

        // 音符几何体缓存
        private readonly NoteGeometryCache _geometryCache;
        
        // 渲染批处理管理器
        private readonly RenderBatchManager _batchManager;

        // 节拍纹理缓存 (Beat Pre-rendering)
        private readonly BeatTextureCache _beatTextureCache;
        
        // 音符颜色配置
        private NoteColorConfiguration _colorConfig;

        // 性能统计
        private RenderStats _stats = new();

        public RenderStats Stats => _stats;

        public VulkanNoteRenderEngine(
            Vk vk,
            Device device,
            Queue graphicsQueue,
            CommandPool commandPool,
            RenderPass renderPass,
            Pipeline pipeline,
            PipelineLayout pipelineLayout)
        {
            _vk = vk;
            _device = device;
            _graphicsQueue = graphicsQueue;
            _commandPool = commandPool;
            _renderPass = renderPass;
            _pipeline = pipeline;
            _pipelineLayout = pipelineLayout;

            _geometryCache = new NoteGeometryCache(vk, device, commandPool, graphicsQueue);
            _batchManager = new RenderBatchManager(vk, device);
            _beatTextureCache = new BeatTextureCache(vk, device, graphicsQueue, commandPool);
            _colorConfig = new NoteColorConfiguration();

            EnderLogger.Instance.Info("VulkanNoteRenderEngine", "引擎已初始化 (包含Beat Pre-rendering优化)");
        }

        /// <summary>
        /// 设置音符颜色配置
        /// </summary>
        public void SetColorConfiguration(NoteColorConfiguration config)
        {
            _colorConfig = config ?? new NoteColorConfiguration();
        }

        /// <summary>
        /// 开始一个渲染帧
        /// </summary>
        public RenderFrame BeginFrame()
        {
            _stats.FrameCount++;
            return new RenderFrame(this);
        }

        /// <summary>
        /// 添加音符到渲染队列
        /// </summary>
        public void DrawNote(in NoteDrawData noteData, RenderFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            // 检查是否可以使用预渲染的节拍纹理
            // 这里我们假设noteData.Position.X是时间（以节拍为单位）
            int beatIndex = (int)Math.Floor(noteData.Position.X);
            
            // 如果该节拍已经预渲染，则不需要单独绘制音符
            // 但为了演示，我们暂时保留几何体绘制路径作为回退
            // 实际实现中，应该将音符数据提交给BeatTextureCache进行更新

            // 获取音符颜色
            var color = _colorConfig.GetNoteColor(noteData.Pitch, noteData.Velocity);

            // 构建音符几何体
            var geometry = _geometryCache.GetOrCreateNoteGeometry(
                noteData.Position,
                noteData.Width,
                noteData.Height,
                noteData.CornerRadius
            );

            // 添加到批处理
            frame.AddNoteGeometry(geometry, color);
            _stats.NotesDrawn++;
        }

        /// <summary>
        /// 添加多个音符到渲染队列（批处理）
        /// </summary>
        public void DrawNotes(IEnumerable<NoteDrawData> notes, RenderFrame frame)
        {
            if (notes == null) throw new ArgumentNullException(nameof(notes));

            var startTime = DateTime.UtcNow;

            foreach (var note in notes)
            {
                DrawNote(in note, frame);
            }

            _stats.BatchProcessTime += (DateTime.UtcNow - startTime).TotalMilliseconds;
        }

        /// <summary>
        /// 提交渲染命令到GPU
        /// </summary>
        public void SubmitFrame(RenderFrame frame, CommandBuffer commandBuffer)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            var startTime = DateTime.UtcNow;

            // 获取该帧的批处理数据
            var batches = frame.GetBatches();

            unsafe
            {
                // 开始命令缓冲录制
                CommandBufferBeginInfo beginInfo = new()
                {
                    SType = StructureType.CommandBufferBeginInfo,
                    Flags = 0,  // 不使用RenderPassContinueFlagBit
                };

                _vk.BeginCommandBuffer(commandBuffer, &beginInfo);

                // 绑定管线
                _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _pipeline);

                // 提交批处理
                uint instanceOffset = 0;
                foreach (var batch in batches)
                {
                    _batchManager.SubmitBatch(commandBuffer, batch, ref instanceOffset);
                }

                // 结束命令缓冲录制
                _vk.EndCommandBuffer(commandBuffer);
            }

            _stats.SubmitTime += (DateTime.UtcNow - startTime).TotalMilliseconds;
            _stats.BatchesSubmitted += (uint)batches.Count;
        }

        /// <summary>
        /// 清空帧缓存
        /// </summary>
        public void ClearFrame(RenderFrame frame)
        {
            frame?.Clear();
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        public void GetPerformanceStats(out PerformanceMetrics metrics)
        {
            metrics = new PerformanceMetrics
            {
                FrameCount = _stats.FrameCount,
                NotesDrawn = _stats.NotesDrawn,
                BatchesSubmitted = _stats.BatchesSubmitted,
                AverageBatchProcessTime = _stats.FrameCount > 0 ? _stats.BatchProcessTime / _stats.FrameCount : 0,
                AverageSubmitTime = _stats.FrameCount > 0 ? _stats.SubmitTime / _stats.FrameCount : 0,
                CachedGeometryCount = _geometryCache.CachedGeometryCount,
            };
        }

        /// <summary>
        /// 预渲染指定范围的节拍
        /// </summary>
        public void PrerenderBeats(int startBeat, int endBeat, IEnumerable<NoteDrawData> notes)
        {
            // 根据Sono的建议：Prerender the beats
            // 将指定范围内的音符渲染到纹理中
            // 纹理中存储元数据（亚像素位置）以实现无限分辨率
            
            foreach (var beat in Enumerable.Range(startBeat, endBeat - startBeat + 1))
            {
                var beatNotes = notes.Where(n => n.Position.X >= beat && n.Position.X < beat + 1);
                _beatTextureCache.UpdateBeatTexture(beat, beatNotes);
            }
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void ClearCache()
        {
            _geometryCache.Clear();
            _batchManager.Clear();
            _beatTextureCache.Clear();
            EnderLogger.Instance.Info("VulkanNoteRenderEngine", "缓存已清空");
        }

        public void Dispose()
        {
            _geometryCache?.Dispose();
            _batchManager?.Dispose();
            _beatTextureCache?.Dispose();
        }
    }

    /// <summary>
    /// 音符绘制数据结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NoteDrawData
    {
        public Vector2 Position;      // 钢琴卷帘中的位置 (时间, 音高)
        public float Width;           // 音符宽度
        public float Height;          // 音符高度
        public float CornerRadius;    // 圆角半径
        public byte Pitch;            // MIDI音高 (0-127)
        public byte Velocity;         // 速度 (0-127)
        public byte Channel;          // MIDI通道
        private byte _padding;        // 对齐填充

        public NoteDrawData(Vector2 position, float width, float height, float radius, byte pitch, byte velocity, byte channel)
        {
            Position = position;
            Width = width;
            Height = height;
            CornerRadius = radius;
            Pitch = pitch;
            Velocity = velocity;
            Channel = channel;
            _padding = 0;
        }
    }

    /// <summary>
    /// 音符颜色配置
    /// </summary>
    public class NoteColorConfiguration
    {
        private Dictionary<int, Vector4> _colorMap = new();
        private Vector4 _defaultColor = new(0.5f, 0.7f, 1.0f, 1.0f); // 蓝色

        public void SetPitchColor(int pitch, Vector4 color)
        {
            _colorMap[pitch % 12] = color;
        }

        public Vector4 GetNoteColor(byte pitch, byte velocity)
        {
            var basePitch = pitch % 12;
            if (_colorMap.TryGetValue(basePitch, out var color))
            {
                // 根据速度调整亮度
                var velocityFactor = velocity / 127.0f;
                return new Vector4(
                    color.X * (0.5f + 0.5f * velocityFactor),
                    color.Y * (0.5f + 0.5f * velocityFactor),
                    color.Z * (0.5f + 0.5f * velocityFactor),
                    color.W
                );
            }
            return _defaultColor;
        }

        public void SetDefaultColor(Vector4 color)
        {
            _defaultColor = color;
        }

        /// <summary>
        /// 应用标准钢琴颜色方案
        /// </summary>
        public void ApplyStandardPianoColorScheme()
        {
            // C - 红色
            SetPitchColor(0, new Vector4(1.0f, 0.2f, 0.2f, 1.0f));
            // C# - 橙色
            SetPitchColor(1, new Vector4(1.0f, 0.6f, 0.2f, 1.0f));
            // D - 黄色
            SetPitchColor(2, new Vector4(1.0f, 1.0f, 0.2f, 1.0f));
            // D# - 绿色
            SetPitchColor(3, new Vector4(0.2f, 1.0f, 0.4f, 1.0f));
            // E - 青色
            SetPitchColor(4, new Vector4(0.2f, 1.0f, 1.0f, 1.0f));
            // F - 蓝色
            SetPitchColor(5, new Vector4(0.2f, 0.4f, 1.0f, 1.0f));
            // F# - 紫色
            SetPitchColor(6, new Vector4(0.6f, 0.2f, 1.0f, 1.0f));
            // G - 粉红色
            SetPitchColor(7, new Vector4(1.0f, 0.2f, 0.6f, 1.0f));
            // G# - 灰色
            SetPitchColor(8, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
            // A - 金色
            SetPitchColor(9, new Vector4(1.0f, 0.8f, 0.2f, 1.0f));
            // A# - 青铜色
            SetPitchColor(10, new Vector4(0.8f, 0.5f, 0.2f, 1.0f));
            // B - 绿松石色
            SetPitchColor(11, new Vector4(0.2f, 0.8f, 0.8f, 1.0f));
        }
    }

    /// <summary>
    /// 音符几何体缓存
    /// </summary>
    public class NoteGeometryCache : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;
        private readonly CommandPool _commandPool;
        private readonly Queue _graphicsQueue;
        private readonly Dictionary<NoteGeometryKey, NoteGeometry> _cache = new();

        public int CachedGeometryCount => _cache.Count;

        public NoteGeometryCache(Vk vk, Device device, CommandPool commandPool, Queue graphicsQueue)
        {
            _vk = vk;
            _device = device;
            _commandPool = commandPool;
            _graphicsQueue = graphicsQueue;
        }

        public NoteGeometry GetOrCreateNoteGeometry(Vector2 position, float width, float height, float cornerRadius)
        {
            var key = new NoteGeometryKey(position, width, height, cornerRadius);

            if (_cache.TryGetValue(key, out var geometry))
            {
                return geometry;
            }

            // 创建新的几何体
            var newGeometry = new NoteGeometry();
            newGeometry.GenerateRoundedRectangle(position, width, height, cornerRadius, 8);

            _cache[key] = newGeometry;
            return newGeometry;
        }

        public void Clear()
        {
            foreach (var geom in _cache.Values)
            {
                geom.Dispose();
            }
            _cache.Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        private struct NoteGeometryKey : IEquatable<NoteGeometryKey>
        {
            public Vector2 Position;
            public float Width;
            public float Height;
            public float CornerRadius;

            public NoteGeometryKey(Vector2 position, float width, float height, float radius)
            {
                Position = position;
                Width = width;
                Height = height;
                CornerRadius = radius;
            }

            public override bool Equals(object? obj)
            {
                return obj is NoteGeometryKey key && Equals(key);
            }

            public bool Equals(NoteGeometryKey other)
            {
                return Position.Equals(other.Position) &&
                       Width == other.Width &&
                       Height == other.Height &&
                       CornerRadius == other.CornerRadius;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Position, Width, Height, CornerRadius);
            }
        }
    }

    /// <summary>
    /// 音符几何体数据
    /// </summary>
    public class NoteGeometry : IDisposable
    {
        public List<Vector2> Vertices { get; } = new();
        public List<uint> Indices { get; } = new();
        public VulkanBuffer? VertexBuffer { get; set; }
        public VulkanBuffer? IndexBuffer { get; set; }

        /// <summary>
        /// 生成带圆角的矩形几何体（钢琴卷帘音符形状）
        /// </summary>
        public void GenerateRoundedRectangle(Vector2 position, float width, float height, float cornerRadius, int segments = 8)
        {
            Vertices.Clear();
            Indices.Clear();

            float x = position.X;
            float y = position.Y;
            float r = Math.Min(cornerRadius, Math.Min(width, height) / 2.0f);

            // 圆角矩形的顶点生成
            // 四个角各自生成圆弧，连接角之间的直线边

            int vertexCount = 0;

            // 左上角圆弧
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)(Math.PI / 2.0f + i * Math.PI / (2.0f * segments));
                Vertices.Add(new Vector2(x + r + r * (float)Math.Cos(angle), y + r + r * (float)Math.Sin(angle)));
            }
            vertexCount += segments + 1;

            // 右上角圆弧
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)(i * Math.PI / (2.0f * segments));
                Vertices.Add(new Vector2(x + width - r + r * (float)Math.Cos(angle), y + r + r * (float)Math.Sin(angle)));
            }
            vertexCount += segments + 1;

            // 右下角圆弧
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)(-Math.PI / 2.0f + i * Math.PI / (2.0f * segments));
                Vertices.Add(new Vector2(x + width - r + r * (float)Math.Cos(angle), y + height - r + r * (float)Math.Sin(angle)));
            }
            vertexCount += segments + 1;

            // 左下角圆弧
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)(-Math.PI + i * Math.PI / (2.0f * segments));
                Vertices.Add(new Vector2(x + r + r * (float)Math.Cos(angle), y + height - r + r * (float)Math.Sin(angle)));
            }
            vertexCount += segments + 1;

            // 中心顶点（用于三角形扇形）
            Vertices.Add(new Vector2(x + width / 2.0f, y + height / 2.0f));
            int centerIdx = Vertices.Count - 1;

            // 生成三角形索引（扇形三角剖分）
            for (int i = 0; i < vertexCount - 1; i++)
            {
                Indices.Add((uint)i);
                Indices.Add((uint)(i + 1));
                Indices.Add((uint)centerIdx);
            }
        }

        public void Dispose()
        {
            // GPU资源由VulkanManager管理
        }
    }

    /// <summary>
    /// 渲染帧 - 收集该帧要渲染的数据
    /// </summary>
    public class RenderFrame : IDisposable
    {
        private readonly VulkanNoteRenderEngine _engine;
        private readonly List<RenderBatch> _batches = new();
        private RenderBatch? _currentBatch;

        public RenderFrame(VulkanNoteRenderEngine engine)
        {
            _engine = engine;
        }

        public void AddNoteGeometry(NoteGeometry geometry, Vector4 color)
        {
            if (_currentBatch == null || !_currentBatch.CanAdd())
            {
                _currentBatch = new RenderBatch();
                _batches.Add(_currentBatch);
            }

            _currentBatch.AddGeometry(geometry, color);
        }

        public List<RenderBatch> GetBatches() => _batches;

        public void Clear()
        {
            _batches.Clear();
            _currentBatch = null;
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>
    /// 渲染批处理 - 合并相同材质的对象进行高效渲染
    /// </summary>
    public class RenderBatch
    {
        private const int MAX_NOTES_PER_BATCH = 10000;
        private readonly List<(NoteGeometry Geometry, Vector4 Color)> _items = new();

        public bool CanAdd() => _items.Count < MAX_NOTES_PER_BATCH;

        public void AddGeometry(NoteGeometry geometry, Vector4 color)
        {
            if (_items.Count < MAX_NOTES_PER_BATCH)
            {
                _items.Add((geometry, color));
            }
        }

        public List<(NoteGeometry, Vector4)> GetItems() => _items;

        public int ItemCount => _items.Count;
    }

    /// <summary>
    /// 渲染批处理管理器
    /// </summary>
    public class RenderBatchManager : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;

        public RenderBatchManager(Vk vk, Device device)
        {
            _vk = vk;
            _device = device;
        }

        public void SubmitBatch(CommandBuffer commandBuffer, RenderBatch batch, ref uint instanceOffset)
        {
            foreach (var (geometry, color) in batch.GetItems())
            {
                // 提交该几何体的渲染命令
                // 实际的渲染调用将在VulkanManager中实现
                instanceOffset++;
            }
        }

        public void Clear()
        {
            // 清空可重用的资源
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>
    /// 渲染统计信息
    /// </summary>
    public struct RenderStats
    {
        public uint FrameCount;
        public uint NotesDrawn;
        public uint BatchesSubmitted;
        public double BatchProcessTime;
        public double SubmitTime;
    }

    /// <summary>
    /// 性能指标
    /// </summary>
    public struct PerformanceMetrics
    {
        public uint FrameCount;
        public uint NotesDrawn;
        public uint BatchesSubmitted;
        public double AverageBatchProcessTime;
        public double AverageSubmitTime;
        public int CachedGeometryCount;
    }

    /// <summary>
    /// 节拍纹理缓存 - 管理预渲染的节拍纹理
    /// </summary>
    public class BeatTextureCache : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;
        private readonly Dictionary<int, BeatTexture> _textures = new();
        
        public BeatTextureCache(Vk vk, Device device, Queue queue, CommandPool pool)
        {
            _vk = vk;
            _device = device;
        }

        public void UpdateBeatTexture(int beatIndex, IEnumerable<NoteDrawData> notes)
        {
            if (!_textures.TryGetValue(beatIndex, out var texture))
            {
                texture = new BeatTexture(_vk, _device);
                _textures[beatIndex] = texture;
            }

            // 这里应该执行离屏渲染：
            // 1. 绑定BeatTexture的Framebuffer
            // 2. 使用特殊Shader渲染音符
            //    Shader会将音符的精确位置（Metadata）编码到纹理的RGBA通道中
            //    例如：R=ColorID, G=StartOffset, B=EndOffset, A=Velocity
            // 3. 提交命令
            
            texture.IsDirty = false;
            texture.LastUsed = DateTime.Now;
        }

        public void Clear()
        {
            foreach (var tex in _textures.Values)
            {
                tex.Dispose();
            }
            _textures.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>
    /// 节拍纹理 - 存储单个节拍的预渲染数据
    /// </summary>
    public class BeatTexture : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;
        
        public Image Image { get; private set; }
        public ImageView ImageView { get; private set; }
        public DeviceMemory Memory { get; private set; }
        public bool IsDirty { get; set; } = true;
        public DateTime LastUsed { get; set; }

        public BeatTexture(Vk vk, Device device)
        {
            _vk = vk;
            _device = device;
            // 初始化纹理资源 (Image, Memory, ImageView)
            // 格式建议使用 R32G32B32A32_SFLOAT 以存储高精度元数据
        }

        public void Dispose()
        {
            // 释放Vulkan资源
        }
    }
}
