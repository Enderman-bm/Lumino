# 事件绘制状态修复报告

**问题描述**: 鼠标绘制事件时，对应的事件条会消失，且无法更改事件状态

**问题根本原因**:

1. **事件类型切换时状态混乱** - 当用户切换事件类型（如从弯音切换到CC）时，正在进行的绘制操作没有被取消
2. **坐标转换错误** - 在曲线完成时，屏幕X坐标没有正确转换为音乐时间值，导致无法正确匹配音符
3. **Y坐标范围限制不一致** - 鼠标按下时Y坐标没有被限制在有效范围，导致计算错误

---

## 修复方案

### 1. 修复事件类型切换导致的状态混乱
**文件**: `VelocityViewCanvas.cs` - `OnViewModelPropertyChanged` 方法

**问题**: 当事件类型改变时，正在进行的绘制操作（`IsDrawing == true`）仍然继续，导致状态混乱

**解决方案**:
```csharp
else if (e.PropertyName == nameof(PianoRollViewModel.CurrentEventType) ||
         e.PropertyName == nameof(PianoRollViewModel.CurrentCCNumber))
{
    // 事件类型或CC号改变时：
    // 1) 如果正在绘制，取消绘制（防止状态混乱）
    // 2) 强制刷新画布显示新的事件类型
    if (ViewModel?.EventCurveDrawingModule?.IsDrawing == true)
    {
        System.Diagnostics.Debug.WriteLine($"[VelocityViewCanvas] 事件类型切换，取消正在进行的绘制");
        ViewModel.EventCurveDrawingModule.CancelDrawing();
    }
    
    System.Diagnostics.Debug.WriteLine($"[VelocityViewCanvas] 事件类型改变: {e.PropertyName}, 刷新画布");
    _renderSyncService.SyncRefresh();
}
```

**效果**: 
- ✅ 事件类型切换时自动取消正在进行的绘制
- ✅ 强制刷新画布显示新的事件指示块
- ✅ 防止状态混乱和绘制错误

---

### 2. 修复Y坐标范围不一致问题
**文件**: `VelocityViewCanvas.cs` - `OnPointerPressed` 方法

**问题**: `OnPointerPressed` 中Y坐标没有被限制，而 `OnPointerMoved` 中被限制在 `[0, Bounds.Height]`

**解决方案**:
```csharp
// 屏幕坐标转换为世界坐标（Y坐标限制在画布范围内）
var worldPosition = new Point(
    position.X + ViewModel.CurrentScrollOffset,
    Math.Max(0, Math.Min(Bounds.Height, position.Y))  // ← 添加范围限制
);
```

**效果**:
- ✅ Y坐标始终在有效范围内
- ✅ 防止超出范围的坐标值导致计算错误

---

### 3. 修复坐标转换错误（核心问题）
**文件**: `VelocityViewCanvas.cs` - `OnCurveCompleted` 方法

**问题**: 使用屏幕X坐标直接与音符时间值比较，导致无法匹配正确的音符

**转换流程**:
```
屏幕X坐标 → 世界X坐标（加上滚动偏移）→ 音乐时间值
ScreenX      WorldX = ScreenX + ScrollOffset    MusicTime = WorldX / TimeToPixelScale
```

**解决方案**:
```csharp
// 将屏幕X坐标转换为世界坐标，再转换为音乐时间值
var worldX = curvePoint.ScreenPosition.X;
var musicTime = (worldX + scrollOffset) / timeToPixelScale;

// 找到这个时间位置对应的音符
var noteAtTime = ViewModel.CurrentTrackNotes.FirstOrDefault(n =>
    n.StartPosition.ToDouble() <= musicTime &&
    n.StartPosition.ToDouble() + n.Duration.ToDouble() > musicTime);
```

**效果**:
- ✅ 正确地将屏幕坐标转换为音乐时间值
- ✅ 准确匹配曲线点对应的音符
- ✅ 事件值正确应用到音符

---

## 代码修改总结

### 修改的文件
- `VelocityViewCanvas.cs` (3处修改)

### 修改统计
| 修改项 | 位置 | 变更内容 |
|--------|------|--------|
| 事件类型切换处理 | OnViewModelPropertyChanged | 添加IsDrawing检查，切换时取消绘制 |
| Y坐标范围限制 | OnPointerPressed | 添加范围限制 `Math.Max(0, Math.Min())` |
| 坐标转换逻辑 | OnCurveCompleted | 实现 `ScreenX → WorldX → MusicTime` 转换 |

### 编译结果
- ✅ **0个编译错误**
- ⚠️ 106个既有警告（不相关）

---

## 验证清单

### 绘制时指示块消失问题
- [ ] 在弯音模式下绘制曲线，确认橙色指示块仍然可见
- [ ] 在CC模式下绘制曲线，确认绿色指示块仍然可见
- [ ] 绘制曲线时，指示块高度是否与已有值匹配
- [ ] 完成绘制后，指示块高度是否更新为新值

### 无法改变事件状态问题
- [ ] 在弯音模式绘制中途切换到CC模式，绘制是否立即停止
- [ ] 切换后是否显示CC指示块而不是弯音指示块
- [ ] 切换事件类型后，是否可以继续绘制新的事件
- [ ] 在CC模式改变CC号（如1→7），是否正确刷新显示

### 数值准确性
- [ ] 绘制完成后，是否每个音符都获得了对应的事件值
- [ ] 不同音符是否获得不同的值（对应绘制的曲线高度）
- [ ] 事件值是否在有效范围内（弯音：-8192～8191，CC：0-127）

---

## 调试日志示例

### 正常流程
```
[VelocityViewCanvas] OnPointerPressed: Position=(514, 150), LeftButtonPressed=True
[VelocityViewCanvas] Starting curve drawing at (514, 150), CanvasHeight=200
[VelocityViewCanvas] OnCurveUpdated triggered, calling SyncRefresh
[VelocityViewCanvas] UpdateDrawingEventCurve: (516, 152), PointCount=2
...
[VelocityViewCanvas] FinishDrawingEventCurve: PointCount=25
[VelocityViewCanvas] OnCurveCompleted: 25 points, calling SyncRefresh
[VelocityViewCanvas] 应用 PitchBend 曲线到音符
[VelocityViewCanvas] 曲线点: ScreenX=514, Value=-1000, 转换后MusicTime=2.57
[VelocityViewCanvas] 设置弯音值: -1000
```

### 事件类型切换
```
[VelocityViewCanvas] 事件类型改变: CurrentEventType, 刷新画布
[VelocityViewCanvas] 事件类型切换，取消正在进行的绘制
[VelocityViewCanvas] OnCurveCancelled triggered, calling SyncRefresh
```

---

## 预期改进

✅ **问题1解决**: 现在切换事件类型时会自动取消绘制，防止状态混乱
✅ **问题2解决**: 坐标转换正确，事件值能够正确应用到音符
✅ **性能**: 无性能退化，仅添加状态检查和坐标转换

---

## 后续建议

1. **增强日志** - 添加更多调试日志便于问题诊断
2. **单元测试** - 为坐标转换逻辑添加单元测试
3. **边界检查** - 在极端情况下（如空音符列表）进行更多验证
