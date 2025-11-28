using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core = LuminoRenderEngine.Core;
using LuminoRenderEngine.Performance;
using EnderDebugger;
using LuminoRenderEngine.Vulkan;

namespace LuminoRenderEngine.Examples
{
    /// <summary>
    /// LuminoRenderEngine 集成示例
    /// </summary>
    public class LuminoRenderEngineExample
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private Core.LuminoRenderEngine? _engine;

        /// <summary>
        /// 示例7: Vulkan初始化测试
        /// </summary>
        public async Task DemoVulkanInitialization()
        {
            _logger.Info("LuminoRenderEngineExample", "示例7: Vulkan初始化测试");
            
            try
            {
                // 启用调试模式以查看详细日志
                EnderLogger.Instance.EnableDebugMode("debug");

                // 创建并初始化Vulkan渲染管理器
                var renderManager = new VulkanRenderManager();
                
                _logger.Info("VulkanTest", "正在初始化Vulkan...");
                renderManager.Initialize();

                if (renderManager.IsInitialized && renderManager.Context != null && renderManager.Context.IsValid)
                {
                    _logger.Info("VulkanTest", "✓ Vulkan初始化成功！");
                    
                    // 显示一些设备信息
                    var vk = Silk.NET.Vulkan.Vk.GetApi();
                    var props = vk.GetPhysicalDeviceProperties(renderManager.Context.PhysicalDevice);
                    var deviceName = Silk.NET.Core.SilkMarshal.PtrToString(new IntPtr(props.DeviceName));
                    
                    _logger.Info("VulkanTest", $"设备名称: {deviceName}");
                    _logger.Info("VulkanTest", $"设备类型: {props.DeviceType}");
                    _logger.Info("VulkanTest", $"Vulkan版本: {props.ApiVersion}");
                    _logger.Info("VulkanTest", $"驱动版本: {props.DriverVersion}");
                    
                    Console.WriteLine("✓ Vulkan初始化成功！");
                    Console.WriteLine($"  - 设备名称: {deviceName}");
                    Console.WriteLine($"  - 设备类型: {props.DeviceType}");
                    Console.WriteLine($"  - Vulkan版本: {props.ApiVersion}");
                    Console.WriteLine($"  - 驱动版本: {props.DriverVersion}");
                }
                else
                {
                    _logger.Error("VulkanTest", "✗ Vulkan初始化失败或上下文无效");
                    Console.WriteLine("✗ Vulkan初始化失败！");
                }

                // 清理资源
                renderManager.Dispose();
                
                _logger.Info("VulkanTest", "✅ Vulkan初始化测试完成");
            }
            catch (Exception ex)
            {
                _logger.Error("VulkanTest", $"✗ Vulkan初始化测试失败: {ex.Message}");
                _logger.LogException(ex, "VulkanTest", "Vulkan初始化异常");
                Console.WriteLine($"✗ Vulkan初始化测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 示例1: 基础初始化和音符添加
        /// </summary>
        public void DemoBasicUsage()
        {
            _logger.Info("LuminoRenderEngineExample", "示例1: 基础使用");

            // 创建并初始化引擎
            _engine = new Core.LuminoRenderEngine();
            var config = new Core.RenderEngineConfig
            {
                MaxNotesPerBatch = 5000,
                EnableGpuCaching = true
            };
            _engine.Initialize(config);

            // 添加一些音符
            var notes = GenerateSampleNotes(100);
            _engine.AddNotes(notes);

            _logger.Info("LuminoRenderEngineExample", $"✅ 添加了 {_engine.TotalNotes} 个音符");

            _engine.Dispose();
        }

        /// <summary>
        /// 示例2: 高性能查询
        /// </summary>
        public void DemoPerformanceQueries()
        {
            _logger.Info("LuminoRenderEngineExample", "示例2: 高性能查询");

            _engine = new Core.LuminoRenderEngine();
            _engine.Initialize();

            // 添加大量音符
            var notes = GenerateSampleNotes(50000);
            _engine.AddNotes(notes);

            _logger.Info("LuminoRenderEngineExample", $"添加了 {_engine.TotalNotes} 个音符");

            // 进行几次查询并测量性能
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 查询时间范围内的音符 - O(log n)
            var visibleNotes1 = _engine.QueryVisibleNotes(0, 10, 30, 90);
            _logger.Info("LuminoRenderEngineExample", 
                $"查询1 (0-10秒, 30-90音高): {visibleNotes1.Count} 个音符");

            var visibleNotes2 = _engine.QueryVisibleNotes(5, 15, 0, 127);
            _logger.Info("LuminoRenderEngineExample", 
                $"查询2 (5-15秒, 全音高): {visibleNotes2.Count} 个音符");

            var visibleNotes3 = _engine.QueryVisibleNotes(0, 5, 60, 72);
            _logger.Info("LuminoRenderEngineExample", 
                $"查询3 (0-5秒, 60-72音高): {visibleNotes3.Count} 个音符");

            sw.Stop();
            _logger.Info("LuminoRenderEngineExample", 
                $"✅ 3次查询完成，耗时: {sw.ElapsedMilliseconds}ms");

            _engine.Dispose();
        }

        /// <summary>
        /// 示例3: 渲染循环
        /// </summary>
        public async Task DemoRenderLoop()
        {
            _logger.Info("LuminoRenderEngineExample", "示例3: 渲染循环");

            _engine = new Core.LuminoRenderEngine();
            _engine.Initialize();

            // 添加音符
            var notes = GenerateSampleNotes(1000);
            _engine.AddNotes(notes);

            // 模拟10帧的渲染循环
            for (int frame = 0; frame < 10; frame++)
            {
                double deltaTime = 0.016; // 60 FPS

                await _engine.RenderFrameAsync(deltaTime, recorder =>
                {
                    // 这里会执行GPU渲染命令
                    recorder.BeginRenderPass();
                    
                    // 查询当前可见的音符
                    var visibleNotes = _engine.QueryVisibleNotes(
                        _engine.CurrentTime, 
                        _engine.CurrentTime + 1.0,
                        0, 127);
                    
                    recorder.DrawNotes(visibleNotes);
                    recorder.EndRenderPass();
                });

                if (frame % 3 == 0)
                {
                    _logger.Debug("LuminoRenderEngineExample", 
                        $"渲染帧 {frame}: 时间={_engine.CurrentTime:F3}s");
                }
            }

            _logger.Info("LuminoRenderEngineExample", 
                $"✅ 渲染循环完成 ({_engine.FrameCount} 帧)");

            _engine.Dispose();
        }

        /// <summary>
        /// 示例4: 性能监控
        /// </summary>
        public void DemoPerformanceMonitoring()
        {
            _logger.Info("LuminoRenderEngineExample", "示例4: 性能监控");

            _engine = new Core.LuminoRenderEngine();
            _engine.Initialize();

            // 添加音符
            var notes = GenerateSampleNotes(10000);
            _engine.AddNotes(notes);

            // 执行渲染帧
            for (int i = 0; i < 50; i++)
            {
                _engine.BeginFrame(0.016);
                
                // 模拟查询
                var visible = _engine.QueryVisibleNotes(
                    _engine.CurrentTime, 
                    _engine.CurrentTime + 2.0,
                    0, 127);
                
                _engine.EndFrame();
            }

            // 获取性能报告
            var report = _engine.GetPerformanceReport();
            _logger.Info("LuminoRenderEngineExample", $"平均FPS: {report.AverageFps:F1}");
            _logger.Info("LuminoRenderEngineExample", $"平均帧时间: {report.FrameTimeMs:F2}ms");
            _logger.Info("LuminoRenderEngineExample", $"GPU内存使用: {report.GpuMemoryUsageMb}MB");

            // 获取优化建议
            var suggestions = _engine.GetOptimizationSuggestions();
            if (suggestions.Count > 0)
            {
                _logger.Info("LuminoRenderEngineExample", "优化建议:");
                foreach (var suggestion in suggestions)
                {
                    _logger.Info("LuminoRenderEngineExample", 
                        $"  - {suggestion.Title} ({suggestion.Severity})");
                }
            }
            else
            {
                _logger.Info("LuminoRenderEngineExample", "✅ 性能良好，无优化建议");
            }

            _engine.Dispose();
        }

        /// <summary>
        /// 示例5: 轨道管理
        /// </summary>
        public void DemoTrackManagement()
        {
            _logger.Info("LuminoRenderEngineExample", "示例5: 轨道管理");

            _engine = new Core.LuminoRenderEngine();
            _engine.Initialize();

            // 为不同的轨道添加音符
            for (int track = 0; track < 5; track++)
            {
                var trackNotes = GenerateSampleNotes(100, track);
                _engine.AddNotes(trackNotes);

                // 配置轨道渲染属性
                var trackState = _engine.GetOrCreateTrack(track);
                trackState.IsVisible = true;
                trackState.Opacity = 1.0f;
                trackState.Color = new System.Numerics.Vector4(
                    (track + 1) / 5.0f, 0.5f, 0.8f, 1.0f);
            }

            _logger.Info("LuminoRenderEngineExample", $"配置了5个轨道，总共{_engine.TotalNotes}个音符");

            // 获取所有活跃轨道
            var activeTracks = _engine.GetActiveTracks();
            _logger.Info("LuminoRenderEngineExample", $"活跃轨道数: {activeTracks.Count()}");

            // 更新轨道属性
            _engine.UpdateTrackProperty(0, "instrument", "Piano");
            _logger.Info("LuminoRenderEngineExample", "✅ 轨道管理示例完成");

            _engine.Dispose();
        }

        /// <summary>
        /// 示例6: 完整集成示例
        /// </summary>
        public async Task DemoIntegrationComplete()
        {
            _logger.Info("LuminoRenderEngineExample", "示例6: 完整集成示例");

            using (_engine = new Core.LuminoRenderEngine())
            {
                // 初始化
                var config = new Core.RenderEngineConfig
                {
                    MaxNotesPerBatch = 8000,
                    EnableGpuCaching = true,
                    EnableDynamicLOD = true
                };
                _engine.Initialize(config);

                // 生成大量音符
                var allNotes = GenerateSampleNotes(100000);
                _engine.AddNotes(allNotes);
                _logger.Info("LuminoRenderEngineExample", 
                    $"加载了 {_engine.TotalNotes} 个音符");

                // 渲染循环
                const int FRAME_COUNT = 300;
                var startTime = DateTime.Now;

                for (int frame = 0; frame < FRAME_COUNT; frame++)
                {
                    double deltaTime = 0.016; // 60 FPS目标

                    await _engine.RenderFrameAsync(deltaTime, recorder =>
                    {
                        recorder.BeginRenderPass();
                        
                        // 查询可见音符
                        var visible = _engine.QueryVisibleNotes(
                            _engine.CurrentTime,
                            _engine.CurrentTime + 2.0,
                            0, 127);
                        
                        recorder.DrawNotes(visible);
                        recorder.EndRenderPass();
                    });
                }

                var elapsed = DateTime.Now - startTime;
                
                // 性能统计
                var report = _engine.GetPerformanceReport();
                _logger.Info("LuminoRenderEngineExample", "=== 最终性能统计 ===");
                _logger.Info("LuminoRenderEngineExample", 
                    $"渲染帧数: {_engine.FrameCount}");
                _logger.Info("LuminoRenderEngineExample", 
                    $"平均FPS: {report.AverageFps:F2}");
                _logger.Info("LuminoRenderEngineExample", 
                    $"平均帧时间: {report.FrameTimeMs:F3}ms");
                _logger.Info("LuminoRenderEngineExample", 
                    $"总耗时: {elapsed.TotalSeconds:F2}秒");
                _logger.Info("LuminoRenderEngineExample", 
                    $"✅ 完整集成示例完成");
            }
        }

        /// <summary>
        /// 运行所有示例
        /// </summary>
        public async Task RunAllExamples()
        {
            _logger.Info("LuminoRenderEngineExample", 
                "======================================");
            _logger.Info("LuminoRenderEngineExample", 
                "LuminoRenderEngine 示例套件");
            _logger.Info("LuminoRenderEngineExample", 
                "======================================");

            try
            {
                DemoBasicUsage();
                _logger.Info("LuminoRenderEngineExample", "");

                DemoPerformanceQueries();
                _logger.Info("LuminoRenderEngineExample", "");

                await DemoRenderLoop();
                _logger.Info("LuminoRenderEngineExample", "");

                DemoPerformanceMonitoring();
                _logger.Info("LuminoRenderEngineExample", "");

                DemoTrackManagement();
                _logger.Info("LuminoRenderEngineExample", "");

                await DemoIntegrationComplete();
                
                // 运行 Vulkan 初始化测试
                await DemoVulkanInitialization();
                _logger.Info("LuminoRenderEngineExample", "");

                _logger.Info("LuminoRenderEngineExample", 
                    "======================================");
                _logger.Info("LuminoRenderEngineExample", 
                    "✅ 所有示例运行完毕");
                _logger.Info("LuminoRenderEngineExample", 
                    "======================================");
            }
            catch (Exception ex)
            {
                _logger.Error("LuminoRenderEngineExample", "示例执行出错");
                _logger.LogException(ex);
            }
        }

        /// <summary>
        /// 主入口点
        /// </summary>
        public static async Task Main(string[] args)
        {
            var example = new LuminoRenderEngineExample();
            
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "vulkan":
                        await example.DemoVulkanInitialization();
                        break;
                    case "all":
                        await example.RunAllExamples();
                        break;
                    default:
                        Console.WriteLine("可用示例:");
                        Console.WriteLine("  - 'vulkan': 运行 Vulkan 初始化测试");
                        Console.WriteLine("  - 'all': 运行所有示例");
                        Console.WriteLine("  - 默认: 运行 Vulkan 初始化测试");
                        await example.DemoVulkanInitialization();
                        break;
                }
            }
            else
            {
                // 默认运行 Vulkan 测试
                await example.DemoVulkanInitialization();
            }
        }

        #region 辅助方法
        private List<Core.NoteRenderInfo> GenerateSampleNotes(int count, int? trackOverride = null)
        {
            var notes = new List<Core.NoteRenderInfo>();
            var random = new Random(42);

            for (int i = 0; i < count; i++)
            {
                notes.Add(new Core.NoteRenderInfo
                {
                    StartTime = random.NextDouble() * 60.0,
                    Duration = random.NextDouble() * 2.0 + 0.1,
                    Pitch = random.Next(0, 128),
                    Velocity = random.Next(0, 128),
                    Channel = random.Next(0, 16),
                    TrackIndex = trackOverride ?? random.Next(0, 10),
                });
            }

            return notes;
        }
        #endregion
    }
}
