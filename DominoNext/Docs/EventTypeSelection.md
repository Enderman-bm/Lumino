# 事件类型选择功能使用说明

## 功能概述

在EventViewPanel的左侧固定区域新增了事件类型选择功能，用户可以选择不同的MIDI事件类型进行编辑：

1. **力度 (Velocity)** - 范围：1-127
2. **弯音 (Pitch Bend)** - 范围：-8192~8191
3. **CC控制器 (Control Change)** - 范围：0-127，可选择0-127的CC号

## 使用方法

### 1. 选择事件类型
- 点击左侧区域的事件类型按钮
- 从弹出菜单中选择所需的事件类型：
  - 力度 (1-127)
  - 弯音 (-8192~8191)
  - CC控制器 (0-127)

### 2. 设置CC号（仅限CC类型）
- 当选择"CC控制器"类型时，会在下方显示CC号输入框
- 使用数字调节器设置0-127范围内的CC控制器号
- 不同的CC号会使用不同的颜色进行区分

### 3. 曲线渲染
- **力度**：使用绿色直线连接，显示关键点
- **弯音**：使用蓝色平滑曲线，不显示关键点
- **CC控制器**：根据CC号生成不同颜色的平滑曲线，显示关键点

## 技术实现

### 新增的枚举类型
```csharp
public enum EventType
{
    Velocity,        // 力度
    PitchBend,       // 弯音
    ControlChange    // 控制器变化
}
```

### 新增的ViewModel属性
- `CurrentEventType`: 当前选择的事件类型
- `CurrentCCNumber`: 当前选择的CC控制器号（0-127）
- `IsEventTypeSelectorOpen`: 是否显示事件类型选择器
- `CurrentEventTypeText`: 当前事件类型的显示名称
- `CurrentEventValueRange`: 当前事件类型的数值范围
- `CurrentEventDescription`: 当前事件类型的完整描述

### 新增的命令
- `ToggleEventTypeSelectorCommand`: 切换事件类型选择器的显示/隐藏
- `SelectEventTypeCommand`: 选择事件类型
- `SetCCNumberCommand`: 设置CC控制器号

### 增强的ControllerCurveRenderer
- `DrawEventCurve`: 根据事件类型绘制相应的曲线
- `DrawControlChangeCurve`: 绘制CC控制器曲线
- 支持不同事件类型的专用样式和颜色

## 扩展性

该设计具有良好的扩展性，可以轻松添加新的MIDI事件类型：

1. 在`EventType`枚举中添加新的事件类型
2. 在`CurrentEventTypeText`和`CurrentEventValueRange`属性中添加对应的显示文本
3. 在`ControllerCurveRenderer`中添加新事件类型的样式
4. 在XAML中添加相应的UI选项

## 注意事项

- 力度值使用直线连接，适合表示离散的音符力度
- 弯音使用平滑曲线，适合表示连续的音高变化
- CC控制器根据不同的CC号使用不同颜色，便于区分多个控制器
- 所有的曲线渲染都基于优化的`MouseCurveRenderer`，确保性能和视觉效果