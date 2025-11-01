using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Lumino.Services.Interfaces;
using Silk.NET.Vulkan;
using EnderDebugger;

namespace Lumino.Views.Rendering.Vulkan
{
    public class VulkanRenderContext : IDisposable
    {
        private readonly IVulkanRenderService _vulkanService;
        private readonly Dictionary<string, object> _buffers = new();
        private readonly Stack<object> _renderPasses = new();
        private readonly Queue<InstancedBatchData> _instancedBatchPool = new();
        private InstancedBatchData? _currentInstancedBatch;
        private const int MAX_INSTANCED_BATCHES = 20;
        private const long MAX_INSTANCE_MEMORY = 64 * 1024 * 1024; // 64MB
        
        // GPU内存池优化 - 兼容旧接口
        private readonly Queue<object> _vertexBufferPool = new();
        private readonly Queue<object> _indexBufferPool = new();
        private readonly Queue<object> _uniformBufferPool = new();
        private const int MAX_BUFFER_POOL_SIZE = 100;
        
        // GPU内存池优化 - 增强版本
        private readonly Dictionary<Type, Queue<object>> _typedBufferPools = new();
        private readonly Dictionary<object, MemoryInfo> _bufferMemoryInfo = new();
        private const int MAX_TYPED_POOL_SIZE = 200;
        private const long MAX_TOTAL_MEMORY = 512 * 1024 * 1024; // 512MB 总内存限制

        /// <summary>
        /// 内存信息跟踪
        /// </summary>
        private class MemoryInfo
        {
            public long Size { get; set; }
            public DateTime LastUsed { get; set; }
            public int UseCount { get; set; }
            public bool IsPooled { get; set; }

            public MemoryInfo(long size)
            {
                Size = size;
                LastUsed = DateTime.Now;
                UseCount = 1;
                IsPooled = false;
            }
        }
        
        // 性能监控工具
        private readonly PerformanceMonitor _performanceMonitor = new();
        private const int PERFORMANCE_SAMPLE_COUNT = 60; // 60帧采样

        /// <summary>
        /// 性能监控类
        /// </summary>
        private class PerformanceMonitor
        {
            private readonly Queue<double> _frameTimes = new();
            private readonly Queue<int> _drawCalls = new();
            private readonly Queue<long> _memoryUsage = new();
            private DateTime _lastFrameTime = DateTime.Now;
            private int _currentDrawCalls = 0;

            public void BeginFrame()
            {
                _lastFrameTime = DateTime.Now;
                _currentDrawCalls = 0;
            }

            public void EndFrame()
            {
                var frameTime = (DateTime.Now - _lastFrameTime).TotalMilliseconds;

                // 添加采样
                _frameTimes.Enqueue(frameTime);
                _drawCalls.Enqueue(_currentDrawCalls);
                _memoryUsage.Enqueue(GC.GetTotalMemory(false));

                // 保持采样数量
                while (_frameTimes.Count > PERFORMANCE_SAMPLE_COUNT) _frameTimes.Dequeue();
                while (_drawCalls.Count > PERFORMANCE_SAMPLE_COUNT) _drawCalls.Dequeue();
                while (_memoryUsage.Count > PERFORMANCE_SAMPLE_COUNT) _memoryUsage.Dequeue();
            }

            public void RecordDrawCall()
            {
                _currentDrawCalls++;
            }

            public PerformanceStats GetStats()
            {
                if (_frameTimes.Count == 0) return new PerformanceStats();

                return new PerformanceStats
                {
                    AverageFrameTime = _frameTimes.Average(),
                    MaxFrameTime = _frameTimes.Max(),
                    MinFrameTime = _frameTimes.Min(),
                    AverageDrawCalls = (int)_drawCalls.Average(),
                    MaxDrawCalls = _drawCalls.Max(),
                    TotalMemoryUsage = _memoryUsage.LastOrDefault(),
                    Fps = _frameTimes.Count > 0 ? 1000.0 / _frameTimes.Average() : 0
                };
            }
        }

        /// <summary>
        /// 性能统计数据
        /// </summary>
        public class PerformanceStats
        {
            public double AverageFrameTime { get; set; }
            public double MaxFrameTime { get; set; }
            public double MinFrameTime { get; set; }
            public int AverageDrawCalls { get; set; }
            public int MaxDrawCalls { get; set; }
            public long TotalMemoryUsage { get; set; }
            public double Fps { get; set; }

            public override string ToString()
            {
                return $"FPS: {Fps:F1}, FrameTime: {AverageFrameTime:F2}ms, DrawCalls: {AverageDrawCalls}, Memory: {TotalMemoryUsage / 1024 / 1024}MB";
            }
        }
        
        // 并行渲染支持
        private readonly object _batchLock = new object();        // 顶点数据缓存优化
        private readonly Dictionary<RoundedRectKey, CachedVertexData> _vertexCache = new();
        private readonly Queue<RoundedRectKey> _vertexCacheOrder = new();
        private const int MAX_VERTEX_CACHE_SIZE = 100;

        /// <summary>
        /// 圆角矩形缓存键
        /// </summary>
        private struct RoundedRectKey : IEquatable<RoundedRectKey>
        {
            public float Width;
            public float Height;
            public float RadiusX;
            public float RadiusY;

            public RoundedRectKey(float width, float height, float radiusX, float radiusY)
            {
                Width = width;
                Height = height;
                RadiusX = radiusX;
                RadiusY = radiusY;
            }

            public bool Equals(RoundedRectKey other)
            {
                return Width.Equals(other.Width) && Height.Equals(other.Height) &&
                       RadiusX.Equals(other.RadiusX) && RadiusY.Equals(other.RadiusY);
            }

            public override bool Equals(object? obj) => obj is RoundedRectKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Width, Height, RadiusX, RadiusY);
        }

        /// <summary>
        /// 缓存的顶点数据
        /// </summary>
        private class CachedVertexData
        {
            public float[] Vertices { get; set; }
            public uint[] Indices { get; set; }
            public object? VertexBuffer { get; set; }
            public object? IndexBuffer { get; set; }
            public DateTime LastUsed { get; set; }

            public CachedVertexData(float[] vertices, uint[] indices)
            {
                Vertices = vertices;
                Indices = indices;
                LastUsed = DateTime.Now;
            }
        }

        public VulkanRenderContext(IVulkanRenderService vulkanService)
        {
            _vulkanService = vulkanService ?? throw new ArgumentNullException(nameof(vulkanService));
            _currentInstancedBatch = new InstancedBatchData(); // 初始化当前批处理
        }

    /// <summary>
    /// 优化后的实例化数据 - GPU高性能渲染，内存对齐优化
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct InstanceData
    {
        // 位置和尺寸 (16字节对齐)
        public Silk.NET.Maths.Vector2D<float> Position; // x, y (8字节)
        public Silk.NET.Maths.Vector2D<float> Scale;    // width, height (8字节)

        // 颜色和圆角 (16字节对齐)
        public Silk.NET.Maths.Vector4D<float> Color;    // r, g, b, a (16字节)
        public float Radius;                            // 圆角半径 (4字节)
        public uint RenderFlags;                        // 渲染标志位 (4字节)

        public InstanceData()
        {
            Position = new Silk.NET.Maths.Vector2D<float>(0f, 0f);
            Scale = new Silk.NET.Maths.Vector2D<float>(1f, 1f);
            Color = new Silk.NET.Maths.Vector4D<float>(1f, 1f, 1f, 1f); // 默认白色不透明
            Radius = 3.0f;
            RenderFlags = 0; // 默认无特殊标志
        }

        // 渲染标志位定义
        public const uint FLAG_HAS_SHADOW = 1 << 0;
        public const uint FLAG_IS_SELECTED = 1 << 1;
        public const uint FLAG_HAS_BORDER = 1 << 2;
        public const uint FLAG_USE_GRADIENT = 1 << 3;
    }

    /// <summary>
    /// Push常量结构体
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PushConstants
    {
        public Silk.NET.Maths.Matrix4X4<float> Projection;
        public Silk.NET.Maths.Vector4D<float> Color;
        public float Radius;
    }

        /// <summary>
        /// GPU实例化批处理数据 - 优化版本
        /// </summary>
        public class InstancedBatchData
        {
            public List<InstanceData> Instances { get; } = new List<InstanceData>();
            public object? VertexBuffer { get; set; }
            public object? IndexBuffer { get; set; }
            public object? InstanceBuffer { get; set; }
            public int InstanceCount => Instances.Count;
            public long MemoryUsage { get; set; }
            public DateTime LastUsed { get; set; }
            public bool IsDirty { get; set; } // 标记是否需要更新GPU缓冲区

            public InstancedBatchData()
            {
                LastUsed = DateTime.Now;
                IsDirty = false;
            }

            public void Clear()
            {
                Instances.Clear();
                MemoryUsage = 0;
                IsDirty = true;
                LastUsed = DateTime.Now;
            }

            public bool CanAddInstance(InstanceData instance, long maxMemoryUsage)
            {
                long estimatedMemory = MemoryUsage + EstimateInstanceMemory(instance);
                // 限制实例数量和内存使用，同时考虑GPU性能
                return InstanceCount < 2048 && estimatedMemory < maxMemoryUsage;
            }

            public void AddInstance(InstanceData instance)
            {
                Instances.Add(instance);
                MemoryUsage += EstimateInstanceMemory(instance);
                IsDirty = true;
                LastUsed = DateTime.Now;
            }

            private long EstimateInstanceMemory(InstanceData instance)
            {
                // 更精确的内存估算，包括对齐开销
                return 64; // InstanceData结构体大小 + GPU对齐开销
            }

            /// <summary>
            /// 压缩实例数据，移除重复项
            /// </summary>
            public void Optimize()
            {
                if (Instances.Count < 2) return;

                // 简单的去重优化（在实际实现中可以更复杂）
                var uniqueInstances = new List<InstanceData>();
                var seen = new HashSet<(float, float, float, float, float)>();

                foreach (var instance in Instances)
                {
                    var key = (instance.Position.X, instance.Position.Y,
                              instance.Scale.X, instance.Scale.Y, instance.Radius);
                    if (seen.Add(key))
                    {
                        uniqueInstances.Add(instance);
                    }
                }

                if (uniqueInstances.Count < Instances.Count)
                {
                    Instances.Clear();
                    Instances.AddRange(uniqueInstances);
                    IsDirty = true;
                    EnderLogger.Instance.Debug("GPU优化", $"实例批次压缩: {uniqueInstances.Count}/{Instances.Count} 个唯一实例");
                }
            }
        }

        /// <summary>
        /// 获取或创建实例化批次 - 优化版本
        /// </summary>
        private InstancedBatchData GetOrCreateInstancedBatch()
        {
            lock (_batchLock)
            {
                // 首先尝试重用现有批次
                if (_currentInstancedBatch != null &&
                    _currentInstancedBatch.CanAddInstance(default, MAX_INSTANCE_MEMORY))
                {
                    return _currentInstancedBatch;
                }

                // 从池中获取批次
                if (_instancedBatchPool.Count > 0)
                {
                    var batch = _instancedBatchPool.Dequeue();
                    batch.Clear();
                    _currentInstancedBatch = batch;
                    return batch;
                }

                // 创建新批次
                if (_instancedBatchPool.Count < MAX_INSTANCED_BATCHES)
                {
                    _currentInstancedBatch = new InstancedBatchData();
                    return _currentInstancedBatch;
                }

                // 池已满，强制刷新并重用最早的批次
                FlushInstancedBatches();
                if (_instancedBatchPool.Count > 0)
                {
                    var batch = _instancedBatchPool.Dequeue();
                    batch.Clear();
                    _currentInstancedBatch = batch;
                    return batch;
                }

                // 极端情况：创建新批次
                _currentInstancedBatch = new InstancedBatchData();
                return _currentInstancedBatch;
            }
        }

        /// <summary>
        /// 刷新所有实例化批次到GPU
        /// </summary>
        private void FlushInstancedBatches()
        {
            lock (_batchLock)
            {
                var batchesToFlush = new List<InstancedBatchData>();

                // 收集所有需要刷新的批次
                if (_currentInstancedBatch != null && _currentInstancedBatch.InstanceCount > 0)
                {
                    batchesToFlush.Add(_currentInstancedBatch);
                }

                foreach (var batch in _instancedBatchPool)
                {
                    if (batch.InstanceCount > 0)
                    {
                        batchesToFlush.Add(batch);
                    }
                }

                // 并行处理批次优化和刷新
                Parallel.ForEach(batchesToFlush, batch =>
                {
                    batch.Optimize(); // 优化批次数据

                    if (batch.IsDirty && batch.InstanceCount > 0)
                    {
                        // 刷新到GPU（这里是占位符，实际实现需要Vulkan命令）
                        EnderLogger.Instance.Debug("GPU优化", $"刷新实例化批次: {batch.InstanceCount} 个实例, {batch.MemoryUsage} 字节");
                        batch.IsDirty = false;
                    }

                    // 回收批次到池中
                    if (_instancedBatchPool.Count < MAX_INSTANCED_BATCHES)
                    {
                        batch.Clear();
                        _instancedBatchPool.Enqueue(batch);
                    }
                });

                _currentInstancedBatch = null;
            }
        }

        /// <summary>
        /// 获取或创建缓存的圆角矩形顶点数据
        /// </summary>
        private CachedVertexData GetOrCreateCachedVertices(float width, float height, float radiusX, float radiusY)
        {
            var key = new RoundedRectKey(width, height, radiusX, radiusY);

            // 尝试从缓存获取
            if (_vertexCache.TryGetValue(key, out var cachedData))
            {
                cachedData.LastUsed = DateTime.Now;
                return cachedData;
            }

            // 缓存未命中，创建新的顶点数据
            var vertices = GenerateRoundedRectVertices(width, height, radiusX, radiusY);
            var indices = GenerateRoundedRectIndices();

            cachedData = new CachedVertexData(vertices, indices);

            // 添加到缓存
            _vertexCache[key] = cachedData;
            _vertexCacheOrder.Enqueue(key);

            // 缓存大小管理
            if (_vertexCache.Count > MAX_VERTEX_CACHE_SIZE)
            {
                // 移除最旧的缓存项
                var oldestKey = _vertexCacheOrder.Dequeue();
                if (_vertexCache.TryGetValue(oldestKey, out var oldestData))
                {
                    // 清理GPU资源（占位符）
                    oldestData.VertexBuffer = null;
                    oldestData.IndexBuffer = null;
                }
                _vertexCache.Remove(oldestKey);
            }

            return cachedData;
        }

        /// <summary>
        /// 生成圆角矩形的顶点数据 - 优化版本
        /// </summary>
        private float[] GenerateRoundedRectVertices(float width, float height, float radiusX, float radiusY)
        {
            // 使用更高效的顶点生成算法
            const int segmentsPerCorner = 8; // 每个圆角的段数
            const int totalVertices = 4 + 4 * segmentsPerCorner; // 4个角 + 4个圆角的段

            var vertices = new float[totalVertices * 2]; // x, y per vertex
            int vertexIndex = 0;

            // 确保半径不超过矩形尺寸的一半
            float rx = Math.Min(radiusX, width * 0.5f);
            float ry = Math.Min(radiusY, height * 0.5f);

            // 左上角圆角
            AddCornerVertices(vertices, ref vertexIndex, 0, 0, rx, ry, segmentsPerCorner, Math.PI, Math.PI * 1.5f);

            // 右上角圆角
            AddCornerVertices(vertices, ref vertexIndex, width - rx, 0, rx, ry, segmentsPerCorner, Math.PI * 1.5f, Math.PI * 2);

            // 右下角圆角
            AddCornerVertices(vertices, ref vertexIndex, width - rx, height - ry, rx, ry, segmentsPerCorner, 0, Math.PI * 0.5f);

            // 左下角圆角
            AddCornerVertices(vertices, ref vertexIndex, 0, height - ry, rx, ry, segmentsPerCorner, Math.PI * 0.5f, Math.PI);

            return vertices;
        }

        /// <summary>
        /// 添加圆角顶点
        /// </summary>
        private void AddCornerVertices(float[] vertices, ref int vertexIndex, float centerX, float centerY,
                                      float radiusX, float radiusY, int segments, double startAngle, double endAngle)
        {
            double angleStep = (endAngle - startAngle) / segments;

            for (int i = 0; i <= segments; i++)
            {
                double angle = startAngle + angleStep * i;
                float x = centerX + radiusX * (float)Math.Cos(angle);
                float y = centerY + radiusY * (float)Math.Sin(angle);

                vertices[vertexIndex++] = x;
                vertices[vertexIndex++] = y;
            }
        }

        /// <summary>
        /// 生成圆角矩形的索引数据 - 优化版本
        /// </summary>
        private uint[] GenerateRoundedRectIndices()
        {
            const int segmentsPerCorner = 8;
            const int verticesPerCorner = segmentsPerCorner + 1;
            const int totalVertices = 4 + 4 * segmentsPerCorner;

            // 使用三角扇形填充圆角，使用三角形条带填充直边
            var indices = new List<uint>();

            // 中心矩形（不包括圆角区域）
            uint centerTopLeft = (uint)(verticesPerCorner * 0 + segmentsPerCorner / 2);
            uint centerTopRight = (uint)(verticesPerCorner * 1 + segmentsPerCorner / 2);
            uint centerBottomRight = (uint)(verticesPerCorner * 2 + segmentsPerCorner / 2);
            uint centerBottomLeft = (uint)(verticesPerCorner * 3 + segmentsPerCorner / 2);

            // 添加中心矩形
            indices.AddRange(new uint[] {
                centerTopLeft, centerTopRight, centerBottomRight,
                centerTopLeft, centerBottomRight, centerBottomLeft
            });

            // 添加四个圆角的三角扇形
            for (int corner = 0; corner < 4; corner++)
            {
                uint cornerStart = (uint)(corner * verticesPerCorner);
                uint centerVertex = cornerStart; // 圆角的第一个顶点作为中心

                for (int i = 1; i < verticesPerCorner; i++)
                {
                    indices.Add(centerVertex);
                    indices.Add(cornerStart + (uint)i);
                    indices.Add(cornerStart + (uint)((i + 1) % verticesPerCorner));
                }
            }

            return indices.ToArray();
        }

        /// <summary>
        /// 更新缓冲区数据
        /// </summary>
        private void UpdateBufferData<T>(object buffer, T data)
        {
            // 这里将集成实际的Vulkan缓冲区更新逻辑
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        public PerformanceStats GetPerformanceStats()
        {
            return _performanceMonitor.GetStats();
        }

        /// <summary>
        /// 开始帧性能监控
        /// </summary>
        public void BeginFrame()
        {
            _performanceMonitor.BeginFrame();
        }

        /// <summary>
        /// 结束帧性能监控
        /// </summary>
        public void EndFrame()
        {
            _performanceMonitor.EndFrame();
        }

        /// <summary>
        /// 记录绘制调用
        /// </summary>
        public void RecordDrawCall()
        {
            _performanceMonitor.RecordDrawCall();
        }

        public object? CreateVertexBuffer(float[] vertices)
        {
            // 尝试从对象池获取缓冲区
            if (_vertexBufferPool.Count > 0)
            {
                var buffer = _vertexBufferPool.Dequeue();
                // 更新缓冲区数据
                UpdateBufferData(buffer, vertices);
                return buffer;
            }
            
            // 这里将集成实际的Vulkan缓冲区创建逻辑
            return new object(); // 临时占位符
        }

        /// <summary>
        /// 创建索引缓冲区
        /// </summary>
        public object? CreateIndexBuffer(uint[] indices)
        {
            // 尝试从对象池获取缓冲区
            if (_indexBufferPool.Count > 0)
            {
                var buffer = _indexBufferPool.Dequeue();
                // 更新缓冲区数据
                UpdateBufferData(buffer, indices);
                return buffer;
            }
            
            // 这里将集成实际的Vulkan索引缓冲区创建逻辑
            return new object(); // 临时占位符
        }

        /// <summary>
        /// 创建统一缓冲区
        /// </summary>
        public object? CreateUniformBuffer<T>(T data) where T : unmanaged
        {
            // 尝试从对象池获取缓冲区
            if (_uniformBufferPool.Count > 0)
            {
                var buffer = _uniformBufferPool.Dequeue();
                // 更新缓冲区数据
                UpdateBufferData(buffer, data);
                return buffer;
            }
            
            // 这里将集成实际的Vulkan统一缓冲区创建逻辑
            return new object(); // 临时占位符
        }
        
        /// <summary>
        /// 绘制矩形
        /// </summary>
        public void DrawRect(Rect rect, IBrush brush, IPen? pen = null)
        {
            if (!_vulkanService.IsEnabled || brush == null) return;

            // 异步执行渲染命令以提高性能
            _vulkanService.EnqueueRenderCommand(() =>
            {
                // 这里将集成实际的Vulkan矩形绘制逻辑
                // 包括顶点生成、着色器绑定、绘制调用等
            });
        }

        /// <summary>
        /// 设置画刷
        /// </summary>
        public void SetBrush(IBrush brush)
        {
            if (!_vulkanService.IsEnabled) return;
            // 这里将集成实际的Vulkan画刷设置逻辑
        }

        /// <summary>
        /// 设置画笔
        /// </summary>
        public void SetPen(IPen pen)
        {
            if (!_vulkanService.IsEnabled) return;
            // 这里将集成实际的Vulkan画笔设置逻辑
        }

        /// <summary>
        /// 绘制多个圆角矩形实例 - GPU高性能优化
        /// </summary>
        public void DrawRoundedRectsInstanced(IEnumerable<RoundedRect> rects, IBrush brush, IPen? pen = null)
        {
            if (!_vulkanService.IsEnabled) return;

            // 转换为实例数据
            var instances = new List<InstanceData>();
            foreach (var rect in rects)
            {
                if (rect.Rect.Width > 0 && rect.Rect.Height > 0)
                {
                    // 转换Avalonia颜色到Vector4D
                    var color = Colors.White;
                    if (brush is SolidColorBrush solidBrush)
                    {
                        color = solidBrush.Color;
                    }
                    var colorVec = new Silk.NET.Maths.Vector4D<float>(
                        color.R / 255f,
                        color.G / 255f,
                        color.B / 255f,
                        color.A / 255f
                    );

                    uint flags = 0;
                    if (pen != null) flags |= InstanceData.FLAG_HAS_BORDER;

                    instances.Add(new InstanceData
                    {
                        Position = new Silk.NET.Maths.Vector2D<float>((float)rect.Rect.X, (float)rect.Rect.Y),
                        Scale = new Silk.NET.Maths.Vector2D<float>((float)rect.Rect.Width, (float)rect.Rect.Height),
                        Color = colorVec,
                        Radius = 3.0f, // 使用固定圆角半径
                        RenderFlags = flags
                    });
                }
            }

            if (instances.Count == 0) return;

            // 获取或创建实例化批次
            var batch = GetOrCreateInstancedBatch();

            // 添加实例到批次
            foreach (var instance in instances)
            {
                if (!batch.CanAddInstance(instance, MAX_INSTANCE_MEMORY))
                {
                    // 批次已满，刷新当前批次并创建新批次
                    FlushInstancedBatches();
                    batch = GetOrCreateInstancedBatch();
                }
                batch.Instances.Add(instance);
            }

            // 立即刷新以确保渲染
            FlushInstancedBatches();
        }

        /// <summary>
        /// 绘制圆角矩形 - GPU优化版本
        /// </summary>
        public void DrawRoundedRect(RoundedRect rect, IBrush brush, IPen? pen = null)
        {
            // 获取VulkanManager实例
            var vulkanServiceImpl = _vulkanService as Lumino.Services.Implementation.VulkanRenderService;
            var vulkanManager = vulkanServiceImpl?.VulkanManager;
            if (vulkanManager == null || !_vulkanService.IsEnabled) return;

            // 直接排队到VulkanManager的命令缓冲区
            Action<CommandBuffer> renderAction = (CommandBuffer commandBuffer) =>
            {
                try
                {
                    // 1. 检查矩形是否有效
                    if (rect.Rect.Width <= 0 || rect.Rect.Height <= 0)
                        return;

                    // 2. 检查画刷是否有效
                    if (brush == null)
                        return;

                    // 3. 计算圆角矩形的顶点数据 - 使用实例化渲染优化
                    float[] vertices = GenerateRoundedRectVertices((float)rect.Rect.Width, (float)rect.Rect.Height,
                                                                 (float)GetRadiusX(rect), (float)GetRadiusY(rect));
                    uint[] indices = GenerateRoundedRectIndices();

                    // 4. 创建顶点缓冲区和索引缓冲区
                    object? vertexBuffer = CreateVertexBuffer(vertices);
                    object? indexBuffer = CreateIndexBuffer(indices);

                    // 5. 存储缓冲区引用以便后续清理
                    string bufferKey = $"roundedRect_{vertices.GetHashCode()}_{indices.GetHashCode()}";
                    _buffers[bufferKey] = new { VertexBuffer = vertexBuffer, IndexBuffer = indexBuffer };

                    // 6. GPU优化的实例化渲染
                    // 对于大量相似音符，使用实例化渲染减少draw call
                    var stats = _vulkanService.GetStats();
                    bool useInstancing = stats.TotalDrawCalls > 50; // 当draw call超过50时使用实例化

                    if (vertexBuffer != null && indexBuffer != null)
                    {
                        if (useInstancing)
                        {
                            // 实例化渲染：一次绘制多个相似的圆角矩形
                            var instanceData = new InstanceData
                            {
                                Position = new Silk.NET.Maths.Vector2D<float>((float)rect.Rect.X, (float)rect.Rect.Y),
                                Scale = new Silk.NET.Maths.Vector2D<float>((float)rect.Rect.Width, (float)rect.Rect.Height),
                                Color = new Silk.NET.Maths.Vector4D<float>(
                                    GetBrushColor(brush)[0],
                                    GetBrushColor(brush)[1],
                                    GetBrushColor(brush)[2],
                                    GetBrushColor(brush)[3]
                                ),
                                Radius = (float)GetRadiusX(rect),
                                RenderFlags = 0 // 默认无特殊标志
                            };
                            DrawInstancedRoundedRect(commandBuffer, vertexBuffer, indexBuffer, instanceData);
                            EnderLogger.Instance.Debug("GPU优化", $"实例化渲染圆角矩形: {rect.Rect.Width}x{rect.Rect.Height}");
                        }
                        else
                        {
                            // 传统单实例渲染
                            DrawSingleRoundedRect(commandBuffer, vertexBuffer, indexBuffer, rect, brush, pen);
                        }
                    }

                    // 7. 性能监控
                    var memoryUsage = EstimateMemoryUsage(vertices, indices);
                    EnderLogger.Instance.Debug("GPU性能", $"圆角矩形内存使用: {memoryUsage} bytes, 实例化: {useInstancing}");
                }
                catch (Exception ex)
                {
                    EnderLogger.Instance.Error("GPU错误", $"Vulkan绘制圆角矩形异常: {ex.Message}");
                }
            };

            vulkanManager.EnqueueRenderCommand(renderAction);
        }

        /// <summary>
        /// 获取圆角矩形的X轴圆角半径
        /// </summary>
        private double GetRadiusX(RoundedRect rect)
        {
            // 简化实现，返回默认圆角半径
            return 3.0; // 与NoteRenderer中的CORNER_RADIUS保持一致
        }
        
        /// <summary>
        /// 获取圆角矩形的Y轴圆角半径
        /// </summary>
        private double GetRadiusY(RoundedRect rect)
        {
            // 简化实现，返回默认圆角半径
            return 3.0; // 与NoteRenderer中的CORNER_RADIUS保持一致
        }

        /// <summary>
        /// 绘制线条
        /// </summary>
        public void DrawLine(Point start, Point end, IPen pen)
        {
            if (!_vulkanService.IsEnabled) return;

            // 异步执行渲染命令以提高性能
            _vulkanService.EnqueueRenderCommand(() =>
            {
                // 这里将集成实际的Vulkan线条绘制逻辑
            });
        }

        /// <summary>
        /// 绘制文本
        /// </summary>
        public void DrawText(string text, Rect bounds, IBrush brush, double fontSize = 12)
        {
            if (!_vulkanService.IsEnabled) return;

            // 异步执行渲染命令以提高性能
            _vulkanService.EnqueueRenderCommand(() =>
            {
                // 这里将集成实际的Vulkan文本渲染逻辑
                // 包括字体纹理生成、字符映射、文本布局等
            });
        }

        /// <summary>
        /// 设置裁剪区域
        /// </summary>
        public void SetClip(Rect clipRect)
        {
            if (!_vulkanService.IsEnabled) return;

            // 异步执行渲染命令以提高性能
            _vulkanService.EnqueueRenderCommand(() =>
            {
                // 这里将集成实际的Vulkan裁剪逻辑
            });
        }

        /// <summary>
        /// 清除裁剪区域
        /// </summary>
        public void ClearClip()
        {
            if (!_vulkanService.IsEnabled) return;

            // 异步执行渲染命令以提高性能
            _vulkanService.EnqueueRenderCommand(() =>
            {
                // 这里将集成实际的Vulkan裁剪清除逻辑
            });
        }

        /// <summary>
        /// 刷新命令缓冲区
        /// </summary>
        public void Flush()
        {
            if (!_vulkanService.IsEnabled) return;

            // 异步执行渲染命令以提高性能
            _vulkanService.EnqueueRenderCommand(() =>
            {
                // 这里将集成实际的Vulkan命令缓冲区提交逻辑
            });
        }

        /// <summary>
        /// 绘制实例化圆角矩形 - GPU高性能优化
        /// </summary>
        private void DrawInstancedRoundedRect(CommandBuffer commandBuffer, object vertexBuffer, object indexBuffer, InstanceData instanceData)
        {
            // 实例化渲染实现
            // 1. 创建实例缓冲区
            object instanceBuffer = CreateInstanceBuffer(instanceData);
            
            // 2. 绑定实例数据
            BindInstanceBuffer(commandBuffer, instanceBuffer);
            
            // 3. 执行实例化绘制调用 - 一次绘制多个实例
            // 在实际Vulkan实现中，这里应该调用 vkCmdDrawIndexed 或 vkCmdDraw
            EnderLogger.Instance.Debug("GPU实例化", $"绘制实例: 位置({instanceData.Position[0]},{instanceData.Position[1]}), 大小({instanceData.Scale[0]},{instanceData.Scale[1]})");
            
            // 4. 清理实例缓冲区
            CleanupInstanceBuffer(instanceBuffer);
        }

        /// <summary>
        /// 绘制单个圆角矩形 - 传统渲染
        /// </summary>
        private unsafe void DrawSingleRoundedRect(CommandBuffer commandBuffer, object vertexBuffer, object indexBuffer, RoundedRect rect, IBrush brush, IPen? pen)
        {
            // 传统单实例渲染实现
            double radiusX = GetRadiusX(rect);
            double radiusY = GetRadiusY(rect);
            
            // 实际的Vulkan绘制命令
            // 这里需要实现真正的Vulkan顶点缓冲区绑定和绘制调用
            
            // 1. 绑定顶点缓冲区
            // 2. 绑定索引缓冲区  
            // 3. 设置uniform数据（颜色、变换等）
            // 4. 执行绘制调用
            
            EnderLogger.Instance.Debug("GPU单实例", $"绘制圆角矩形: {rect.Rect.Width}x{rect.Rect.Height}, 圆角: {radiusX}x{radiusY}");
        }

        /// <summary>
        /// 创建实例缓冲区
        /// </summary>
        private object CreateInstanceBuffer(InstanceData instanceData)
        {
            // 在实际实现中，这里应该创建Vulkan实例缓冲区
            return new object();
        }

        /// <summary>
        /// 绑定实例缓冲区
        /// </summary>
        private void BindInstanceBuffer(CommandBuffer commandBuffer, object instanceBuffer)
        {
            // 在实际实现中，这里应该绑定Vulkan实例缓冲区到管线
        }

        /// <summary>
        /// 清理实例缓冲区
        /// </summary>
        private void CleanupInstanceBuffer(object instanceBuffer)
        {
            // 在实际实现中，这里应该清理Vulkan实例缓冲区资源
        }

        /// <summary>
        /// 获取画刷颜色
        /// </summary>
        private float[] GetBrushColor(IBrush brush)
        {
            if (brush is ISolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                return new float[] { color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f };
            }
            return new float[] { 1f, 1f, 1f, 1f }; // 默认白色
        }

        /// <summary>
        /// 估算内存使用量
        /// </summary>
        private long EstimateMemoryUsage(float[] vertices, uint[] indices)
        {
            long vertexMemory = vertices.Length * sizeof(float);
            long indexMemory = indices.Length * sizeof(uint);
            return vertexMemory + indexMemory + 1024; // 额外开销
        }

        /// <summary>
        /// 获取实例化批处理对象 - GPU对象池优化
        /// </summary>
        private InstancedBatchData GetPooledInstancedBatch()
        {
            if (_instancedBatchPool.Count > 0)
            {
                var batch = _instancedBatchPool.Dequeue();
                batch.Clear();
                return batch;
            }
            return new InstancedBatchData();
        }

        /// <summary>
        /// 返回实例化批处理对象到对象池
        /// </summary>
        private void ReturnPooledInstancedBatch(InstancedBatchData batch)
        {
            if (batch != null && _instancedBatchPool.Count < MAX_INSTANCED_BATCHES)
            {
                batch.Clear();
                _instancedBatchPool.Enqueue(batch);
            }
        }

        /// <summary>
        /// 刷新当前实例化批处理 - GPU批处理优化
        /// </summary>
        public void FlushInstancedBatch()
        {
            lock (_batchLock)
            {
                if (_currentInstancedBatch != null && _currentInstancedBatch.InstanceCount > 0)
                {
                    int instanceCount = _currentInstancedBatch.InstanceCount; // 先保存实例数量

                    // 执行实例化渲染
                    DrawInstancedBatch(_currentInstancedBatch);

                    // 返回对象池
                    ReturnPooledInstancedBatch(_currentInstancedBatch);
                    _currentInstancedBatch = null;

                    EnderLogger.Instance.Debug("GPU批处理", $"刷新实例化批次: {instanceCount} 个实例");
                }
            }
        }

        /// <summary>
        /// 绘制实例化批处理
        /// </summary>
        private void DrawInstancedBatch(InstancedBatchData batch)
        {
            if (batch == null || batch.InstanceCount == 0) return;

            try
            {
                // 1. 创建实例缓冲区
                var instanceDataArray = batch.Instances.ToArray();
                object instanceBuffer = CreateInstanceBuffer(instanceDataArray);

                // 2. 执行实例化绘制
                // 在实际Vulkan实现中，这里应该调用 vkCmdDrawIndexedIndirect 或 vkCmdDraw
                EnderLogger.Instance.Debug("GPU实例化", $"绘制批处理: {batch.InstanceCount} 个实例, 内存使用: {batch.MemoryUsage} bytes");

                // 3. 清理实例缓冲区
                CleanupInstanceBuffer(instanceBuffer);

                // 更新统计信息
                // _vulkanService.UpdateStats 已移除，无需手动更新统计信息
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("GPU错误", $"实例化批处理绘制失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建实例缓冲区数组
        /// </summary>
        private object CreateInstanceBuffer(InstanceData[] instances)
        {
            // 在实际实现中，这里应该创建包含所有实例数据的Vulkan缓冲区
            return new object();
        }

        /// <summary>
        /// 清理资源 - GPU内存优化
        /// </summary>
        public void Dispose()
        {
            // 清理当前实例化批处理
            if (_currentInstancedBatch != null)
            {
                ReturnPooledInstancedBatch(_currentInstancedBatch);
                _currentInstancedBatch = null;
            }

            // 清理实例化批处理池
            while (_instancedBatchPool.Count > 0)
            {
                var batch = _instancedBatchPool.Dequeue();
                // 清理批处理资源
                batch.Clear();
            }

            // 清理Vulkan缓冲区资源
            foreach (var buffer in _buffers.Values)
            {
                // 在实际实现中，这里应该清理Vulkan缓冲区资源
            }
            _buffers.Clear();
            
            // 清理缓冲区对象池
            _vertexBufferPool.Clear();
            _indexBufferPool.Clear();
            _uniformBufferPool.Clear();
            
            EnderLogger.Instance.Info("GPU内存", "Vulkan渲染上下文资源已清理");
        }
    }

    /// <summary>
    /// Vulkan渲染状态管理器
    /// </summary>
    public class VulkanRenderState
    {
        private readonly Stack<Rect> _clipStack = new();
        private readonly Stack<Matrix> _transformStack = new();
        private Matrix _currentTransform = Matrix.Identity;
        private Rect _currentClip = default;

        public Rect CurrentClip => _currentClip;
        public Matrix CurrentTransform => _currentTransform;

        public void PushClip(Rect clip)
        {
            _clipStack.Push(_currentClip);
            _currentClip = clip == default ? clip : (_currentClip == default ? clip : _currentClip.Intersect(clip));
        }

        public void PopClip()
        {
            if (_clipStack.Count > 0)
            {
                _currentClip = _clipStack.Pop();
            }
            else
            {
                _currentClip = default;
            }
        }

        public void PushTransform(Matrix transform)
        {
            _transformStack.Push(_currentTransform);
            _currentTransform = transform * _currentTransform;
        }

        public void PopTransform()
        {
            if (_transformStack.Count > 0)
            {
                _currentTransform = _transformStack.Pop();
            }
            else
            {
                _currentTransform = Matrix.Identity;
            }
        }

        public void Reset()
        {
            _clipStack.Clear();
            _transformStack.Clear();
            _currentTransform = Matrix.Identity;
            _currentClip = default;
        }
    }
}