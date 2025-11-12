# Vulkan音符渲染引擎架构文档

## 系统架构概览

```
┌─────────────────────────────────────────────────────────┐
│             Lumino 编辑器主程序                          │
│  (EditorViewModel / MainWindow / PianoRollControl)       │
└────────────────────┬────────────────────────────────────┘
                     │
         ┌───────────┴────────────┐
         │                        │
    ┌────▼─────────────────┐ ┌──▼─────────────────┐
    │ Rendering System     │ │ Services Layer     │
    │ (XAML/Avalonia)      │ │ (Audio, MIDI, etc) │
    └────┬─────────────────┘ └──┬─────────────────┘
         │                       │
         └───────────┬───────────┘
                     │
         ┌───────────▼──────────────────────┐
         │  VulkanNoteRenderEngine          │
         │ - 音符绘制管理                   │
         │ - 批处理优化                     │
         │ - 颜色配置                       │
         └───────────┬──────────────────────┘
                     │
         ┌───────────┴─────────────────────────┐
         │                                     │
    ┌────▼──────────────┐  ┌────────────────▼──┐
    │ PianoRollUI       │  │ UI Renderers       │
    │ Renderer          │  │ - LineRenderer     │
    │ - Grid Render     │  │ - RectRenderer     │
    │ - Keyboard        │  │ - TextRenderer     │
    │ - Playhead        │  └────────────────────┘
    │ - SelectionBox    │
    └────┬──────────────┘
         │
         └─────────┬──────────────┐
                   │              │
        ┌──────────▼──┐  ┌───────▼───────┐
        │  Performance │  │ Optimization  │
        │  Monitor     │  │ Advisor       │
        │ - Stats      │  │ - Analysis    │
        │ - Analysis   │  │ - Suggestions │
        └──────────────┘  └───────────────┘
                   │
                   │
         ┌─────────▼──────────────┐
         │  Vulkan Rendering      │
         │  Backend               │
         │ (VulkanManager)        │
         │ - Command Buffers      │
         │ - Pipelines            │
         │ - Memory Management    │
         └────────┬───────────────┘
                  │
         ┌────────▼─────────────┐
         │   GPU Memory         │
         │ - Vertex Buffers     │
         │ - Index Buffers      │
         │ - Texture Memory     │
         └──────────────────────┘
```

## 核心类设计

### 1. VulkanNoteRenderEngine

**职责**：管理音符的整个渲染生命周期

**主要成员**：

```
VulkanNoteRenderEngine
├── _geometryCache: NoteGeometryCache
│   └── 存储预生成的音符几何体以避免重复计算
├── _batchManager: RenderBatchManager
│   └── 管理渲染批处理，优化GPU提交
├── _colorConfig: NoteColorConfiguration
│   └── 管理音符颜色方案
└── _stats: RenderStats
    └── 累计渲染统计信息
```

**关键方法流程**：

```
BeginFrame()
  → 创建新的RenderFrame
    
DrawNote(noteData, frame)
  → GetColorConfiguration(pitch, velocity)
  → GetOrCreateGeometry(position, size, radius)
  → AddToCurrentBatch(geometry, color)
  
SubmitFrame(frame, commandBuffer)
  → GetBatches()
  → BindPipeline()
  → SubmitBatch() × N
  → UpdateStats()
  
ClearFrame(frame)
  → frame.Clear()
```

### 2. NoteGeometryCache

**目的**：缓存音符几何体以提高性能

**缓存策略**：

```
Key: (Position, Width, Height, CornerRadius)
  ↓
Value: NoteGeometry
  ├── Vertices: List<Vector2>
  │   └── 音符顶点坐标列表
  ├── Indices: List<uint>
  │   └── 三角形索引
  ├── VertexBuffer: GPU资源
  └── IndexBuffer: GPU资源
```

**缓存命中场景**：
- 相同大小和位置的重复音符
- UI元素（网格线、键盘）
- 不同轨道的相似音符

### 3. RenderFrame

**目的**：收集单帧的所有渲染数据

**工作流程**：

```
RenderFrame
├── _batches: List<RenderBatch>
│   └── 当前帧的所有批处理
├── _currentBatch: RenderBatch?
│   └── 当前正在填充的批处理
│
└── 方法：
    ├── AddNoteGeometry(geometry, color)
    │   ├── 检查当前批次容量
    │   ├── 如果满则创建新批次
    │   └── 添加到当前批次
    │
    ├── GetBatches()
    │   └── 返回所有批次供提交
    │
    └── Clear()
        └── 重置为新帧
```

### 4. PianoRollUIRenderer

**目的**：渲染钢琴卷帘的所有UI组件

**子系统**：

```
PianoRollUIRenderer
├── _lineRenderer: LineRenderer
│   ├── RenderGrid() - 绘制网格
│   ├── RenderPlayhead() - 绘制播放头
│   └── DrawLine() - 基础直线
│
├── _rectRenderer: RectangleRenderer
│   ├── RenderKeyboard() - 绘制键盘
│   ├── RenderSelectionBox() - 绘制选区
│   └── DrawRectangle() - 基础矩形
│
└── _textRenderer: TextRenderer
    └── DrawText() - 文本（未来扩展）
```

### 5. RenderPerformanceMonitor

**目的**：实时性能数据采集与分析

**数据流**：

```
BeginFrame()
  ↓ (记录时间戳)
RecordStageTime(stageName, timeMs)
  ↓ (累计各阶段时间)
RecordNoteCount(count)
RecordBatchCount(count)
RecordGPUMemoryUsage(used, allocated)
RecordCPUMemoryUsage(used)
  ↓ (填充FrameMetrics)
EndFrame()
  ↓ (添加到历史)
_frameHistory (维持大小限制)
  ↓
GetReport()
  ├── 计算平均值: Average(x) = Sum(x) / Count
  ├── 计算百分位: Percentile(p) = Sort()[p*Count/100]
  ├── 计算FPS: FPS = 1000 / AverageFrameTime
  └── 返回PerformanceReport
```

### 6. RenderOptimizationAdvisor

**分析规则**：

```
GetOptimizationSuggestions()
  ├── 检查 AverageFrameTime > TargetFrameTime * 1.2
  │   └── 建议: 减少音符数、优化批处理
  │
  ├── 检查 (MaxTime - MinTime) > TargetFrameTime * 0.5
  │   └── 建议: 检查峰值负载、使用时间切片
  │
  ├── 检查 P99FrameTime > TargetFrameTime * 2
  │   └── 建议: 帧率上限、简化渲染
  │
  ├── 检查 AverageBatchCount > 1000
  │   └── 建议: 合并对象、提高效率
  │
  └── 检查 GPUMemory > 1000MB
      └── 建议: 压缩纹理、实现LOD
```

---

## 数据流示意

### 音符渲染流程

```
MIDI文件
  ↓ (导入)
NoteModel[]
  ↓ (转换)
NoteDrawData[]
  ↓ (视口裁剪)
VisibleNotes[]
  │
  ├─→ VulkanNoteRenderEngine.DrawNotes()
  │     ├─→ 获取颜色 (NoteColorConfiguration)
  │     ├─→ 获取/创建几何体 (NoteGeometryCache)
  │     └─→ 添加到批处理 (RenderBatch)
  │
  ├─→ RenderFrame (收集数据)
  │
  ├─→ SubmitFrame()
  │     ├─→ 绑定管线
  │     ├─→ 提交每个批处理
  │     └─→ 同步统计
  │
  └─→ GPU执行 (Vulkan驱动)
      └─→ 屏幕显示
```

### 性能监测流程

```
实时渲染循环
  ├─→ BeginFrame()
  │     └─→ 启动计时器
  │
  ├─→ 渲染操作
  │     ├─→ RecordStageTime("Building", 1.5ms)
  │     ├─→ RecordStageTime("Submission", 0.8ms)
  │     ├─→ RecordNoteCount(1000)
  │     └─→ RecordBatchCount(10)
  │
  └─→ EndFrame()
      ├─→ 停止计时器
      ├─→ 创建FrameMetrics
      ├─→ 添加到_frameHistory
      ├─→ 维持历史大小
      └─→ 返回给应用层
```

---

## 内存模型

### GPU内存布局

```
GPU内存
├── VBO (顶点缓冲)
│   ├── 网格顶点
│   ├── 键盘顶点
│   └── 音符顶点 × N
│
├── IBO (索引缓冲)
│   ├── 网格索引
│   ├── 键盘索引
│   └── 音符索引 × N
│
├── UBO (统一缓冲)
│   ├── 变换矩阵
│   ├── 投影矩阵
│   └── 视图矩阵
│
└── 纹理内存
    ├── UI纹理
    └── 字体纹理（未来）
```

### CPU内存组织

```
VulkanNoteRenderEngine
├── _frameHistory (300项)
│   └── 每项 ~500字节 = 150KB
│
├── _cache (动态)
│   └── 典型 1000-5000项 = 10-50MB
│
├── _tempBuffers
│   ├── 顶点缓冲 ~10MB
│   └── 索引缓冲 ~5MB
│
└── 其他开销
    └── ~50MB
```

---

## 性能优化点

### 1. 批处理优化

**前**：
```
DrawNote(note1) → 1条命令
DrawNote(note2) → 1条命令
DrawNote(note3) → 1条命令
... (1000个命令)
总计：1000次GPU提交
```

**后**：
```
Batch 1: [note1, note2, ... note100] → 1条命令
Batch 2: [note101, ... note200] → 1条命令
... (10个命令)
总计：10次GPU提交 (提升100x!)
```

### 2. 几何体缓存

**前**：
```
每帧计算：1000个音符 × 几何计算 = 10ms
```

**后**：
```
首次计算：音符模板几何 = 0.1ms
后续帧：从缓存取 = 0.01ms
节省：~90%时间
```

### 3. 视口裁剪

**前**：
```
渲染所有音符：10000个 = 30ms
```

**后**：
```
只渲染可见音符：1000个 = 3ms
节省：~90%工作
```

---

## 扩展点

### 1. 音符渲染方式

当前实现：简单矩形
可扩展为：
- 3D音符（立体显示）
- 自定义形状（五角星、圆形等）
- 动画过渡（淡入淡出）
- 粒子效果

### 2. UI组件

当前实现：网格、键盘、播放头、选区
可扩展为：
- 小节编号
- 速度曲线可视化
- 旋律线
- 手势标记

### 3. 渲染后处理

可添加：
- 模糊效果
- 景深
- 运动模糊
- 色彩分级

### 4. 音频可视化

可集成：
- 频谱分析
- 波形显示
- 能量条
- 节拍指示

---

## 集成检查清单

在将渲染引擎集成到Lumino时：

- [ ] Vulkan初始化成功
- [ ] 性能监测器运行正常
- [ ] 批处理优化生效
- [ ] 内存使用在可接受范围
- [ ] 帧率稳定在60fps以上
- [ ] 没有内存泄漏
- [ ] 支持热重载（场景切换）
- [ ] 错误处理完善
- [ ] 日志输出清晰
- [ ] 文档完整

---

## 故障排查

### 问题：FPS低于30

检查列表：
1. 运行 `advisor.GenerateOptimizationReport()`
2. 检查GPU内存是否不足
3. 检查批处理大小
4. 检查视口裁剪是否有效
5. 使用 `monitor.LogDetailedAnalysis()`

### 问题：内存持续增长

原因排查：
1. 几何体缓存是否清理：`engine.ClearCache()`
2. 命令缓冲是否正确释放
3. CPU对象是否重用

### 问题：帧时间波动

可能原因：
1. 垃圾回收周期
2. 某些帧的额外工作
3. 系统中断

解决方案：
1. 使用对象池
2. 异步处理
3. 预分配资源

---

**架构版本**: 1.0  
**最后更新**: 2025年11月12日
