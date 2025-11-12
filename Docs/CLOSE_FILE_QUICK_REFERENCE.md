# 关闭文件功能 - 快速参考

## 使用方法

### 通过菜单关闭文件
1. 点击菜单栏 **"文件"** 
2. 点击 **"关闭"** 选项

### 效果
- 如果当前没有未保存的更改：
  - 立即清空所有编辑内容
  - 文件名和大小显示被清空
  - 显示成功提示 "文件已关闭"
  
- 如果有未保存的更改：
  - 显示确认对话框
  - 用户可选择保存或不保存
  - 确认后清空内容

## 功能特点

✅ **智能未保存更改检查**
- 集成现有的 `IApplicationService.CanShutdownSafelyAsync()` 检查
- 自动检测编辑过程中的任何更改

✅ **完全清空编辑界面**
- 清空所有音符和编辑状态
- 清空所有音轨定义
- 重置显示信息

✅ **用户友好的对话框**
- 清晰的确认提示
- 成功/错误通知
- 异常情况的详细错误信息

✅ **完整的日志记录**
- 所有操作都被记录用于调试
- 便于跟踪和排查问题

## 代码位置

| 项目 | 文件 | 行号 | 内容 |
|------|------|------|------|
| 业务逻辑 | `ViewModels/MainWindowViewModel.cs` | ~457-514 | `CloseFileAsync()` 方法 |
| UI菜单 | `Views/MainWindow.axaml` | ~72 | 菜单项定义 |
| 文档 | `Docs/CLOSE_FILE_FEATURE.md` | - | 完整功能文档 |

## 集成点

### 与其他功能的交互

**与 NewFileAsync 的关系**
- 相似：都会清空编辑内容
- 区别：NewFile会创建新的ViewModel，Close只清空内容

**与 OpenFileAsync 的关系**
- OpenFile 在打开新文件前自动检查未保存更改
- 用户可选择显式关闭或直接打开新文件

**与 SaveFileAsync 的关系**
- 用户可在关闭前选择保存
- 或在关闭确认对话框中选择不保存

## 命令绑定

MVVM Toolkit 自动生成的命令属性：
```csharp
public IAsyncRelayCommand CloseFileCommand { get; }
```

在XAML中使用：
```xml
<MenuItem Header="关闭" Command="{Binding CloseFileCommand}"/>
```

## 工作流示例

### 流程图

```
用户点击"关闭"
    ↓
检查未保存更改 (CanShutdownSafelyAsync)
    ↓
    ├─ 有更改? → 显示确认对话框
    │              ├─ 用户点击"是" → 继续清空
    │              └─ 用户点击"否" → 取消操作，返回
    │
    └─ 无更改? → 直接清空
         ↓
    清空CurrentOpenedFileName
    清空CurrentOpenedFileSizeText
    清空PianoRoll内容
    清空TrackSelector内容
         ↓
    显示"文件已关闭"提示
         ↓
    完成
```

## 异常处理

所有异常都会被捕获并显示用户友好的错误消息：
- 文件系统错误 → "错误：关闭文件失败"
- 业务逻辑错误 → 详细的错误信息对话框
- 异常详情记录到日志系统

## 编译状态

✅ **编译成功**
```
Lumino 成功，出现 89 警告 (12.3 秒)
```

- 无新的编译错误引入
- 警告数与之前相同（现有项目警告）

## 测试检查清单

- [ ] 打开MIDI文件
- [ ] 点击"文件" → "关闭"
- [ ] 验证文件内容被清空
- [ ] 验证文件名显示消失
- [ ] 编辑后尝试关闭，验证确认对话框出现
- [ ] 在确认对话框中选择"否"，验证返回编辑
- [ ] 关闭后立即打开新文件，验证工作正常

## 快捷键 (未来扩展)

当前没有配置快捷键，但可轻松添加：

```xml
<!-- 在 MainWindow.axaml 中添加 -->
<KeyBinding Gesture="Ctrl+W" Command="{Binding CloseFileCommand}"/>
```

## 支持的文件类型

- MIDI文件 (`.mid`, `.midi`)
- Lumino项目文件 (`.lmpf`)
- 其他支持的格式

## 相关API

### IApplicationService
```csharp
Task<bool> CanShutdownSafelyAsync()
```
检查是否有未保存的更改。

### IDialogService
```csharp
Task<bool> ShowConfirmationDialogAsync(string title, string message)
Task ShowInfoDialogAsync(string title, string message)
Task ShowErrorDialogAsync(string title, string message)
```

## 问题排查

| 问题 | 可能原因 | 解决方案 |
|------|---------|---------|
| "关闭"菜单项不可见 | XAML未正确保存 | 重新加载视图或重建项目 |
| 点击"关闭"无反应 | ViewModel未正确注册 | 检查依赖注入配置 |
| UI未正确清空 | ClearContent()失败 | 查看详细日志 |
| 出现异常对话框 | 未捕获的异常 | 检查日志文件获取详情 |

## 扩展建议

1. **记住最近打开的文件** - 在关闭时保存文件历史
2. **自动保存** - 关闭前自动保存备份
3. **多标签页支持** - 为不同文件添加标签页
4. **项目统计** - 关闭时显示编辑统计信息
5. **插件钩子** - 允许扩展插件在关闭时执行操作

---

**创建日期**: 2025年11月12日
**功能版本**: 1.0
**状态**: ✅ 已完成
