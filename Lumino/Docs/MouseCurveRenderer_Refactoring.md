# 鼠标曲线渲染器重构说明

## 重构概述

这次重构将力度条渲染器 (`VelocityBarRenderer`) 中的鼠标轨迹绘制逻辑提取到了一个独立的通用组件 `MouseCurveRenderer` 中。

## 重构的好处

### 1. 职责分离
- **VelocityBarRenderer**: 专注于力度条的绘制逻辑
- **MouseCurveRenderer**: 专注于鼠标轨迹曲线的绘制

### 2. 代码复用
- 其他功能（如音高编辑、控制器编辑等）可以直接使用 `MouseCurveRenderer`
- 避免重复实现相似的曲线绘制逻辑

### 3. 易于维护
- 曲线绘制相关的bug修复和功能改进只需要在一个地方进行
- 样式和行为的调整更加集中和一致

### 4. 扩展性强
- 通过 `CurveStyle` 类可以轻松配置不同的视觉效果
- 支持平滑曲线、虚线、关键点显示等多种选项

## 新的组件结构

### MouseCurveRenderer
位置: `Lumino/Views/Rendering/Common/MouseCurveRenderer.cs`

主要功能:
- `DrawCurve()`: 绘制鼠标轨迹曲线
- `DrawDots()`: 绘制轨迹上的关键点
- `DrawMouseTrail()`: 绘制完整的鼠标轨迹（曲线+点）
- `CurveStyle`: 样式配置类

### 重构后的 VelocityBarRenderer
保留功能:
- 力度条的绘制 (`DrawVelocityBar`)
- 力度相关的样式计算
- 力度值文本显示
- 当前编辑位置的力度条预览

移除功能:
- 鼠标轨迹曲线的具体绘制实现
- 贝塞尔曲线计算
- 关键点绘制逻辑

## 使用示例

### 1. 力度编辑 (已集成)
```csharp
// 在 VelocityBarRenderer.DrawEditingPreview 中
var curveStyle = _curveRenderer.CreateEditingPreviewStyle();
_curveRenderer.DrawMouseTrail(context, editingModule.EditingPath, canvasBounds, scrollOffset, curveStyle);
```

### 2. 音高编辑 (示例实现)
```csharp
// 在 PitchCurveRenderer 中
public void DrawPitchEditingPreview(DrawingContext context, IEnumerable<Point> pitchEditingPath, 
    Rect canvasBounds, double scrollOffset = 0)
{
    var pitchStyle = CreatePitchEditingStyle();
    _curveRenderer.DrawMouseTrail(context, pitchEditingPath, canvasBounds, scrollOffset, pitchStyle);
}
```

### 3. 自定义样式
```csharp
var customStyle = new MouseCurveRenderer.CurveStyle
{
    Brush = Brushes.Red,
    Pen = new Pen(Brushes.DarkRed, 3),
    ShowDots = true,
    DotSize = 5.0,
    UseSmoothCurve = true,
    DashPattern = new double[] { 10, 5 }
};

_curveRenderer.DrawMouseTrail(context, points, bounds, offset, customStyle);
```

## 文件清单

### 新增文件
- `Lumino/Views/Rendering/Common/MouseCurveRenderer.cs` - 通用鼠标曲线渲染器
- `Lumino/Views/Rendering/Events/PitchCurveRenderer.cs` - 音高编辑示例 (演示用途)

### 修改文件
- `Lumino/Views/Rendering/Events/VelocityBarRenderer.cs` - 移除曲线绘制逻辑，使用新的渲染器

## 向后兼容性

所有现有的公共接口保持不变，重构是内部实现的优化，不会影响外部调用者。

## 未来扩展方向

1. **控制器编辑**: 可以使用相同的曲线渲染器处理控制器值的编辑
2. **自动化编辑**: 支持各种MIDI自动化数据的可视化编辑
3. **效果参数编辑**: 音频效果参数的实时编辑界面
4. **表情编辑**: 音乐表情的图形化编辑工具