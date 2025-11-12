# Vulkan音符渲染引擎 - 快速集成指南

## 5分钟快速开始

### 第1步：初始化引擎

```csharp
// 在编辑器启动时
public class EditorService
{
    private VulkanNoteRenderEngine _renderEngine;
    private PianoRollUIRenderer _uiRenderer;
    private RenderPerformanceMonitor _monitor;
    
    public void Initialize()
    {
        // 获取Vulkan实例
        var vk = Vk.GetApi();
        var device = VulkanRenderService.Instance.Device;
        var graphicsQueue = VulkanRenderService.Instance.GraphicsQueue;
        var commandPool = VulkanRenderService.Instance.CommandPool;
        var renderPass = VulkanRenderService.Instance.RenderPass;
        var pipeline = VulkanRenderService.Instance.Pipeline;
        var pipelineLayout = VulkanRenderService.Instance.PipelineLayout;
        
        // 创建渲染引擎
        _renderEngine = new VulkanNoteRenderEngine(
            vk, device, graphicsQueue, commandPool,
            renderPass, pipeline, pipelineLayout
        );
        
        // 创建UI渲染器
        _uiRenderer = new PianoRollUIRenderer(_renderEngine, vk, device);
        
        // 创建性能监测器
        _monitor = new RenderPerformanceMonitor(historySize: 300);
        
        // 应用标准钢琴颜色
        var colorConfig = new NoteColorConfiguration();
        colorConfig.ApplyStandardPianoColorScheme();
        _renderEngine.SetColorConfiguration(colorConfig);
    }
}
```

### 第2步：在渲染循环中使用

```csharp
public void RenderFrame(List<NoteDrawData> notes, double playheadX)
{
    _monitor.BeginFrame();
    
    // 创建渲染帧
    var frame = _renderEngine.BeginFrame();
    
    // 配置UI参数
    var gridConfig = new GridConfiguration
    {
        TimeGridSpacing = 10f,
        PitchGridSpacing = 5f,
    };
    _uiRenderer.ConfigureGrid(gridConfig);
    
    // 渲染UI组件
    _uiRenderer.RenderGrid(frame, 1920, 1080, 0, 100, 0, 128);
    _uiRenderer.RenderKeyboard(frame, 100, 1080, 0, 128);
    _uiRenderer.RenderPlayhead(frame, (float)playheadX, 1080);
    
    // 渲染音符（只渲染可见的）
    var visibleNotes = FilterVisibleNotes(notes, 0, 100, 0, 128);
    _renderEngine.DrawNotes(visibleNotes, frame);
    
    // 提交到GPU
    _renderEngine.SubmitFrame(frame, commandBuffer);
    
    // 清理帧
    _renderEngine.ClearFrame(frame);
    
    // 记录性能
    _monitor.RecordNoteCount(visibleNotes.Count);
    _monitor.EndFrame();
    
    // 定期输出性能报告
    if (frameCount % 60 == 0)  // 每秒一次@60fps
    {
        var report = _monitor.GetReport();
        Debug.WriteLine($"FPS: {report.AverageFPS:F1}");
    }
}

private List<NoteDrawData> FilterVisibleNotes(
    List<NoteDrawData> notes,
    float timeStart, float timeEnd,
    float pitchStart, float pitchEnd)
{
    return notes
        .Where(n => n.Position.X >= timeStart && n.Position.X <= timeEnd)
        .Where(n => n.Position.Y >= pitchStart && n.Position.Y <= pitchEnd)
        .ToList();
}
```

### 第3步：处理性能问题

```csharp
// 定期检查性能
public void PeriodicHealthCheck()
{
    if (_frameCount % 300 == 0)  // 每5秒检查一次@60fps
    {
        var advisor = new RenderOptimizationAdvisor(_monitor, 60.0);
        var suggestions = advisor.GetOptimizationSuggestions();
        
        if (suggestions.Count > 0)
        {
            EnderLogger.Instance.Warn("RenderHealth", 
                $"发现 {suggestions.Count} 个性能问题，详见日志");
            advisor.GenerateOptimizationReport();
        }
    }
}
```

---

## 数据结构速查

### 音符数据

```csharp
var note = new NoteDrawData(
    position: new Vector2(100, 200),    // 屏幕坐标
    width: 60f,                         // 像素
    height: 20f,                        // 像素
    radius: 5f,                         // 圆角
    pitch: 60,                          // MIDI音高 (0-127)
    velocity: 100,                      // MIDI速度 (0-127)
    channel: 0                          // MIDI通道 (0-15)
);
```

### 颜色配置

```csharp
var colorConfig = new NoteColorConfiguration();

// 选项1：使用标准方案
colorConfig.ApplyStandardPianoColorScheme();

// 选项2：自定义
colorConfig.SetPitchColor(0, new Vector4(1.0f, 0.2f, 0.2f, 1.0f));  // C - 红
colorConfig.SetPitchColor(1, new Vector4(1.0f, 0.6f, 0.2f, 1.0f));  // C# - 橙

// 设置默认颜色
colorConfig.SetDefaultColor(new Vector4(0.5f, 0.5f, 0.5f, 1.0f));

_renderEngine.SetColorConfiguration(colorConfig);
```

---

## 常见集成场景

### 场景1：单轨道渲染

```csharp
public void RenderSingleTrack(List<NoteDrawData> trackNotes)
{
    var frame = _renderEngine.BeginFrame();
    _renderEngine.DrawNotes(trackNotes, frame);
    _renderEngine.SubmitFrame(frame, commandBuffer);
}
```

### 场景2：多轨道混合渲染

```csharp
public void RenderMultipleTracks(Dictionary<int, List<NoteDrawData>> tracksByChannel)
{
    var frame = _renderEngine.BeginFrame();
    
    // 按通道渲染
    foreach (var (channel, notes) in tracksByChannel)
    {
        _renderEngine.DrawNotes(notes, frame);
    }
    
    _renderEngine.SubmitFrame(frame, commandBuffer);
}
```

### 场景3：实时音符编辑反馈

```csharp
public void RenderWithSelection(
    List<NoteDrawData> allNotes,
    List<NoteDrawData> selectedNotes)
{
    var frame = _renderEngine.BeginFrame();
    
    // 先渲染所有音符
    _renderEngine.DrawNotes(allNotes, frame);
    
    // 再渲染选中的（会覆盖前面的）
    var selectedConfig = new NoteColorConfiguration();
    selectedConfig.SetDefaultColor(new Vector4(1.0f, 1.0f, 0.0f, 1.0f));  // 黄色
    _renderEngine.SetColorConfiguration(selectedConfig);
    _renderEngine.DrawNotes(selectedNotes, frame);
    
    _renderEngine.SubmitFrame(frame, commandBuffer);
}
```

---

## 性能调优建议

### 基本优化（必做）

```csharp
// 1. 启用视口裁剪
var visibleNotes = notes
    .Where(n => IsInViewport(n, viewport))
    .ToList();
_renderEngine.DrawNotes(visibleNotes, frame);

// 2. 定期清空缓存
if (_frameCount % 1000 == 0)
{
    _renderEngine.ClearCache();
    GC.Collect();
}

// 3. 监测内存
if (_frameCount % 100 == 0)
{
    var used = GC.GetTotalMemory(false);
    EnderLogger.Instance.Debug("Memory", $"当前内存: {used / 1024 / 1024}MB");
}
```

### 中级优化（推荐）

```csharp
// 1. 按分组渲染
var byVelocity = notes.GroupBy(n => n.Velocity / 64);  // 分3组
foreach (var group in byVelocity)
{
    _renderEngine.DrawNotes(group.ToList(), frame);
}

// 2. 异步加载
await Task.Run(() =>
{
    var processed = PreprocessNotes(notes);
    _renderEngine.DrawNotes(processed, frame);
});

// 3. 帧率上限
if (DateTime.UtcNow - lastFrameTime < targetFrameDuration)
{
    Thread.Sleep(targetFrameDuration - (DateTime.UtcNow - lastFrameTime));
}
```

### 高级优化（可选）

```csharp
// 1. 动态LOD
int lodLevel = notes.Count > 10000 ? 2 : 1;
var filteredNotes = notes.Where((n, i) => i % lodLevel == 0).ToList();
_renderEngine.DrawNotes(filteredNotes, frame);

// 2. 预测加载
var nextViewport = PredictNextViewport();
PreloadNotesFor(nextViewport);

// 3. GPU内存管理
if (monitor.GetReport().LastFrameGPUMemoryUsed > 500_000_000)  // 500MB
{
    _renderEngine.ClearCache();
}
```

---

## 调试技巧

### 启用详细日志

```csharp
// 在监测器中启用详细模式
_monitor._verboseLogging = true;
_monitor._verboseLogFrameInterval = 30;  // 每30帧输出一次

// 运行后查看日志
_monitor.LogDetailedAnalysis("调试信息");
```

### 实时性能面板

```csharp
public string GetPerformanceOverlay()
{
    var report = _monitor.GetReport();
    
    return $@"
FPS: {report.AverageFPS:F1}
帧时间: {report.AverageFrameTime:F2}ms
P99: {report.P99FrameTime:F2}ms
音符: {report.AverageNoteCount:F0}
批次: {report.AverageBatchCount:F0}
内存: {report.LastFrameGPUMemoryUsed / 1024 / 1024:F0}MB
";
}
```

### 性能基准测试

```csharp
public async Task RunBenchmark()
{
    for (int i = 0; i < 300; i++)  // 5秒@60fps
    {
        _monitor.BeginFrame();
        
        // 模拟最坏情况
        var testNotes = GenerateTestNotes(20000);
        var frame = _renderEngine.BeginFrame();
        _renderEngine.DrawNotes(testNotes, frame);
        _renderEngine.SubmitFrame(frame, commandBuffer);
        
        _monitor.RecordNoteCount(testNotes.Count);
        _monitor.EndFrame();
        
        await Task.Delay(1);
    }
    
    _monitor.LogDetailedAnalysis("基准测试结果");
}
```

---

## 故障排查速查表

| 问题 | 症状 | 解决方案 |
|------|------|--------|
| FPS低 | <30fps | 启用视口裁剪；减少音符数 |
| 卡顿 | 偶发丢帧 | 检查P99；启用帧率上限 |
| 内存爆炸 | 持续增长 | 调用ClearCache()；检查泄漏 |
| 颜色异常 | 音符颜色错误 | 验证NoteColorConfiguration |
| 渲染错误 | 显示异常 | 检查Vulkan初始化；查看错误日志 |

---

## 集成清单

实际集成时逐项检查：

- [ ] VulkanNoteRenderEngine 已初始化
- [ ] PianoRollUIRenderer 已初始化
- [ ] RenderPerformanceMonitor 已初始化
- [ ] 颜色配置已应用
- [ ] 视口裁剪已启用
- [ ] 性能监测已集成
- [ ] 错误处理已完善
- [ ] 日志系统已连接
- [ ] 内存管理已验证
- [ ] 帧率测试已通过
- [ ] 不同分辨率已测试
- [ ] 文档已更新

---

## 相关文件

- `VULKAN_RENDER_ENGINE_GUIDE.md` - 完整使用指南
- `VULKAN_ARCHITECTURE.md` - 系统架构文档
- `VulkanRenderEngineExample.cs` - 完整示例代码

---

**快速参考版本**: 1.0  
**适用范围**: Lumino编辑器集成  
**难度级别**: ⭐⭐ (中等)
