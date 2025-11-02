# 力度绘制界面修复 - 快速参考

## 问题
绘制力度时，力度条消失，用户无法参考现有值。

## 根因
`DrawVelocityBars` 方法在绘制时过早返回，导致力度条被隐藏。

## 修复
改为分层渲染：
1. 先绘制力度条
2. 再绘制编辑预览
3. 最后绘制曲线预览

## 修改代码

### 修复前
```csharp
// ❌ 错误：直接返回，隐藏力度条
if (ViewModel.EventCurveDrawingModule?.IsDrawing == true)
{
    _curveRenderer.DrawEventCurve(...);
    return;  // ← 导致力度条隐藏
}
```

### 修复后
```csharp
// ✅ 正确：先显示力度条，再显示曲线预览

// 第 1 层：力度条
foreach (var note in visibleNotes)
{
    _velocityRenderer.DrawVelocityBar(...);
}

// 第 2 层：编辑预览
if (ViewModel.VelocityEditingModule?.IsEditingVelocity == true)
{
    _velocityRenderer.DrawEditingPreview(...);
}

// 第 3 层：曲线预览（在最后）
if (ViewModel.EventCurveDrawingModule?.IsDrawing == true)
{
    _curveRenderer.DrawEventCurve(...);
}
```

## 修改位置
- **文件**: `VelocityViewCanvas.cs`
- **方法**: `DrawVelocityBars`
- **行数**: 608-641

## 编译
✅ 成功 (0 错误)

## 效果
| 阶段 | 修复前 | 修复后 |
|------|-------|--------|
| 绘制时 | ❌ 力度条隐藏 | ✅ 力度条显示 + 曲线预览 |
| 完成后 | ❌ 数据丢失 | ✅ 数据保存 + 显示更新 |

## 用户体验
- ✅ 绘制时可参考现有力度值
- ✅ 实时看到曲线预览
- ✅ 完成后数据被正确保存
