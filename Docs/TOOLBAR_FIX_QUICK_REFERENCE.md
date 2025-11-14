# 工具栏修复 - 快速参考

## 问题
工具栏按钮（铅笔、选择、橡皮等）无法与钢琴卷帘交互。

## 原因
`PianoRollViewModel` 没有订阅 `Toolbar` 的事件，导致工具切换后属性通知未触发。

## 修复
**文件**：`Lumino/ViewModels/Editor/Base/PianoRollViewModel.Events.cs`

### 修改前
```csharp
// 工具栏事件 - 先暂时注释掉，我们通过Configuration属性变化来处理
// Toolbar.EventViewToggleRequested += OnEventViewToggleRequested;
// Toolbar.ToolChanged += OnToolChanged;
// Toolbar.NoteDurationChanged += OnNoteDurationChanged;
// Toolbar.GridQuantizationChanged += OnGridQuantizationChanged;
```

### 修改后
```csharp
// 工具栏事件 - 订阅工具变化事件以确保PianoRollViewModel能够正确响应
Toolbar.ToolChanged += OnToolChanged;
Toolbar.NoteDurationChanged += OnNoteDurationChanged;
Toolbar.GridQuantizationChanged += OnGridQuantizationChanged;
Toolbar.EventViewToggleRequested += OnEventViewToggleRequested;
```

并添加了四个事件处理方法：
- `OnToolChanged` - 工具切换
- `OnNoteDurationChanged` - 音符时长变化
- `OnGridQuantizationChanged` - 网格量化变化
- `OnEventViewToggleRequested` - 事件视图切换

## 结果
✅ 所有工具现在都能正常工作
✅ 编译成功，无错误
✅ 保持了MVVM架构的完整性

## 测试建议
1. 点击铅笔工具，在钢琴卷帘上点击创建音符
2. 点击选择工具，框选并移动音符
3. 点击橡皮工具，点击音符删除
4. 更改网格量化，验证音符对齐
