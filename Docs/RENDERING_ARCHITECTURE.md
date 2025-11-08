# Lumino 渲染架构与性能优化指南

## 概述
本文档描述 Lumino 的渲染系统架构，包括 Avalonia UI 集成、Vulkan 自定义渲染路径、并行化策略及性能优化措施。

## 架构层次

### 1. UI 框架层（Avalonia）
- **职责**：窗口管理、布局、控件树、用户交互
- **渲染入口**：`Control.Render(DrawingContext)` 回调
- **约束**：所有 Avalonia 对象（IBrush、IPen、StyledProperty）必须在 UI 线程创建和访问

### 2. 自定义渲染层
#### 2.1 渲染器架构（Compute/Draw 两阶段）
- **Compute 阶段**（可并行，后台线程安全）：
  - 输入：视图状态（缩放、滚动、可见范围）、纯数据（ARGB bytes、坐标）
  - 输出：`PreparedRoundedRectBatch`、`PreparedLineBatch` 等纯数据结构
  - 示例：`VerticalGridRenderer.ComputeGridBatches()`
  
- **Draw 阶段**（UI 线程，串行）：
  - 消费 Compute 阶段生成的批次
  - 调用 `DrawingContext.Draw*()` 或 Vulkan 适配器接口
  - 提交批次到 `VulkanRenderService.EnqueuePreparedRoundedRectBatch()`

#### 2.2 主要渲染器
| 渲染器 | 职责 | 并行化状态 |
|--------|------|-----------|
| `VerticalGridRenderer` | 垂直网格线（小节、拍、细分） | ✅ 已实现 Compute/Draw |
| `HorizontalGridRenderer` | 水平网格线（琴键） | ⏸️ 待迁移 |
| `PlayheadRenderer` | 播放头指示器 | ✅ UI 线程初始化 |
| `OptimizedNoteRenderer` | 音符渲染（Vulkan 批处理） | ⚠️ 部分优化 |
| `DragPreviewRenderer` | 拖拽预览 | ✅ UI 线程初始化 |

### 3. Vulkan 渲染服务
- **`VulkanRenderService`**：全局单例，管理 Vulkan 设备、交换链、命令队列
- **批次队列**：
  - `PreparedRoundedRectBatch`：矩形批次（带圆角半径）
  - `PreparedLineBatch`（计划中）：线条批次
- **渲染线程**：独立线程处理批次提交并与 Avalonia 表面同步

## 性能优化策略

### 已实施
1. **UI 线程资源预缓存**
   - `EnsurePensInitialized()` 在 UI 线程创建并缓存 Avalonia 对象
   - 缓存 ARGB bytes 供后台线程使用
   
2. **后台线程纯数据计算**
   - `ComputeGridBatches()` 生成批次，不触及 Avalonia API
   
3. **性能监控**
   - `PerformanceMonitor` 测量关键路径耗时
   - 启用方式：`PianoRollCanvas.EnablePerformanceMonitoring = true`
   
4. **Vulkan 批处理**
   - 合并多个绘制调用为单个批次提交
   - 减少 draw call 开销

### 计划中
1. **对象池**
   - `PreparedRoundedRectBatch` 复用
   - ArrayPool 用于临时数组分配
   
2. **结构体优化**
   - 将短生命周期 class 改为 struct（减少 GC 压力）
   
3. **批次合并**
   - 相邻网格线合并为单个几何体
   - 减少 GPU 提交开销
   
4. **全局 Vulkan 渲染**
   - 独立渲染线程与交换链
   - 完全绕过 Avalonia 的 Skia 后端（仅用于 UI 控件）

## 配置选项

### 运行时开关
```csharp
// 在 App.axaml.cs 或启动代码中设置

// 并行渲染（实验性，默认禁用）
PianoRollCanvas.EnableParallelPianoRendering = false;
PianoRollCanvas.ParallelWorkerCount = 8;

// 性能监控（开发/分析时启用）
PianoRollCanvas.EnablePerformanceMonitoring = true;

// Vulkan 渲染（自动检测，失败则回退 Skia）
// 由 VulkanRenderService.Instance.IsSupported 控制
```

### 性能分析
```csharp
// 运行一段时间后输出统计
PerformanceMonitor.LogSummary();

// 重置统计并强制 GC（用于基准测试）
PerformanceMonitor.Reset();
```

## 线程安全原则

### ✅ 安全操作（后台线程）
- 读取纯数据属性（`viewModel.Zoom`、`viewModel.BaseQuarterNoteWidth`）
- 使用已缓存的 ARGB bytes
- 创建 `PreparedRoundedRectBatch` 并填充数据
- 数学计算、数组操作

### ❌ 禁止操作（后台线程）
- 调用 `DrawingContext.Draw*()`
- 创建 `IBrush`、`IPen`、`SolidColorBrush`
- 访问 `StyledProperty`（使用 `GetValue()`）
- 读取 `Avalonia.Media.Colors.*` 或 `Brush.Color`

## 故障排除

### 问题：小节线不显示
**原因**：
1. `EnsurePensInitialized()` 未在 UI 线程调用
2. Measure color ARGB 为 (0,0,0,0)（透明）
3. 后台缓存计算的 scrollOffset 捕获错误

**解决方案**：
- 确保在 `Render()` 头部调用 `_verticalGridRenderer.EnsurePensInitialized()`
- 检查 fallback 颜色是否设为不透明
- 使用局部变量捕获 scrollOffset 而非 closure 引用外部变量

### 问题："Call from invalid thread" 异常
**原因**：后台线程直接调用 Avalonia API

**解决方案**：
- 使用 Compute/Draw 两阶段模式
- 后台线程仅生成纯数据批次
- UI 线程消费批次并绘制

### 问题：性能回归
**诊断步骤**：
1. 启用 `PerformanceMonitor`
2. 运行并调用 `PerformanceMonitor.LogSummary()`
3. 检查 GC 统计（Gen0/Gen1/Gen2 计数）
4. 使用 Visual Studio Profiler 或 dotTrace

## 未来路线图

### 短期（1-2 周）
- [ ] 将 `HorizontalGridRenderer` 迁移为 Compute/Draw
- [ ] 实现 `PreparedRoundedRectBatch` 对象池
- [ ] 添加批次合并逻辑

### 中期（1-2 月）
- [ ] 全局 Vulkan 渲染线程架构
- [ ] 完整的 Avalonia interop（表面共享）
- [ ] 音符渲染全面迁移到 Vulkan 批处理

### 长期（3+ 月）
- [ ] GPU 加速频谱图渲染
- [ ] 跨平台 Vulkan 兼容性测试（Linux/macOS）
- [ ] 完整的性能回归测试套件

## 参考资料
- [Avalonia Rendering](https://docs.avaloniaui.net/docs/concepts/control-trees)
- [Vulkan Best Practices](https://www.khronos.org/assets/uploads/developers/library/2016-vulkan-devday-uk/9-Vulkan-Best-Practices.pdf)
- [.NET Memory Performance](https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/performance)

---
**最后更新**：2025年11月8日  
**维护者**：Lumino 开发团队
