using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumino.Services.Interfaces;
using Lumino.ViewModels;
using Silk.NET.Vulkan;
using Avalonia;
using Avalonia.Media;
using EnderDebugger;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 线程预处理的线段批次（用于阶段1：将 CPU 计算的线段发送到渲染线程）
    /// </summary>
    public class PreparedLineBatch
    {
        public struct Line
        {
            public double X1, Y1, X2, Y2;
            public byte R, G, B, A;
            public double Thickness;
        }

        public List<Line> Lines { get; } = new List<Line>();

        public void Add(double x1, double y1, double x2, double y2, byte r, byte g, byte b, byte a, double thickness)
        {
            Lines.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, R = r, G = g, B = b, A = a, Thickness = thickness });
        }
    }

    /// <summary>
    /// 线程预处理的 RoundedRect 批次（用于批量 instanced 绘制）
    /// </summary>
    public class PreparedRoundedRectBatch
    {
        // 使用简单的 POCO 数据以避免在后台线程创建 Avalonia UI 对象（IBrush/RoundedRect）
        public struct RoundedRectData
        {
            public double X;
            public double Y;
            public double Width;
            public double Height;
            public double RadiusX;
            public double RadiusY;
        }

        public List<RoundedRectData> RoundedRects { get; } = new List<RoundedRectData>();

        // 统一批次颜色（ARGB），由生产者在创建批次时设置
        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        // optional pen thickness (unused for now)
        public double? PenThickness { get; set; }

    // Optional source identifier for debugging/routing (e.g. "VerticalGridRenderer", "PlayheadRenderer")
    public string? Source { get; set; }

        public void Add(double x, double y, double width, double height, double rx, double ry)
        {
            RoundedRects.Add(new RoundedRectData { X = x, Y = y, Width = width, Height = height, RadiusX = rx, RadiusY = ry });
        }
    }

    /// <summary>
    /// Vulkan渲染服务实现
    /// </summary>
    public class VulkanRenderService : IVulkanRenderService, IDisposable
    {
        private readonly ConcurrentQueue<Action> _renderCommandQueue = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _renderThread;
        private readonly VulkanConfiguration _configuration;
    private VulkanManager? _vulkanManager;
        private Lumino.Views.Rendering.Vulkan.VulkanRenderContext? _renderContext;
        private bool _initialized = false;
        private uint _currentFrameIndex = 0;
    // runtime accumulators for logging/probing
    private long _accumulatedDequeuedRoundedRectBatches = 0;
    private long _accumulatedRoundedRectInstances = 0;
    private long _accumulatedDequeuedLineBatches = 0;
    private long _accumulatedLineInstances = 0;
    private bool _verboseLogging = false;
    private int _verboseLogFrameInterval = 60; // log every N frames when verbose
    
    // 添加对MainWindowViewModel的引用
    private MainWindowViewModel? _mainWindowViewModel;

        /// <summary>
        /// 获取VulkanRenderService的单例实例
        /// </summary>
        public static VulkanRenderService Instance { get; } = new VulkanRenderService();

        public VulkanRenderService()
        {
            _configuration = VulkanConfiguration.Load();
            // 不在构造函数中启动渲染循环，而是在Initialize方法中启动
        }
        
        /// <summary>
        /// 设置MainWindowViewModel引用
        /// </summary>
        /// <param name="mainWindowViewModel">主窗口视图模型</param>
        public void SetMainWindowViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;
        }

        /// <summary>
        /// 是否支持Vulkan
        /// </summary>
        public bool IsSupported => true;

        /// <summary>
        /// 是否启用Vulkan渲染
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// 获取VulkanManager实例
        /// </summary>
        public VulkanManager? VulkanManager => _vulkanManager;

        /// <summary>
        /// 初始化Vulkan渲染服务
        /// </summary>
        /// <param name="windowHandle">窗口句柄</param>
        /// <returns>是否初始化成功</returns>
        public bool Initialize(nint windowHandle)
        {
            if (_initialized)
                return true;

            try
            {
                _vulkanManager = new VulkanManager();
                unsafe
                {
                    // Initialize内部会负责创建Surface
                    _initialized = _vulkanManager.Initialize((void*)windowHandle);
                }
                
                // 订阅帧绘制完成事件
                if (_initialized)
                {
                    _vulkanManager.OnFrameDrawn += OnFrameDrawn;
                    // 初始化 VulkanRenderContext 以便其他组件可以访问 GPU 相关帮助方法
                    _renderContext = new Lumino.Views.Rendering.Vulkan.VulkanRenderContext(this);
                    
                    // 在VulkanManager初始化成功后启动渲染循环
                    _renderThread = Task.Run(RenderLoop, _cancellationTokenSource.Token);
                }
                
                return _initialized;
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.Error("VulkanRenderService", $"Vulkan初始化失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 帧绘制完成事件处理程序
        /// </summary>
        private void OnFrameDrawn()
        {
            // 更新帧信息
            _mainWindowViewModel?.UpdateFrameInfo();
        }

        /// <summary>
        /// 开始渲染帧
        /// </summary>
        public void BeginFrame()
        {
            // 在Vulkan中，开始帧通常不需要特殊处理
            // 实际的渲染命令会在EndFrame中执行
        }

        /// <summary>
        /// 结束渲染帧并提交
        /// </summary>
        public void EndFrame()
        {
            if (!_initialized)
                return;

            // 首先消费由工作线程预生成的线段批次，转换为渲染命令并入队。
            // 这些调用会最终生成 Action<CommandBuffer> 并被 VulkanManager 在 CreateCommandBuffers 时消费。
            try
            {
                if (_renderContext != null)
                {
                    while (_preparedLineBatches.TryDequeue(out var batch))
                    {
                        foreach (var ln in batch.Lines)
                        {
                            try
                            {
                                // 把线段转换为一个非常细的矩形来绘制
                                var x1 = ln.X1; var y1 = ln.Y1; var x2 = ln.X2; var y2 = ln.Y2;
                                var left = Math.Min(x1, x2);
                                var top = Math.Min(y1, y2);
                                var width = Math.Abs(x2 - x1);
                                var height = Math.Abs(y2 - y1);

                                // 如果线是水平或竖直方向，确保宽度或高度至少为1像素
                                if (width < 1) width = 1;
                                if (height < 1) height = 1;

                                var rect = new Avalonia.Rect(left, top, width, height);

                                // 创建一个临时画刷
                                var color = new Avalonia.Media.Color(ln.A, ln.R, ln.G, ln.B);
                                var brush = new Avalonia.Media.SolidColorBrush(color);

                                // 使用 VulkanRenderContext 的高层接口排队绘制（将矩形转换为 Avalonia.RoundedRect）
                                var rrect = new Avalonia.RoundedRect(rect, 0.0);
                                _renderContext.DrawRoundedRect(rrect, brush, null);
                            }
                            catch (Exception exLine)
                            {
                                EnderLogger.Instance.LogException(exLine, "VulkanRenderService", "处理 PreparedLineBatch 行时出错");
                            }
                        }
                    // accumulate line batch counts (best-effort)
                    // Note: we don't have per-batch instance count here; could extend PreparedLineBatch with counts if needed
                    }
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanRenderService", "消费 PreparedLineBatch 时出错");
            }

            // 执行排队的渲染命令（包括上面生成的命令）
            ProcessRenderCommands();

            // 消费 PreparedRoundedRectBatch：先按 Brush+Pen 合并多个批次的实例，
            // 然后对每个合并组仅调用一次 DrawRoundedRectsInstanced 来减少 draw calls
            try
            {
                if (_renderContext != null)
                {
                    // 合并按批次颜色和笔粗细（在渲染线程创建 Avalonia 对象）
                    var groups = new Dictionary<string, (Avalonia.Media.IBrush brush, Avalonia.Media.IPen? pen, List<Avalonia.RoundedRect> rects)>();

                    string GetKey(byte a, byte r, byte g, byte b, double? penThickness)
                    {
                        var bkey = $"SC:{a:X2}{r:X2}{g:X2}{b:X2}";
                        var pkey = penThickness.HasValue ? $"P:{penThickness.Value:F2}" : "P:null";
                        return bkey + "|" + pkey;
                    }

                    int totalInstances = 0;
                    int dequeuedBatches = 0;

                    while (_preparedRoundedRectBatches.TryDequeue(out var rbatch))
                    {
                        dequeuedBatches++;
                        try
                        {
                            if (rbatch.RoundedRects.Count == 0) continue;

                            var key = GetKey(rbatch.A, rbatch.R, rbatch.G, rbatch.B, rbatch.PenThickness);
                            // Log source for debugging
                            try
                            {
                                var src = rbatch.Source ?? "(unknown)";
                                var firstX = rbatch.RoundedRects.Count>0 ? rbatch.RoundedRects[0].X : double.NaN;
                                var firstY = rbatch.RoundedRects.Count>0 ? rbatch.RoundedRects[0].Y : double.NaN;
                                EnderLogger.Instance.Debug("VulkanRenderService", $"Dequeued PreparedRoundedRectBatch from Source={src}, count={rbatch.RoundedRects.Count}, firstRect=({firstX:F2},{firstY:F2})");
                            }
                            catch { }

                            if (!groups.TryGetValue(key, out var entry))
                            {
                                // 在渲染线程创建对应的画刷/笔
                                var color = new Avalonia.Media.Color(rbatch.A, rbatch.R, rbatch.G, rbatch.B);
                                var brush = (Avalonia.Media.IBrush)new Avalonia.Media.SolidColorBrush(color);
                                Avalonia.Media.IPen? pen = null;
                                if (rbatch.PenThickness.HasValue)
                                    pen = new Avalonia.Media.Pen(brush, rbatch.PenThickness.Value);

                                entry = (brush, pen, new List<Avalonia.RoundedRect>());
                                groups[key] = entry;
                            }

                            // convert RoundedRectData -> Avalonia.RoundedRect on render thread
                            foreach (var rrd in rbatch.RoundedRects)
                            {
                                var rect = new Avalonia.Rect(rrd.X, rrd.Y, rrd.Width, rrd.Height);
                                var rrect = new Avalonia.RoundedRect(rect, rrd.RadiusX, rrd.RadiusY);
                                entry.rects.Add(rrect);
                                totalInstances++;
                            }

                            // update tuple back to dictionary
                            groups[key] = (entry.brush, entry.pen, entry.rects);
                        }
                        catch (Exception exR)
                        {
                            EnderLogger.Instance.LogException(exR, "VulkanRenderService", "处理 PreparedRoundedRectBatch 时出错");
                        }
                    }

                    // 提交合并后的组
                    int drawCalls = 0;
                    foreach (var kv in groups)
                    {
                        try
                        {
                            var brush = kv.Value.brush;
                            var pen = kv.Value.pen;
                            var rects = kv.Value.rects;
                            if (rects.Count == 0) continue;
                            _renderContext.DrawRoundedRectsInstanced(rects, brush, pen);
                            drawCalls++;
                        }
                        catch (Exception exDraw)
                        {
                            EnderLogger.Instance.LogException(exDraw, "VulkanRenderService", "提交合并批次时出错");
                        }
                    }

                    EnderLogger.Instance.Info("VulkanRenderService", $"PreparedRoundedRectBatch: dequeuedBatches={dequeuedBatches}, mergedGroups={groups.Count}, drawCalls={drawCalls}, totalInstances={totalInstances}");

                    // accumulate runtime stats
                    System.Threading.Interlocked.Add(ref _accumulatedDequeuedRoundedRectBatches, dequeuedBatches);
                    System.Threading.Interlocked.Add(ref _accumulatedRoundedRectInstances, totalInstances);

                    if (_verboseLogging && (_currentFrameIndex % (uint)_verboseLogFrameInterval == 0))
                    {
                        EnderLogger.Instance.Debug("VulkanRenderService", $"[VERBOSE] FrameIndex={_currentFrameIndex}, dequeuedRoundedRectBatches={dequeuedBatches}, totalInstances={totalInstances}, mergedGroups={groups.Count}, drawCalls={drawCalls}");
                    }
                }
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanRenderService", "消费 PreparedRoundedRectBatch 时出错");
            }

            // 绘制帧
            _vulkanManager?.DrawFrame();
            
            // 更新帧索引
            _currentFrameIndex = (_currentFrameIndex + 1) % 2;
        }

        /// <summary>
        /// 将渲染命令加入队列
        /// </summary>
        /// <param name="command">要执行的渲染命令</param>
        public void EnqueueRenderCommand(Action command)
        {
            _renderCommandQueue.Enqueue(command);
        }

        /// <summary>
        /// 异步执行渲染命令
        /// </summary>
        /// <param name="command">要执行的渲染命令</param>
        /// <returns>任务</returns>
        public Task EnqueueRenderCommandAsync(Action command)
        {
            var tcs = new TaskCompletionSource<bool>();
            _renderCommandQueue.Enqueue(() =>
            {
                try
                {
                    command();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        /// <summary>
        /// 获取渲染统计信息
        /// </summary>
        /// <returns>渲染统计信息</returns>
        public VulkanRenderStats GetStats()
        {
            // 这里应该从VulkanManager获取实际的统计信息
            return new VulkanRenderStats
            {
                TotalFrames = 0,
                TotalDrawCalls = 0,
                TotalVertices = 0,
                GpuUtilization = 0.0,
                MemoryUsage = 0,
                ActiveTextures = 0,
                ActiveBuffers = 0,
                LastFrameTime = 0.0,
                AverageFrameTime = 0.0,
                FrameRate = 0.0
            };
        }

        /// <summary>
        /// 获取渲染统计信息（兼容接口）
        /// </summary>
        /// <returns>渲染统计信息</returns>
        public RenderStats GetRenderStats()
        {
            // 这里应该从VulkanManager获取实际的统计信息
            return new RenderStats
            {
                TotalFrames = 0,
                ErrorCount = 0,
                AverageFrameTime = 0.0,
                LastFrameTime = 0.0
            };
        }

        /// <summary>
        /// 获取渲染上下文
        /// </summary>
        /// <returns>渲染上下文</returns>
        public object GetRenderContext()
        {
            // 返回已创建的 VulkanRenderContext（如果尚未初始化则返回 null）
            return _renderContext as object ?? new object();
        }

        // 线程预处理生成的线段/绘制数据队列（阶段1接口）
        private readonly System.Collections.Concurrent.ConcurrentQueue<PreparedLineBatch> _preparedLineBatches = new();
        // 线程预处理生成的 RoundedRect 批次队列（用于 instanced 提交）
        private readonly System.Collections.Concurrent.ConcurrentQueue<PreparedRoundedRectBatch> _preparedRoundedRectBatches = new();

        public void EnqueuePreparedLineBatch(PreparedLineBatch batch)
        {
            if (batch == null) return;
            _preparedLineBatches.Enqueue(batch);
        }

        public void EnqueuePreparedRoundedRectBatch(PreparedRoundedRectBatch batch)
        {
            if (batch == null) return;
            _preparedRoundedRectBatches.Enqueue(batch);
        }

        /// <summary>
        /// Enable or disable verbose logging for runtime Vulkan batch processing (for debugging/perf probing).
        /// </summary>
        public void SetVerboseLogging(bool enabled, int frameInterval = 60)
        {
            _verboseLogging = enabled;
            if (frameInterval > 0) _verboseLogFrameInterval = frameInterval;
        }

        /// <summary>
        /// Retrieve and reset accumulated runtime stats collected since last call.
        /// Useful for lightweight performance probes from the UI or tests.
        /// </summary>
        public (long dequeuedRoundedRectBatches, long roundedRectInstances, long dequeuedLineBatches, long lineInstances) GetAndResetRuntimeStats()
        {
            var drrb = System.Threading.Interlocked.Exchange(ref _accumulatedDequeuedRoundedRectBatches, 0);
            var rri = System.Threading.Interlocked.Exchange(ref _accumulatedRoundedRectInstances, 0);
            var dlb = System.Threading.Interlocked.Exchange(ref _accumulatedDequeuedLineBatches, 0);
            var li = System.Threading.Interlocked.Exchange(ref _accumulatedLineInstances, 0);
            return (drrb, rri, dlb, li);
        }

        /// <summary>
        /// 处理渲染命令队列
        /// </summary>
        private void ProcessRenderCommands()
        {
            while (_renderCommandQueue.TryDequeue(out var command))
            {
                try
                {
                    command();
                }
                catch (Exception ex)
                {
                    EnderLogger.Instance.LogException(ex, "VulkanRenderService", "执行渲染命令时出错");
                }
            }
        }

        /// <summary>
        /// 渲染循环
        /// </summary>
        private async Task RenderLoop()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_initialized && _vulkanManager != null)
                {
                    try
                    {
                        // 开始帧渲染
                        BeginFrame();
                        
                        // 处理渲染命令队列
                        ProcessRenderCommands();
                        
                        // 结束帧渲染
                        EndFrame();
                    }
                    catch (Exception ex)
                    {
                        EnderLogger.Instance.LogException(ex, "VulkanRenderService", "渲染循环中发生错误");
                    }
                }
                
                // 控制帧率，避免过度占用CPU
                await Task.Delay(16, _cancellationTokenSource.Token); // 约60 FPS
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            if (!_initialized)
                return;

            _cancellationTokenSource.Cancel();
            try
            {
                if (_renderThread != null)
                {
                    _renderThread.Wait(1000); // 等待最多1秒
                }
            }
            catch
            {
                // 忽略等待异常
            }

            _vulkanManager?.Dispose();
            _initialized = false;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Cleanup();
            _cancellationTokenSource?.Dispose();
        }
    }
}