using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Lumino.Services.Implementation;
using Silk.NET.Vulkan;
using EnderDebugger;

namespace Lumino.Rendering.Vulkan.Examples
{
    /// <summary>
    /// Vulkan音符渲染引擎集成示例
    /// 演示如何使用高性能Vulkan音符渲染系统
    /// </summary>
    public class VulkanRenderEngineExample : IDisposable
    {
        private VulkanNoteRenderEngine? _renderEngine;
        private PianoRollUIRenderer? _uiRenderer;
        private RenderPerformanceMonitor? _performanceMonitor;
        private RenderOptimizationAdvisor? _advisor;

        private List<NoteDrawData> _notes = new();
        private Random _random = new();

        // 配置参数
        private readonly float _viewWidth = 1920f;
        private readonly float _viewHeight = 1080f;
        private readonly float _keyboardWidth = 100f;

        public VulkanRenderEngineExample()
        {
            EnderLogger.Instance.Info("VulkanExample", "初始化Vulkan渲染引擎示例");
        }

        /// <summary>
        /// 初始化渲染引擎
        /// </summary>
        public bool Initialize()
        {
            try
            {
                // 获取Vulkan服务实例
                var vulkanService = VulkanRenderService.Instance;

                if (!vulkanService.IsInitialized)
                {
                    EnderLogger.Instance.Warn("VulkanExample", "Vulkan服务未初始化，尝试初始化");
                    if (!vulkanService.Initialize((nint)IntPtr.Zero))
                    {
                        EnderLogger.Instance.Error("VulkanExample", "Vulkan服务初始化失败");
                        return false;
                    }
                }

                // 初始化性能监测器
                _performanceMonitor = new RenderPerformanceMonitor(historySize: 300);
                _advisor = new RenderOptimizationAdvisor(_performanceMonitor, targetFPS: 60.0);

                EnderLogger.Instance.Info("VulkanExample", "渲染引擎示例初始化成功");
                return true;
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanExample", "初始化失败");
                return false;
            }
        }

        /// <summary>
        /// 生成示例音符数据
        /// </summary>
        public void GenerateSampleNotes(int noteCount = 1000)
        {
            _notes.Clear();

            for (int i = 0; i < noteCount; i++)
            {
                var note = new NoteDrawData(
                    position: new Vector2(
                        _random.Next((int)_viewWidth),
                        _random.Next((int)_viewHeight)
                    ),
                    width: 60f + (float)_random.NextDouble() * 40f,
                    height: 20f,
                    radius: 5f,
                    pitch: (byte)_random.Next(128),
                    velocity: (byte)(64 + _random.Next(64)),
                    channel: (byte)_random.Next(16)
                );

                _notes.Add(note);
            }

            EnderLogger.Instance.Info("VulkanExample", $"生成了 {noteCount} 个示例音符");
        }

        /// <summary>
        /// 演示基本渲染流程
        /// </summary>
        public void DemoBasicRendering()
        {
            EnderLogger.Instance.Info("VulkanExample", "演示：基本渲染流程");

            if (_renderEngine == null) return;

            _performanceMonitor?.BeginFrame();

            // 开始渲染帧
            var frame = _renderEngine.BeginFrame();

            // 配置颜色方案
            var colorConfig = new NoteColorConfiguration();
            colorConfig.ApplyStandardPianoColorScheme();
            _renderEngine.SetColorConfiguration(colorConfig);

            // 绘制所有音符
            _renderEngine.DrawNotes(_notes, frame);

            _performanceMonitor?.RecordNoteCount(_notes.Count);
            _performanceMonitor?.RecordBatchCount(10);

            // 清理帧
            _renderEngine.ClearFrame(frame);

            _performanceMonitor?.EndFrame();

            var stats = _performanceMonitor?.GetReport();
            EnderLogger.Instance.Info("VulkanExample", $"帧性能: {stats}");
        }

        /// <summary>
        /// 演示UI组件渲染
        /// </summary>
        public void DemoUIRendering()
        {
            EnderLogger.Instance.Info("VulkanExample", "演示：UI组件渲染");

            if (_uiRenderer == null) return;

            // 配置网格
            var gridConfig = new GridConfiguration
            {
                TimeGridSpacing = 10f,
                PitchGridSpacing = 5f,
                GridLineColor = new Vector4(0.3f, 0.3f, 0.3f, 0.3f),
                GridLineThickness = 0.5f,
                AccentGridLineColor = new Vector4(0.5f, 0.5f, 0.5f, 0.7f),
                AccentGridLineThickness = 1.0f
            };

            _uiRenderer.ConfigureGrid(gridConfig);

            // 配置键盘
            var keyboardConfig = new KeyboardConfiguration
            {
                WhiteKeyColor = new Vector4(1f, 1f, 1f, 1f),
                BlackKeyColor = new Vector4(0.1f, 0.1f, 0.1f, 1f),
                PressedKeyColor = new Vector4(1f, 0.5f, 0f, 1f)
            };

            _uiRenderer.ConfigureKeyboard(keyboardConfig);

            // 配置播放头
            var playheadConfig = new PlayheadConfiguration
            {
                PlayheadLineColor = new Vector4(1f, 0.2f, 0.2f, 1f),
                PlayheadLineThickness = 2f
            };

            _uiRenderer.ConfigurePlayhead(playheadConfig);

            // 创建渲染帧
            // var frame = new RenderFrame(_renderEngine);

            // 渲染各个组件
            // _uiRenderer.RenderGrid(frame, _viewWidth, _viewHeight, 0, 100, 0, 128);
            // _uiRenderer.RenderKeyboard(frame, _keyboardWidth, _viewHeight, 0, 128);
            // _uiRenderer.RenderPlayhead(frame, 500, _viewHeight);
            // _uiRenderer.RenderSelectionBox(frame, new Vector2(100, 100), new Vector2(500, 500));

            EnderLogger.Instance.Info("VulkanExample", "UI组件渲染完成");
        }

        /// <summary>
        /// 演示性能监测
        /// </summary>
        public void DemoPerformanceMonitoring()
        {
            EnderLogger.Instance.Info("VulkanExample", "演示：性能监测");

            if (_performanceMonitor == null) return;

            // 模拟多帧渲染
            for (int frame = 0; frame < 100; frame++)
            {
                _performanceMonitor.BeginFrame();

                // 模拟工作
                var startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < 5)
                {
                    // 模拟渲染工作
                }

                _performanceMonitor.RecordStageTime("Geometry Building", 1.2);
                _performanceMonitor.RecordStageTime("Batch Submission", 0.8);
                _performanceMonitor.RecordStageTime("GPU Sync", 0.3);
                _performanceMonitor.RecordNoteCount(1000 + frame * 10);
                _performanceMonitor.RecordBatchCount(10);

                _performanceMonitor.EndFrame();
            }

            // 输出性能报告
            _performanceMonitor.LogDetailedAnalysis("示例渲染性能");
        }

        /// <summary>
        /// 演示优化建议
        /// </summary>
        public void DemoOptimizationAdvisor()
        {
            EnderLogger.Instance.Info("VulkanExample", "演示：优化建议");

            if (_advisor == null) return;

            _advisor.GenerateOptimizationReport();
        }

        /// <summary>
        /// 演示批处理优化
        /// </summary>
        public void DemoBatchProcessing()
        {
            EnderLogger.Instance.Info("VulkanExample", "演示：批处理优化");

            if (_renderEngine == null) return;

            _performanceMonitor?.BeginFrame();

            // 按音符类型分组
            var notesByChannel = new Dictionary<byte, List<NoteDrawData>>();
            foreach (var note in _notes)
            {
                if (!notesByChannel.ContainsKey(note.Channel))
                {
                    notesByChannel[note.Channel] = new List<NoteDrawData>();
                }
                notesByChannel[note.Channel].Add(note);
            }

            // 批量渲染每个通道的音符
            var frame = _renderEngine.BeginFrame();

            int batchCount = 0;
            foreach (var channelNotes in notesByChannel.Values)
            {
                _renderEngine.DrawNotes(channelNotes, frame);
                batchCount++;
            }

            _performanceMonitor?.RecordBatchCount(batchCount);
            _performanceMonitor?.RecordNoteCount(_notes.Count);
            _performanceMonitor?.EndFrame();

            var stats = _performanceMonitor?.GetReport();
            EnderLogger.Instance.Info("VulkanExample", $"批处理完成: {batchCount} 批, {stats}");
        }

        /// <summary>
        /// 演示实时渲染循环
        /// </summary>
        public async Task DemoRealTimeRenderLoop()
        {
            EnderLogger.Instance.Info("VulkanExample", "演示：实时渲染循环（10秒）");

            if (_renderEngine == null) return;

            var startTime = DateTime.UtcNow;
            int frameCount = 0;

            while ((DateTime.UtcNow - startTime).TotalSeconds < 10)
            {
                _performanceMonitor?.BeginFrame();

                // 更新音符位置（模拟演奏进行）
                for (int i = 0; i < _notes.Count; i++)
                {
                    var note = _notes[i];
                    note.Position = new Vector2(
                        note.Position.X + 1f,
                        note.Position.Y + (float)(_random.NextDouble() - 0.5f)
                    );
                    _notes[i] = note;
                }

                // 渲染
                var frame = _renderEngine.BeginFrame();
                _renderEngine.DrawNotes(_notes, frame);
                _renderEngine.ClearFrame(frame);

                _performanceMonitor?.RecordNoteCount(_notes.Count);
                _performanceMonitor?.EndFrame();

                frameCount++;

                // 避免过度占用CPU
                await Task.Delay(1);
            }

            EnderLogger.Instance.Info("VulkanExample", $"渲染循环完成: {frameCount} 帧");
            _performanceMonitor?.LogDetailedAnalysis("实时渲染循环性能");
        }

        /// <summary>
        /// 运行所有演示
        /// </summary>
        public async Task RunAllDemos()
        {
            EnderLogger.Instance.Info("VulkanExample", "===== 开始Vulkan渲染引擎完整演示 =====");

            if (!Initialize())
            {
                EnderLogger.Instance.Error("VulkanExample", "初始化失败，停止演示");
                return;
            }

            GenerateSampleNotes(1000);

            try
            {
                // 运行各个演示
                DemoBasicRendering();
                await Task.Delay(1000);

                DemoUIRendering();
                await Task.Delay(1000);

                DemoPerformanceMonitoring();
                await Task.Delay(1000);

                DemoBatchProcessing();
                await Task.Delay(1000);

                await DemoRealTimeRenderLoop();
                await Task.Delay(1000);

                DemoOptimizationAdvisor();

                EnderLogger.Instance.Info("VulkanExample", "===== 所有演示完成 =====");
            }
            catch (Exception ex)
            {
                EnderLogger.Instance.LogException(ex, "VulkanExample", "演示执行出错");
            }
        }

        public void Dispose()
        {
            _renderEngine?.Dispose();
            _uiRenderer?.Dispose();
            _performanceMonitor?.Dispose();
        }
    }
}
