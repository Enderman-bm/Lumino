using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumino.Services.Interfaces;
using Silk.NET.Vulkan;
using Avalonia;
using Avalonia.Media;

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
        public List<Avalonia.RoundedRect> RoundedRects { get; } = new List<Avalonia.RoundedRect>();
        public Avalonia.Media.IBrush? Brush { get; set; }
        public Avalonia.Media.IPen? Pen { get; set; }

        public void Add(Avalonia.RoundedRect rr)
        {
            RoundedRects.Add(rr);
        }
    }

    /// <summary>
    /// Vulkan渲染服务实现
    /// </summary>
    public class VulkanRenderService : IVulkanRenderService, IDisposable
    {
        private readonly ConcurrentQueue<Action> _renderCommandQueue = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _renderThread;
        private readonly VulkanConfiguration _configuration;
    private VulkanManager? _vulkanManager;
        private Lumino.Views.Rendering.Vulkan.VulkanRenderContext? _renderContext;
        private bool _initialized = false;
        private uint _currentFrameIndex = 0;

        /// <summary>
        /// 获取VulkanRenderService的单例实例
        /// </summary>
        public static VulkanRenderService Instance { get; } = new VulkanRenderService();

        public VulkanRenderService()
        {
            _configuration = VulkanConfiguration.Load();
            _renderThread = Task.Run(RenderLoop, _cancellationTokenSource.Token);
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
                    _vulkanManager.CreateSurface((void*)windowHandle);
                    _initialized = _vulkanManager.Initialize((void*)windowHandle);
                }
                // 初始化 VulkanRenderContext 以便其他组件可以访问 GPU 相关帮助方法
                if (_initialized)
                {
                    _renderContext = new Lumino.Views.Rendering.Vulkan.VulkanRenderContext(this);
                }
                return _initialized;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vulkan初始化失败: {ex.Message}");
                return false;
            }
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
                                System.Diagnostics.Debug.WriteLine($"处理 PreparedLineBatch 行时出错: {exLine.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"消费 PreparedLineBatch 时出错: {ex.Message}");
            }

            // 执行排队的渲染命令（包括上面生成的命令）
            ProcessRenderCommands();

            // 消费 PreparedRoundedRectBatch：先按 Brush+Pen 合并多个批次的实例，
            // 然后对每个合并组仅调用一次 DrawRoundedRectsInstanced 来减少 draw calls
            try
            {
                if (_renderContext != null)
                {
                    // 按 key 合并：key 由画刷（若为 SolidColorBrush 则使用颜色）和笔的属性组成
                    var groups = new Dictionary<string, (Avalonia.Media.IBrush brush, Avalonia.Media.IPen? pen, List<Avalonia.RoundedRect> rects)>();

                    string GetKey(Avalonia.Media.IBrush? brush, Avalonia.Media.IPen? pen)
                    {
                        var bkey = "null";
                        if (brush is Avalonia.Media.SolidColorBrush scb)
                            bkey = "SC:" + scb.Color.ToString();
                        else if (brush != null)
                            bkey = "B:" + brush.GetHashCode().ToString();

                        var pkey = "null";
                        if (pen != null)
                        {
                            var pt = pen.Thickness.ToString("F2");
                            var pbrush = pen.Brush is Avalonia.Media.SolidColorBrush psc ? psc.Color.ToString() : pen.Brush?.GetHashCode().ToString() ?? "null";
                            pkey = $"P:{pt}:{pbrush}";
                        }

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
                            var brush = rbatch.Brush ?? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Transparent);
                            var pen = rbatch.Pen;
                            var key = GetKey(brush, pen);

                            if (!groups.TryGetValue(key, out var entry))
                            {
                                entry = (brush, pen, new List<Avalonia.RoundedRect>());
                                groups[key] = entry;
                            }

                            entry.rects.AddRange(rbatch.RoundedRects);
                            // since tuple is a value type, update dictionary to be safe
                            groups[key] = (entry.brush, entry.pen, entry.rects);
                            totalInstances += rbatch.RoundedRects.Count;
                        }
                        catch (Exception exR)
                        {
                            System.Diagnostics.Debug.WriteLine($"处理 PreparedRoundedRectBatch 时出错: {exR.Message}");
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
                            System.Diagnostics.Debug.WriteLine($"提交合并批次时出错: {exDraw.Message}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[VulkanRenderService] PreparedRoundedRectBatch: dequeuedBatches={dequeuedBatches}, mergedGroups={groups.Count}, drawCalls={drawCalls}, totalInstances={totalInstances}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"消费 PreparedRoundedRectBatch 时出错: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine($"执行渲染命令时出错: {ex.Message}");
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
                // 这里可以处理后台渲染任务
                await Task.Delay(1, _cancellationTokenSource.Token); // 避免过度占用CPU
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
                _renderThread?.Wait(1000); // 等待最多1秒
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