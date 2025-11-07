# 事件绘制修复 - 用户验证指南

## 应用已启动 ✅

应用程序 Lumino v1.0.0.0 已成功启动，您现在可以进行修复验证。

---

## 🧪 验证测试流程

### 准备工作
1. 应用已启动（Lumino主窗口）
2. 打开或创建一个 MIDI 项目
3. 确保编辑器中有至少 2-3 个音符
4. 打开"输出"窗口查看调试日志（可选但推荐）

---

## 🔍 测试 1: 绘制时指示块显示

### 目标
验证在绘制事件曲线时，已有的指示块仍然显示，且新绘制的曲线也同时显示

### 步骤

```
1. 打开编辑器，选择"弯音"模式
2. 使用铅笔工具在音符区域绘制曲线
3. 观察橙色指示块和正在绘制的曲线
4. 完成绘制（释放鼠标）
5. 观察指示块高度是否已更新
```

### 预期结果
- ✅ 绘制中：能同时看到橙色指示块和黑色曲线
- ✅ 完成后：指示块高度更新为绘制曲线的高度
- ✅ 如果绘制的是 CC，应该看到绿色指示块而非橙色

### 故障排查
| 症状 | 原因 | 解决方案 |
|------|------|--------|
| 绘制时指示块完全消失 | 绘制代码问题 | 检查 DrawPitchBendCurve 方法 |
| 绘制完成后指示块高度不变 | 坐标转换问题 | 检查 OnCurveCompleted 中的转换逻辑 |
| 指示块位置错误 | GetX/GetWidth 计算问题 | 检查 NoteViewModel 坐标方法 |

---

## 🔄 测试 2: 事件类型切换

### 目标
验证切换事件类型时，正在进行的绘制立即停止，且显示正确的指示块类型

### 步骤

```
1. 打开编辑器，选择"弯音"模式
2. 开始在音符区域绘制曲线（不要完成）
3. 绘制中途点击切换到"CC"模式（或按快捷键）
4. 观察画面变化
5. 尝试继续绘制（应该开始新的CC曲线）
```

### 预期结果
- ✅ 绘制中途切换时，弯音曲线立即停止（不会应用数据）
- ✅ 画面立即显示CC指示块而不是弯音指示块
- ✅ 可以立即开始新的CC曲线绘制
- ✅ 输出日志显示：`[VelocityViewCanvas] 事件类型切换，取消正在进行的绘制`

### 故障排查
| 症状 | 原因 | 解决方案 |
|------|------|--------|
| 切换时曲线继续显示 | 取消绘制逻辑未触发 | 检查 OnViewModelPropertyChanged |
| 画面仍显示旧指示块 | 刷新未调用 | 检查 SyncRefresh 是否被调用 |
| 无法继续绘制新曲线 | 状态未重置 | 检查 CancelDrawing 是否完全重置 |

---

## 📊 测试 3: 数值准确性

### 目标
验证绘制的曲线数值是否正确应用到音符模型

### 步骤

```
1. 打开编辑器，选择"弯音"模式
2. 创建一条从中间位置（值=0）到顶部（值=8191）的弯音曲线
3. 完成绘制
4. 打开输出窗口，查看日志
5. 验证每个音符的弯音值是否被正确设置
```

### 预期结果
- ✅ 输出日志显示类似：
  ```
  [VelocityViewCanvas] 应用 PitchBend 曲线到音符
  [VelocityViewCanvas] 曲线点: ScreenX=514, Value=-1000, 转换后MusicTime=2.57
  [VelocityViewCanvas] 设置弯音值: -1000
  [VelocityViewCanvas] 设置弯音值: -500
  [VelocityViewCanvas] 设置弯音值: 0
  ```
- ✅ 不同的音符获得不同的弯音值（对应曲线的形状）
- ✅ 弯音值在有效范围内 (-8192 ~ 8191)
- ✅ CC值在有效范围内 (0 ~ 127)

### 故障排查
| 症状 | 原因 | 解决方案 |
|------|------|--------|
| 没有"设置弯音值"日志 | 音符未被找到 | 检查坐标转换逻辑 |
| 所有音符值相同 | 曲线点未正确分配 | 检查时间值转换 |
| 值超出范围 | 数值限制失效 | 检查 ClampValue 方法 |

---

## 🔧 测试 4: 极端情况

### 场景 4.1: 快速切换事件类型
```
1. 快速连续切换"弯音"→"CC"→"弯音"
2. 观察是否有崩溃或异常
3. 查看是否正确显示当前事件类型
```

### 场景 4.2: 边界处绘制
```
1. 在画布最左边/最右边绘制曲线
2. 在画布最上面/最下面绘制曲线
3. 观察是否有坐标计算错误
```

### 场景 4.3: 没有音符时绘制
```
1. 删除所有音符
2. 尝试绘制曲线
3. 观察是否安全处理（不应崩溃）
```

---

## 📋 调试日志参考

### 正常流程的日志签名
```
[VelocityViewCanvas] OnPointerPressed: Position=(514, 150), LeftButtonPressed=True
[VelocityViewCanvas] Starting curve drawing at (514, 150), CanvasHeight=200
[VelocityViewCanvas] IsDrawing=True

[VelocityViewCanvas] OnCurveUpdated triggered, calling SyncRefresh
[VelocityViewCanvas] UpdateDrawingEventCurve: (516, 152), PointCount=2

[VelocityViewCanvas] FinishDrawingEventCurve: PointCount=25
[VelocityViewCanvas] OnCurveCompleted: 25 points, calling SyncRefresh
[VelocityViewCanvas] 应用 PitchBend 曲线到音符
```

### 切换事件类型的日志
```
[VelocityViewCanvas] 事件类型改变: CurrentEventType, 刷新画布
[VelocityViewCanvas] 事件类型切换，取消正在进行的绘制
[VelocityViewCanvas] OnCurveCancelled triggered, calling SyncRefresh
```

### 异常日志
```
[VelocityViewCanvas] 绘制弯音指示块错误: XXX
[VelocityViewCanvas] OnCurveCompleted 错误: XXX
[VelocityViewCanvas] 未找到时间位置 2.57 对应的音符
```

---

## ✅ 验证完成检查表

完成所有测试后，请检查以下项目：

- [ ] **测试 1**: 绘制时指示块正常显示
  - [ ] 绘制中指示块可见
  - [ ] 绘制完成后指示块高度更新
  - [ ] 弯音显示橙色，CC显示绿色

- [ ] **测试 2**: 事件类型切换正常
  - [ ] 中途切换时绘制停止
  - [ ] 显示切换后的指示块
  - [ ] 能继续绘制新曲线
  - [ ] 日志显示"事件类型切换，取消正在进行的绘制"

- [ ] **测试 3**: 数值准确性正确
  - [ ] 日志显示曲线点被正确应用
  - [ ] 不同音符获得不同的值
  - [ ] 值在有效范围内

- [ ] **测试 4**: 极端情况处理得当
  - [ ] 无崩溃或异常
  - [ ] 坐标计算正确
  - [ ] 安全处理边界情况

---

## 📞 报告问题

如遇到问题，请提供以下信息：

1. **问题描述**: 具体发生了什么
2. **复现步骤**: 如何重现问题
3. **输出日志**: 相关的调试日志片段
4. **截图**: 问题时的画面截图（如适用）
5. **系统信息**: Windows/其他系统，.NET版本

---

## 📚 相关文档

- `DRAWING_STATE_FIX_REPORT.md` - 详细的修复报告
- `QUICK_FIX_SUMMARY.md` - 修复摘要
- 代码中的注释 - `VelocityViewCanvas.cs` 中的详细说明

---

**验证日期**: 2025-11-02  
**修复版本**: 1.0  
**状态**: 等待用户验证反馈

祝您测试顺利！ 🚀
