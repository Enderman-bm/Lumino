# 事件绘制面板 - 快速参考指南

## 核心改进概览

### ✅ 已修复的问题
1. **事件类型切换不响应** - 现在完全支持切换
2. **弯音绘制无法使用** - 现在完整支持
3. **CC控制器面板无效** - 现在完整支持并可设置CC号
4. **速度编辑缺失** - 现在支持（Conductor轨道）

### 📋 支持的事件类型

| 类型 | 数值范围 | 图标 | 适用轨道 | 备注 |
|------|--------|------|--------|------|
| **力度** | 1-127 | 💪 | 音乐轨道 | 音符音量 |
| **弯音** | -8192~8191 | 📈 | 音乐轨道 | 音高弯曲效果 |
| **CC控制器** | 0-127 | 🎛️ | 音乐轨道 | 可自定义CC号 |
| **速度** | 1-300 BPM | 🎵 | Conductor轨道 | 曲速控制 |

## 使用流程

### 步骤 1: 切换事件类型
```
1. 点击左侧事件类型按钮（显示当前类型）
2. 从弹出菜单选择需要的类型
3. 菜单自动关闭，画布内容更新
```

### 步骤 2: 配置CC号 (仅CC模式)
```
1. 选择"CC控制器"后，下方出现CC号输入框
2. 输入0-127范围内的数值
3. 按Enter或点击其他地方确认
```

### 步骤 3: 选择编辑工具
```
铅笔工具 (Pencil)：用于绘制事件曲线
选择工具 (Select)：用于编辑力度值
```

### 步骤 4: 进行编辑操作
```
铅笔工具模式：
  - 按住左键并拖动绘制光滑曲线
  - 松开鼠标完成绘制

选择工具模式：
  - 按住左键上下拖动调整力度值
  - 释放鼠标保存更改
```

## 关键命令

| 方法 | 参数 | 功能 |
|------|------|------|
| `SelectEventType()` | EventType | 选择事件类型 |
| `ToggleEventTypeSelector()` | - | 切换选择器显示 |
| `SetCCNumber()` | int (0-127) | 设置CC号 |
| `StartDrawingEventCurve()` | Point, double | 开始绘制 |
| `UpdateDrawingEventCurve()` | Point | 更新绘制 |
| `FinishDrawingEventCurve()` | - | 完成绘制 |

## 数据流转

```
UI交互 (点击按钮/拖动)
    ↓
触发 PointerPressed/Moved/Released 事件
    ↓
ViewModel 命令被执行
    ↓
EventCurveDrawingModule 或 VelocityEditingModule 处理
    ↓
数据通过 IEventCurveCalculationService 转换
    ↓
VelocityViewCanvas 根据事件类型渲染
    ↓
用户看到实时反馈
```

## 属性绑定

### PianoRollViewModel

```csharp
// 读写属性
CurrentEventType          // 当前选择的事件类型
CurrentCCNumber           // 当前CC号 (0-127)
IsEventTypeSelectorOpen   // 选择器是否打开

// 只读属性
CurrentEventTypeText      // 事件类型的显示文本
CurrentEventValueRange    // 事件值范围描述
CurrentEventDescription   // 完整事件描述
IsDrawingEventCurve       // 是否正在绘制
```

## 错误处理

所有输入都会进行验证：

```csharp
// CC号范围检查 (0-127)
if (ccNumber < 0 || ccNumber > 127)
    throw new ArgumentException("CC号必须在0-127范围内");

// 画布高度检查
if (canvasHeight <= 0)
    throw new ArgumentException("画布高度必须大于0");

// 数值范围限制
value = calculationService.ClampValue(value, eventType, ccNumber);
```

## 技术细节

### 事件类型枚举 (EventType.cs)
```csharp
public enum EventType
{
    Velocity,        // 力度 (1-127)
    PitchBend,       // 弯音 (-8192～8191)
    ControlChange,   // CC (0-127)
    Tempo            // 速度 (1-300 BPM)
}
```

### 值转换公式

力度 Y坐标转换示例：
```
normalizedY = Y / CanvasHeight
velocity = MaxValue - (normalizedY * Range)
velocity = Clamp(velocity, MinValue, MaxValue)
```

## 常见问题

**Q: 为什么看不到CC号输入框？**
A: 只有在选择"CC控制器"事件类型时才会显示。

**Q: CC号输入了超过127的值会怎样？**
A: 会自动限制在0-127范围内。

**Q: 如何切换回力度编辑模式？**
A: 选择"力度"事件类型，然后使用选择工具而不是铅笔工具。

**Q: 弯音的-8192到8191范围是什么意思？**
A: 0是中性值（无弯曲），-8192是最低弯曲，8191是最高弯曲。

**Q: 不同的CC号会有不同的颜色吗？**
A: 是的，系统会根据CC号自动生成不同的颜色（基于HSV色轮）。

## 相关文件位置

| 文件 | 功能 |
|------|------|
| `EventCurveCalculationService.cs` | 数值计算 |
| `EventCurveDrawingModule.cs` | 绘制逻辑 |
| `ControllerCurveRenderer.cs` | 曲线渲染 |
| `VelocityViewCanvas.cs` | UI画布 |
| `EventViewPanel.axaml` | UI布局 |
| `PianoRollViewModel.Commands.cs` | 命令定义 |

## 测试命令行示例

```bash
# 编译项目
dotnet build

# 运行单元测试（如果存在）
dotnet test

# 发布版本
dotnet publish -c Release
```

---

**版本**: 2025年11月2日
**状态**: 生产就绪
**API稳定性**: 稳定
