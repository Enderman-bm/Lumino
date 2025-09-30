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
- 直接输入0-127范围内的数字（不需要上下箭头）
- 只允许输入数字，按Enter键或失去焦点时自动验证并修正范围
- 不同的CC号会使用不同的颜色进行区分

### 3. 铅笔工具曲线绘制
- 选择铅笔工具后，可以在事件视图区域绘制曲线
- 按住鼠标拖动即可绘制连续的曲线
- 松开鼠标完成绘制
- 曲线会根据当前选择的事件类型自动调整数值范围

### 4. 曲线渲染
- **力度**：使用绿色直线连接，显示关键点
- **弯音**：使用蓝色平滑曲线，不显示关键点
- **CC控制器**：根据CC号生成不同颜色的平滑曲线，显示关键点

## 技术实现

### 核心组件

#### 1. 事件类型枚举
```csharp
public enum EventType
{
    Velocity,        // 力度
    PitchBend,       // 弯音
    ControlChange    // 控制器变化
}
```

#### 2. 事件曲线计算服务
- `IEventCurveCalculationService`: 负责不同事件类型的数值计算
- `EventCurveCalculationService`: 实现坐标转换、数值范围计算等功能

#### 3. 曲线绘制模块
- `EventCurveDrawingModule`: 处理铅笔工具的曲线绘制逻辑
- `CurvePoint`: 曲线点数据结构，包含时间、数值和屏幕坐标

#### 4. PianoRollViewModel增强
新增属性：
- `CurrentEventType`: 当前选择的事件类型
- `CurrentCCNumber`: 当前选择的CC控制器号（0-127）
- `IsEventTypeSelectorOpen`: 是否显示事件类型选择器
- `CurrentEventTypeText`: 当前事件类型的显示名称
- `CurrentEventValueRange`: 当前事件类型的数值范围
- `IsDrawingEventCurve`: 是否正在绘制事件曲线

新增命令：
- `ToggleEventTypeSelectorCommand`: 切换事件类型选择器
- `SelectEventTypeCommand`: 选择事件类型
- `ValidateAndSetCCNumberCommand`: 验证并设置CC号

### 5. 增强的ControllerCurveRenderer
- `DrawEventCurve`: 根据事件类型绘制相应的曲线
- `DrawControlChangeCurve`: 绘制CC控制器曲线
- 支持不同事件类型的专用样式和颜色
- 基于HSV颜色空间为不同CC号生成唯一颜色

## 用户界面改进

### CC号输入改进
- 移除了NumericUpDown的上下箭头
- 改用TextBox直接输入数字
- 自动验证输入范围（0-127）
- 只允许数字键盘输入
- 支持Enter键确认和失去焦点自动验证

### 事件绘制体验
- 铅笔工具支持连续曲线绘制
- 自动插入中间点保证曲线连续性
- 智能优化曲线点，移除冗余数据
- 实时显示当前数值范围

## 扩展性设计

该架构具有良好的扩展性，可以轻松添加新的MIDI事件类型：

1. 在`EventType`枚举中添加新的事件类型
2. 在`EventCurveCalculationService`中添加对应的数值范围计算
3. 在`ControllerCurveRenderer`中添加新事件类型的样式
4. 在UI中添加相应的选项

## 性能优化

- 曲线点优化算法减少冗余数据
- 延迟渲染机制避免频繁UI更新
- 智能中间点插入算法保证绘制流畅性
- 基于事件的架构减少不必要的计算

## 注意事项

- 力度值使用直线连接，适合表示离散的音符力度
- 弯音使用平滑曲线，适合表示连续的音高变化
- CC控制器根据不同的CC号使用不同颜色，便于区分多个控制器
- 所有的曲线渲染都基于优化的`MouseCurveRenderer`，确保性能和视觉效果
- CC号输入框会自动限制在有效范围内，无效输入会被自动修正