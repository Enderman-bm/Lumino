# 关闭文件功能 (Close File Feature)

## 概述

为Lumino编辑器添加了关闭打开的MIDI文件或工程文件的功能。用户现在可以通过菜单或快捷键关闭当前打开的文件，系统会检查未保存的更改并清空编辑界面。

## 功能特性

### 1. 关闭文件命令 (CloseFileAsync)

**位置**: `Lumino\ViewModels\MainWindowViewModel.cs`

**功能**:
- 检查是否有未保存的更改
- 如果有未保存更改，提示用户确认
- 清空当前文件名和文件大小显示
- 清空钢琴卷帘内容
- 重置音轨选择器
- 清空音轨总览视图
- 显示成功提示对话框

**代码流程**:
```csharp
[RelayCommand]
private async Task CloseFileAsync()
{
    // 1. 检查未保存更改
    if (!await _applicationService.CanShutdownSafelyAsync())
    {
        // 显示确认对话框
        var shouldProceed = await _dialogService.ShowConfirmationDialogAsync(...);
        if (!shouldProceed)
            return;  // 用户取消关闭
    }

    // 2. 清空UI状态
    CurrentOpenedFileName = string.Empty;
    CurrentOpenedFileSizeText = string.Empty;

    // 3. 清空编辑内容
    PianoRoll?.ClearContent();
    TrackSelector?.ClearTracks();
    // TrackOverview 自动跟随更新

    // 4. 显示成功消息
    await _dialogService.ShowInfoDialogAsync("成功", "文件已关闭。");
}
```

### 2. 用户界面集成

**位置**: `Lumino\Views\MainWindow.axaml`

**菜单项**:
- 在"文件"菜单中添加"关闭"菜单项
- 位置：保存(Save) 和 导入MIDI(Import MIDI) 之间
- 绑定到 `{Binding CloseFileCommand}`

**菜单结构**:
```xml
<MenuItem Header="文件">
    <MenuItem Header="新建" Command="{Binding NewFileCommand}"/>
    <MenuItem Header="打开" Command="{Binding OpenFileCommand}"/>
    <MenuItem Header="保存" Command="{Binding SaveFileCommand}"/>
    <MenuItem Header="关闭" Command="{Binding CloseFileCommand}"/>
    <Separator/>
    <MenuItem Header="导入MIDI" Command="{Binding ImportMidiFileCommand}"/>
    ...
</MenuItem>
```

## 工作流程

### 场景1: 关闭未修改的文件
1. 用户点击"文件" → "关闭"
2. 系统检查未保存更改（返回true，无更改）
3. 清空所有UI内容
4. 显示"文件已关闭"提示
5. 界面回到初始空状态

### 场景2: 关闭有未保存更改的文件
1. 用户点击"文件" → "关闭"
2. 系统检查未保存更改（返回false，有更改）
3. 显示确认对话框："当前项目有未保存的更改，是否关闭而不保存？"
   - 如果用户点击"是"：继续关闭流程
   - 如果用户点击"否"：取消操作，返回编辑
4. 清空所有UI内容
5. 显示"文件已关闭"提示

## 技术实现细节

### MVVM模式
- 使用`RelayCommand`特性标记命令方法
- MVVM Toolkit会自动生成`CloseFileCommand`属性
- 命令可直接绑定到UI元素

### 状态管理
- `CurrentOpenedFileName`: 追踪当前打开的文件名
- `CurrentOpenedFileSizeText`: 追踪当前文件大小显示
- 这两个属性在关闭时都被重置为空字符串

### UI更新
- 使用`Avalonia.Threading.Dispatcher.UIThread.InvokeAsync`确保UI操作在主线程上执行
- `PianoRoll.ClearContent()`: 清空所有音符和相关编辑状态
- `TrackSelector.ClearTracks()`: 清空所有音轨定义

### 事件管理
- 暂时移除`TrackSelector.PropertyChanged`事件处理程序（防止UI更新期间的事件触发）
- 清空完成后重新建立事件监听

## 与其他功能的关联

### NewFileAsync
- `NewFileAsync` 创建新项目时也会调用类似的清空逻辑
- 但`CloseFileAsync`不创建新的ViewModel，只是清空现有内容

### OpenFileAsync
- 用户在打开新文件前会自动触发未保存更改检查
- 如果用户确认打开新文件，旧文件会被自动替换（不需要显式关闭）
- `CloseFileAsync`提供了显式关闭的选项

### SaveFileAsync
- 用户可以先保存文件再关闭
- 或在关闭时选择不保存

## 日志记录

所有关键操作都有日志记录便于调试：
```csharp
_logger.Debug("MainWindowViewModel", "开始执行关闭文件命令");
_logger.Info("MainWindowViewModel", "清空项目内容");
_logger.Info("MainWindowViewModel", "文件已关闭，项目内容已清空");
```

## 异常处理

- 使用try-catch包装整个操作
- 捕获所有异常并显示错误对话框
- 记录异常详情到日志系统

## 快捷键支持 (Future)

虽然当前实现中未定义快捷键，但由于使用了MVVM Toolkit的`RelayCommand`，
添加快捷键支持非常简单：

```xaml
<!-- 在 MainWindow.axaml 中添加快捷键绑定 -->
<KeyBinding Gesture="Ctrl+W" Command="{Binding CloseFileCommand}"/>
```

## 编译验证

✅ 项目编译成功
- Lumino 成功，出现 89 警告 (12.3 秒)
- 未引入新的编译错误
- 与现有89个警告相同

## 测试场景

### 基础测试
- [ ] 点击"文件" → "关闭"
- [ ] 验证没有打开文件时的行为
- [ ] 验证打开文件后可以关闭

### 未保存更改测试
- [ ] 添加音符后关闭，检查确认对话框
- [ ] 确认对话框中选择"是"，验证文件被关闭
- [ ] 确认对话框中选择"否"，验证返回编辑

### UI状态验证
- [ ] 验证文件名显示被清空
- [ ] 验证文件大小显示被清空
- [ ] 验证音符和音轨都被清空
- [ ] 验证界面回到初始状态

### 边界情况
- [ ] 快速点击多次关闭按钮
- [ ] 在UI刷新过程中关闭
- [ ] 关闭后立即打开新文件

## 相关文件

1. **修改文件**:
   - `d:\source\Lumino\Lumino\ViewModels\MainWindowViewModel.cs` (添加CloseFileAsync方法)
   - `d:\source\Lumino\Lumino\Views\MainWindow.axaml` (添加菜单项)

2. **相关类**:
   - `MainWindowViewModel`: 命令和业务逻辑
   - `IApplicationService`: 检查未保存更改
   - `IDialogService`: 显示对话框
   - `PianoRollViewModel`: 钢琴卷帘视图模型
   - `TrackSelectorViewModel`: 音轨选择器视图模型

## 未来改进

1. **快捷键**: 添加 Ctrl+W 快捷键支持
2. **最近文件**: 添加"最近打开的文件"菜单项
3. **多标签页**: 如果编辑器升级为多标签页支持
4. **项目恢复**: 添加自动保存和项目恢复功能
5. **关闭全部**: 添加"关闭所有文件"命令

## 参考资源

- MVVM Toolkit RelayCommand: https://learn.microsoft.com/zh-cn/dotnet/communitytoolkit/mvvm/
- Avalonia File Operations: https://docs.avaloniaui.net/docs/guides/data-binding
- Lumino项目架构: 见 `Docs/ARCHITECTURE.md`
