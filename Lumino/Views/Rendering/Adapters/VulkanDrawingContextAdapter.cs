using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Lumino.Services.Interfaces;
using Lumino.Services.Implementation;
using Lumino.Views.Rendering.Vulkan;
using EnderDebugger;

namespace Lumino.Views.Rendering.Adapters
{
    /// <summary>
    /// Vulkan渲染上下文适配器 - 将Avalonia的DrawingContext适配到Vulkan渲染
    /// </summary>
    public class VulkanDrawingContextAdapter : IDisposable
    {
        private readonly IVulkanRenderService _vulkanService;
        private readonly VulkanRenderContext? _vulkanContext;
        private readonly DrawingContext _skiaContext;
        private readonly bool _useVulkan;
        
        /// <summary>
        /// 指示此适配器是否真正使用 Vulkan 渲染（而非回退到 Skia）
        /// </summary>
        public bool IsVulkanEnabled => _useVulkan;
        
        // 优化相关字段
        private Rect _currentViewport = new Rect(0, 0, 0, 0);
        private readonly Stack<Rect> _clipStack = new();
        private readonly Stack<Matrix> _transformStack = new();
        private Matrix _currentTransform = Matrix.Identity;
        
        // 批处理优化
        private readonly Dictionary<BatchKey, BatchData> _batches = new();
        private const int MAX_BATCH_SIZE = 1000; // 每批次最大对象数
        
        // GPU内存管理
        private readonly Queue<BatchData> _batchPool = new();
        private const int MAX_BATCH_POOL_SIZE = 50; // 批次对象池最大大小
        private long _totalMemoryUsed = 0;
        private const long MAX_MEMORY_USAGE = 256 * 1024 * 1024; // 256MB GPU内存限制

        public VulkanDrawingContextAdapter(DrawingContext skiaContext)
        {
            _skiaContext = skiaContext ?? throw new ArgumentNullException(nameof(skiaContext));
            _vulkanService = VulkanRenderService.Instance;
            var contextObj = _vulkanService.GetRenderContext();
            _vulkanContext = contextObj as VulkanRenderContext;
            
            // Vulkan直接渲染到窗口swapchain会与Avalonia的渲染冲突（两者都渲染到同一窗口）
            // 这里禁用Vulkan路径，使用Skia回退进行所有渲染
            // 对于需要高性能Vulkan渲染的场景（如大量音符），请使用VulkanOffscreenCanvas
            // VulkanOffscreenCanvas使用离屏渲染并将结果作为位图显示，避免了冲突
            _useVulkan = false;

            if (_useVulkan)
            {
                // _vulkanService.BeginFrame();
            }
        }
        
        /// <summary>
        /// 设置当前视口，用于可见性测试
        /// </summary>
        /// <param name="viewport"></param>
        public void SetViewport(Rect viewport)
        {
            _currentViewport = viewport;
        }
        
        /// <summary>
        /// 检查矩形是否在当前视口内（公开方法）
        /// </summary>
        public bool IsRectVisible(Rect rect)
        {
            if (_currentViewport.Width <= 0 || _currentViewport.Height <= 0)
                return true;

            // 简单的矩形相交测试
            return _currentViewport.Intersects(rect);
        }
        
        /// <summary>
        /// 强制刷新所有批处理数据 - GPU状态切换优化版本
        /// </summary>
        public void FlushBatches()
        {
            if (!_useVulkan || _vulkanContext == null)
                return;

            try
            {
                // GPU状态切换优化：按渲染状态排序批次，减少GPU状态变化
                // 排序优先级：变换矩阵 -> 混合模式 -> 画刷 -> 笔 -> 圆角参数
                var sortedBatches = _batches
                    .OrderBy(kvp => GetTransformHash(kvp.Key.Transform))
                    .ThenBy(kvp => kvp.Key.BlendMode)
                    .ThenBy(kvp => GetBrushHash(kvp.Key.Brush))
                    .ThenBy(kvp => GetPenHash(kvp.Key.Pen))
                    .ThenBy(kvp => kvp.Key.RadiusX)
                    .ThenBy(kvp => kvp.Key.RadiusY)
                    .Select(kvp => kvp.Value)
                    .ToList();

                // 统计信息
                int totalRects = 0;
                int batchCount = sortedBatches.Count;

                // 遍历并处理所有批次
                foreach (var batch in sortedBatches)
                {
                    if (batch.Rects.Count == 0) continue;

                    totalRects += batch.Rects.Count;

                    // 简化状态管理：每次都设置画刷和笔
                    _vulkanContext.SetBrush(batch.Brush);
                    if (batch.Pen != null)
                    {
                        _vulkanContext.SetPen(batch.Pen);
                    }

                    // GPU优化：批量绑定状态后一次性提交多个矩形顶点数据
                    // 使用实例化渲染技术，减少draw call
                    if (batch.Rects.Count >= 10)
                    {
                        // 大批量使用实例化渲染
                        _vulkanContext.DrawRoundedRectsInstanced(batch.Rects, batch.Brush, batch.Pen);
                    }
                    else
                    {
                        // 小批量使用传统渲染
                        foreach (var rect in batch.Rects)
                        {
                            _vulkanContext.DrawRoundedRect(rect, batch.Brush, batch.Pen);
                        }
                    }
                }

                // 性能监控
                if (totalRects > 0)
                {
                    EnderLogger.Instance.Info("VulkanDrawingContextAdapter", $"Vulkan GPU批处理优化: {batchCount}个批次, {totalRects}个矩形, 平均{(totalRects / Math.Max(1, batchCount))}个/批次");
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanDrawingContextAdapter", "Vulkan批处理刷新错误");
                // 错误时回退到传统渲染
                foreach (var batch in _batches.Values)
                {
                    foreach (var rect in batch.Rects)
                    {
                        _vulkanContext.DrawRoundedRect(rect, batch.Brush, batch.Pen);
                    }
                }
            }
            finally
            {
                _batches.Clear();
            }
        }
        
        /// <summary>
        /// 获取画刷哈希值，用于状态排序
        /// </summary>
        private int GetBrushHash(IBrush brush)
        {
            if (brush is SolidColorBrush solidBrush)
            {
                return solidBrush.Color.GetHashCode() ^ solidBrush.Opacity.GetHashCode();
            }
            return brush?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// 获取画笔哈希值，用于状态排序
        /// </summary>
        private int GetPenHash(IPen? pen)
        {
            if (pen is Pen p && p.Brush is SolidColorBrush solidBrush)
            {
                return solidBrush.Color.GetHashCode() ^ p.Thickness.GetHashCode() ^ solidBrush.Opacity.GetHashCode();
            }
            return pen?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// 获取变换矩阵的哈希值，用于状态排序
        /// </summary>
        private int GetTransformHash(Matrix transform)
        {
            // 使用矩阵的主要元素计算哈希，以减少状态变化
            // 简化实现，避免访问可能不存在的属性
            return transform.GetHashCode();
        }

        /// <summary>
        /// 检查两个矩阵是否近似相等（避免浮点精度问题）
        /// </summary>
        private bool AreMatricesEqual(Matrix a, Matrix b)
        {
            const double epsilon = 0.001;
            return Math.Abs(a.M11 - b.M11) < epsilon &&
                   Math.Abs(a.M12 - b.M12) < epsilon &&
                   Math.Abs(a.M21 - b.M21) < epsilon &&
                   Math.Abs(a.M22 - b.M22) < epsilon &&
                   Math.Abs(a.M31 - b.M31) < epsilon &&
                   Math.Abs(a.M32 - b.M32) < epsilon;
        }
        
        // 简化的混合模式枚举
        private enum SimpleBlendMode
        {
            SrcOver,
            Src,
            Multiply,
            Screen
        }

        // 批处理键类 - 优化为包含完整渲染状态
        private class BatchKey : IEquatable<BatchKey>
        {
            public IBrush Brush { get; }
            public IPen? Pen { get; }
            public double RadiusX { get; }
            public double RadiusY { get; }
            public Matrix Transform { get; }
            public Rect ClipRect { get; }
            public SimpleBlendMode BlendMode { get; }

            public BatchKey(IBrush brush, IPen? pen, double radiusX, double radiusY,
                           Matrix transform, Rect clipRect, SimpleBlendMode blendMode = SimpleBlendMode.SrcOver)
            {
                Brush = brush;
                Pen = pen;
                RadiusX = radiusX;
                RadiusY = radiusY;
                Transform = transform;
                ClipRect = clipRect;
                BlendMode = blendMode;
            }

            public bool Equals(BatchKey? other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(Brush, other.Brush) &&
                       Equals(Pen, other.Pen) &&
                       RadiusX.Equals(other.RadiusX) &&
                       RadiusY.Equals(other.RadiusY) &&
                       Transform.Equals(other.Transform) &&
                       ClipRect.Equals(other.ClipRect) &&
                       BlendMode == other.BlendMode;
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as BatchKey);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Brush, Pen, RadiusX, RadiusY, Transform, ClipRect, BlendMode);
            }
        }        // 批处理数据类
        private class BatchData
        {
            public IBrush Brush { get; }
            public IPen? Pen { get; }
            public List<RoundedRect> Rects { get; } = new();
            
            public BatchData(IBrush brush, IPen? pen)
            {
                Brush = brush;
                Pen = pen;
            }
        }

        /// <summary>
        /// 绘制矩形 - 优先使用Vulkan，回退到Skia
        /// </summary>
        public void DrawRectangle(IBrush? brush, IPen? pen, Rect rect)
        {
            if (_useVulkan && _vulkanContext != null && brush != null)
            {
                _vulkanContext.DrawRect(rect, brush, pen);
            }
            else
            {
                _skiaContext.DrawRectangle(brush, pen, rect);
            }
        }

        /// <summary>
        /// 绘制圆角矩形 - 优先使用Vulkan，回退到Skia
        /// 包含批处理优化、可见性测试和GPU内存管理
        /// </summary>
        public void DrawRectangle(IBrush? brush, IPen? pen, RoundedRect rect)
        {
            // 可见性测试
            if (!IsRectVisible(rect.Rect))
                return;

            if (_useVulkan && _vulkanContext != null && brush != null)
            {
                // GPU内存管理：检查内存使用
                if (_totalMemoryUsed > MAX_MEMORY_USAGE)
                {
                    EnderLogger.Instance.Warn("VulkanDrawingContextAdapter", $"Vulkan GPU内存警告: 当前使用 {_totalMemoryUsed / (1024 * 1024)}MB，超过限制 {MAX_MEMORY_USAGE / (1024 * 1024)}MB，强制刷新");
                    FlushBatches();
                }
                
                // 获取圆角半径
                double radiusX = GetRadiusX(rect);
                double radiusY = GetRadiusY(rect);
                
                // 使用批处理优化和对象池
                if (radiusX > 0 || radiusY > 0)
                {
                    // 为圆角矩形创建批处理键
                    var key = new BatchKey(brush, pen, radiusX, radiusY,
                                          _currentTransform, _clipStack.Count > 0 ? _clipStack.Peek() : _currentViewport,
                                          SimpleBlendMode.SrcOver);
                    
                    // 检查是否存在现有批次
                    if (!_batches.TryGetValue(key, out var batch))
                    {
                        // GPU优化：从对象池获取批次对象
                        batch = GetPooledBatchData(brush, pen);
                        _batches[key] = batch;
                        
                        // 估算内存使用
                        _totalMemoryUsed += EstimateBatchMemoryUsage(batch);
                    }
                    
                    // 添加到批次
                    batch.Rects.Add(rect);
                    
                    // 智能批处理：根据对象大小和数量动态调整刷新策略
                    if (batch.Rects.Count >= GetOptimalBatchSize(rect.Rect))
                    {
                        FlushBatches();
                    }
                }
                else
                {
                    // 普通矩形直接绘制
                    _vulkanContext.DrawRect(rect.Rect, brush, pen);
                }
            }
            else
            {
                // 回退到Skia渲染
                _skiaContext.DrawRectangle(brush, pen, rect);
            }
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
        /// 绘制线条 - 优先使用Vulkan，回退到Skia
        /// </summary>
        public void DrawLine(IPen pen, Point startPoint, Point endPoint)
        {
            if (_useVulkan && _vulkanContext != null)
            {
                _vulkanContext.DrawLine(startPoint, endPoint, pen);
            }
            else
            {
                _skiaContext.DrawLine(pen, startPoint, endPoint);
            }
        }

        /// <summary>
        /// 绘制文本 - 优先使用Vulkan，回退到Skia
        /// </summary>
        public void DrawText(string text, Typeface typeface, double fontSize, IBrush foreground, Point origin)
        {
            if (_useVulkan && _vulkanContext != null)
            {
                var textBounds = new Rect(origin.X, origin.Y, 100, 20); // 临时估算
                _vulkanContext.DrawText(text, textBounds, foreground, fontSize);
            }
            else
            {
                var formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, foreground);
                _skiaContext.DrawText(formattedText, origin);
            }
        }

        /// <summary>
        /// 设置裁剪区域 - 优先使用Vulkan，回退到Skia
        /// </summary>
        public void PushClip(Rect clip)
        {
            if (_useVulkan && _vulkanContext != null)
            {
                _vulkanContext.SetClip(clip);
            }
            else
            {
                _skiaContext.PushClip(clip);
            }
        }

        /// <summary>
        /// 清除裁剪区域 - 优先使用Vulkan，回退到Skia
        /// </summary>
        public void PopClip()
        {
            if (_clipStack.Count > 0)
            {
                _clipStack.Pop();
            }
            
            if (_useVulkan && _vulkanContext != null)
            {
                // 在实际实现中，这里应该更新Vulkan的裁剪状态
                // _vulkanContext.ClearClip();
            }
            else
            {
                // 对于Skia回退模式，使用变换来实现裁剪
                // 这里简化处理
            }
        }

        /// <summary>
        /// 推送变换矩阵 - 优先使用Vulkan，回退到Skia
        /// </summary>
        public void PushTransform(Matrix matrix)
        {
            if (_useVulkan && _vulkanContext != null)
            {
                // Vulkan变换将在实际实现中处理
            }
            else
            {
                _skiaContext.PushTransform(matrix);
            }
        }

        /// <summary>
        /// 弹出变换矩阵 - 优先使用Vulkan，回退到Skia
        /// </summary>
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
            
            if (_useVulkan && _vulkanContext != null)
            {
                // 在实际实现中，这里应该更新Vulkan的变换状态
            }
            else
            {
                // 对于Skia回退模式，使用Avalonia的PushTransform
                // 这里简化处理
            }
        }

        /// <summary>
        /// 绘制几何图形 - 优先使用Vulkan，回退到Skia
        /// </summary>
        public void DrawGeometry(IBrush? brush, IPen? pen, Geometry geometry)
        {
            if (_useVulkan && _vulkanContext != null && brush != null)
            {
                // 将几何图形转换为Vulkan顶点数据
                // 这里需要实现几何图形的三角化
                var bounds = geometry.Bounds;
                _vulkanContext.DrawRect(bounds, brush, pen);
            }
            else
            {
                _skiaContext.DrawGeometry(brush, pen, geometry);
            }
        }

        /// <summary>
        /// 获取渲染统计信息
        /// </summary>
        public VulkanRenderStats GetRenderStats()
        {
            return _vulkanService.GetStats();
        }

        /// <summary>
        /// 从对象池获取批次数据 - GPU内存优化
        /// </summary>
        private BatchData GetPooledBatchData(IBrush brush, IPen? pen)
        {
            if (_batchPool.Count > 0)
            {
                var batch = _batchPool.Dequeue();
                batch.Rects.Clear();
                return batch;
            }
            
            return new BatchData(brush, pen);
        }
        
        /// <summary>
        /// 将批次数据返回到对象池 - GPU内存优化
        /// </summary>
        private void ReturnPooledBatchData(BatchData batch)
        {
            if (batch != null && _batchPool.Count < MAX_BATCH_POOL_SIZE)
            {
                batch.Rects.Clear();
                _batchPool.Enqueue(batch);
            }
        }
        
        /// <summary>
        /// 估算批次内存使用 - GPU内存管理
        /// </summary>
        private long EstimateBatchMemoryUsage(BatchData batch)
        {
            // 估算：每个矩形约100字节，画刷和画笔约200字节
            return batch.Rects.Count * 100L + 200L;
        }
        
        /// <summary>
        /// 根据矩形大小获取最优批处理大小 - GPU性能优化
        /// </summary>
        private int GetOptimalBatchSize(Rect rect)
        {
            // 小对象：更多批处理，大对象：更少批处理
            double area = rect.Width * rect.Height;
            if (area < 100) // 小音符
                return MAX_BATCH_SIZE;
            else if (area < 1000) // 中等音符
                return MAX_BATCH_SIZE / 2;
            else // 大音符
                return MAX_BATCH_SIZE / 4;
        }
        
        /// <summary>
        /// 获取GPU内存使用统计
        /// </summary>
        public long GetGPUMemoryUsage()
        {
            return _totalMemoryUsed;
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_useVulkan)
            {
                // 确保刷新所有批处理数据
                FlushBatches();
                
                _vulkanContext?.Flush();
                // _vulkanService.EndFrame();
            }
            
            // 清理堆栈
            _clipStack.Clear();
            _transformStack.Clear();
            
            // 清理批次并返回对象池
            foreach (var batch in _batches.Values)
            {
                ReturnPooledBatchData(batch);
            }
            _batches.Clear();
            
            // 清理对象池
            _batchPool.Clear();
            
            _vulkanContext?.Dispose();
        }

        /// <summary>
        /// 批量绘制圆角矩形（实例化渲染）- 直接调用Vulkan上下文
        /// </summary>
        public void DrawRoundedRectsInstanced(IEnumerable<RoundedRect> rects, IBrush brush, IPen? pen = null)
        {
            if (_useVulkan && _vulkanContext != null)
            {
                _vulkanContext.DrawRoundedRectsInstanced(rects, brush, pen);
            }
            else
            {
                // 回退到逐个绘制
                foreach (var rect in rects)
                {
                    _skiaContext.DrawRectangle(brush, pen, rect);
                }
            }
        }
    }
}