# 力度绘制修复 - 快速参考

## 问题
用铅笔工具在 Velocity 模式下绘制力度后，绘制的力度值无法保存到音符。

## 原因
`OnCurveCompleted` 方法中缺少 Velocity 事件类型的处理代码。

## 修复
在 `VelocityViewCanvas.cs` 的 `OnCurveCompleted` 方法中添加了 Velocity case 处理。

### 修复前
```csharp
case Lumino.ViewModels.Editor.Enums.EventType.Velocity:
    // Velocity 值在 UpdateDrawing 时已经处理过
    System.Diagnostics.Debug.WriteLine($"[VelocityViewCanvas] Velocity 值已在绘制时处理");
    break;
```

### 修复后
```csharp
case Lumino.ViewModels.Editor.Enums.EventType.Velocity:
    // 设置音符的速度值
    noteAtTime.Velocity = curvePoint.Value;
    System.Diagnostics.Debug.WriteLine($"[VelocityViewCanvas] 设置音符速度值: {curvePoint.Value}");
    break;
```

## 编译状态
✅ 成功 (0 错误)

## 验证清单
- ✅ 代码逻辑正确
- ✅ 与其他 case 保持一致
- ✅ 编译通过
- ⏳ 需要手动测试

## 文件修改
| 文件 | 行数 | 类型 |
|------|------|------|
| `VelocityViewCanvas.cs` | 345-348 | 替换 |

## 测试步骤
1. 在 Velocity 模式选择铅笔工具
2. 在力度面板上拖动鼠标绘制
3. 释放鼠标
4. **预期**: 力度条重新显示，音符 Velocity 值已更新
