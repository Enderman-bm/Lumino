# 事件绘制调试指南

## 概述
本文档描述了如何调试事件曲线绘制功能。所有调试输出都会通过 `System.Diagnostics.Debug.WriteLine` 写入到 Visual Studio 的调试输出窗口。

## 调试日志位置
1. 打开 Visual Studio
2. 在菜单中选择：**Debug** → **Windows** → **Output**（或按 `Ctrl+Alt+O`）
3. 在输出窗口的下拉菜单中选择：**Debug**

## 事件流和日志点

### 1. 鼠标按下 (OnPointerPressed)
**日志输出示例：**
```
[VelocityViewCanvas] OnPointerPressed: Position=(256.5, 128.3), LeftButtonPressed=True
[VelocityViewCanvas] CurrentTool=Pencil, EventCurveDrawingModule=True
[VelocityViewCanvas] Starting curve drawing at (512.5, 128.3), CanvasHeight=300
[VelocityViewCanvas] IsDrawing=True
```

**预期行为：**
- 应该显示点击位置的屏幕坐标
- 应该显示当前工具为 "Pencil"
- 应该显示 IsDrawing 变为 True

**如果没有这些日志：**
- ❌ 检查鼠标事件是否到达
- ❌ 检查 ViewModel 是否为 null
- ❌ 检查工具选择是否正确设置

---

### 2. 鼠标移动 (OnPointerMoved)
**日志输出示例：**
```
[VelocityViewCanvas] UpdateDrawingEventCurve: (514.2, 130.5), PointCount=2
[VelocityViewCanvas] UpdateDrawingEventCurve: (516.8, 132.1), PointCount=3
[VelocityViewCanvas] UpdateDrawingEventCurve: (519.5, 135.4), PointCount=4
```

**预期行为：**
- 多行日志，每次鼠标移动时输出一行
- PointCount 应该逐渐增加

**如果没有这些日志：**
- ❌ 鼠标移动事件未被触发
- ❌ EventCurveDrawingModule.IsDrawing 不为 True

**如果有日志但 PointCount 不增加：**
- ❌ UpdateDrawing 方法未正确添加点

---

### 3. 曲线更新事件 (OnCurveUpdated)
**日志输出示例：**
```
[VelocityViewCanvas] OnCurveUpdated triggered, calling SyncRefresh
[VelocityViewCanvas] RefreshRender called, calling InvalidateVisual
```

**预期行为：**
- OnCurveUpdated 应该在每次鼠标移动时触发
- 随后应该立即看到 RefreshRender 调用

**如果没有 OnCurveUpdated 日志：**
- ❌ EventCurveDrawingModule 事件未正确订阅
- ❌ 检查 SubscribeToViewModel 方法

**如果有 OnCurveUpdated 但没有 RefreshRender：**
- ❌ RenderSyncService.SyncRefresh() 未工作
- ❌ 检查 RenderSyncService 实现

---

### 4. 鼠标释放 (OnPointerReleased)
**日志输出示例：**
```
[VelocityViewCanvas] FinishDrawingEventCurve: PointCount=25
[VelocityViewCanvas] OnCurveCompleted: 25 points, calling SyncRefresh
[VelocityViewCanvas] RefreshRender called, calling InvalidateVisual
```

**预期行为：**
- 应该显示最终的 PointCount
- OnCurveCompleted 应该被调用
- 应该再次触发 RefreshRender

**如果没有这些日志：**
- ❌ OnPointerReleased 未被调用
- ❌ FinishDrawingEventCurve 未工作

---

## 完整的正常流程示例

以下是正常绘制操作的完整日志序列：

```
[VelocityViewCanvas] OnPointerPressed: Position=(100, 150), LeftButtonPressed=True
[VelocityViewCanvas] CurrentTool=Pencil, EventCurveDrawingModule=True
[VelocityViewCanvas] Starting curve drawing at (350, 150), CanvasHeight=300
[VelocityViewCanvas] IsDrawing=True
[VelocityViewCanvas] UpdateDrawingEventCurve: (351, 151), PointCount=1
[VelocityViewCanvas] OnCurveUpdated triggered, calling SyncRefresh
[VelocityViewCanvas] RefreshRender called, calling InvalidateVisual
[VelocityViewCanvas] UpdateDrawingEventCurve: (352, 152), PointCount=2
[VelocityViewCanvas] OnCurveUpdated triggered, calling SyncRefresh
[VelocityViewCanvas] RefreshRender called, calling InvalidateVisual
[VelocityViewCanvas] UpdateDrawingEventCurve: (355, 155), PointCount=3
[VelocityViewCanvas] OnCurveUpdated triggered, calling SyncRefresh
[VelocityViewCanvas] RefreshRender called, calling InvalidateVisual
[VelocityViewCanvas] FinishDrawingEventCurve: PointCount=3
[VelocityViewCanvas] OnCurveCompleted: 3 points, calling SyncRefresh
[VelocityViewCanvas] RefreshRender called, calling InvalidateVisual
```

## 故障排查矩阵

| 症状 | 可能原因 | 检查项 |
|------|--------|-------|
| 没有任何日志 | 鼠标事件未到达 | 检查 VelocityViewCanvas 是否获得焦点 |
| 只有 OnPointerPressed，没有 OnPointerMoved | 鼠标移动事件不工作 | 检查 OnPointerMoved 是否被覆盖 |
| 有 UpdateDrawingEventCurve 但没有 OnCurveUpdated | 事件订阅问题 | 在 SubscribeToViewModel 中添加断点 |
| 有 OnCurveUpdated 但没有 RefreshRender | RenderSyncService 问题 | 检查 SyncRefresh 实现 |
| 有 RefreshRender 但没有可视反馈 | 渲染逻辑问题 | 检查 Render() 方法中的 DrawVelocityBars 逻辑 |

## 如何阅读坐标

- **屏幕坐标**：相对于 Canvas 顶部的相对位置（0~Width, 0~Height）
- **世界坐标**：考虑了滚动偏移，用于实际数据存储

示例：
```
OnPointerPressed: Position=(256.5, 128.3)    // 屏幕坐标
Starting curve drawing at (512.5, 128.3)     // 世界坐标（已加上滚动偏移）
```

## 性能检查

如果绘制感觉迟钝或卡顿：

1. **检查日志频率**
   - OnCurveUpdated 应该频繁出现（每个鼠标事件一次）
   - 如果频率太低，可能是鼠标事件队列堆积

2. **检查渲染时间**
   - RefreshRender 应该立即跟随 OnCurveUpdated
   - 如果延迟太长，说明 InvalidateVisual 处理速度慢

3. **检查点数增长**
   - PointCount 应该平稳增长
   - 如果跳跃太大，说明某些鼠标事件被丢弃

## 禁用调试日志

完成调试后，删除所有 `System.Diagnostics.Debug.WriteLine` 语句或将其注释出来。

在 Release 构建中，Debug.WriteLine 会被自动编译器优化删除，不会影响性能。

---

**最后更新**：2024年10月
**相关文件**：
- `VelocityViewCanvas.cs` - 主绘制画布
- `EventCurveDrawingModule.cs` - 绘制逻辑
- `PianoRollViewModel.cs` - 视图模型协调
