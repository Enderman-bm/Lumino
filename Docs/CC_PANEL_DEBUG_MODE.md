# CC面板Debug模式控制

## 概述

实现了一个条件启用的CC面板系统：
- **正常模式**：CC面板被禁用，显示"开发中"提示
- **Debug模式**：CC面板完全启用，可正常使用

## 实现方案

### 1. 应用启动时检测Debug标志

在 `App.axaml.cs` 中添加静态属性：

```csharp
/// <summary>
/// 是否处于调试模式 - 用于控制开发中的功能（如CC面板）
/// </summary>
public static bool IsDebugMode { get; set; } = false;
```

在 `OnFrameworkInitializationCompleted` 方法中检测 `--debug` 参数：

```csharp
if (desktop.Args?.Contains("--debug") == true)
{
    IsDebugMode = true;
    // 输出启动字符画...
}
```

### 2. 事件视图面板的条件显示

在 `EventViewPanel.axaml.cs` 的 `OnPropertyChanged` 方法中添加逻辑：

```csharp
else if (e.Property == IsEventViewVisibleProperty && e.NewValue is bool isVisible)
{
    // 只有在Debug模式下才显示CC面板，否则禁用
    if (App.IsDebugMode)
    {
        this.IsVisible = isVisible;
        EnderLogger.Instance.Info("Visibility", $"[EventViewPanel] Debug模式已启用...");
    }
    else
    {
        this.IsVisible = false;
        EnderLogger.Instance.Info("Visibility", "[EventViewPanel] 正常模式下CC面板已禁用...");
    }
}
```

### 3. UI提示区域

在 `PianoRollView.axaml` 中添加"开发中"提示边框，与CC面板在同一网格行：

```xml
<!-- 开发中提示区域（仅在非Debug模式显示） -->
<Border Grid.Row="4"
        x:Name="DevelopmentNoticeBorder"
        Background="#1E1E1E"
        BorderBrush="#404040"
        BorderThickness="0,1,0,1">
    <Grid>
        <StackPanel VerticalAlignment="Center" 
                   HorizontalAlignment="Center"
                   Orientation="Vertical"
                   Spacing="12">
            <TextBlock Text="🔧 CC绘制面板" 
                      FontSize="16"
                      FontWeight="Bold"
                      Foreground="#FFD700"
                      HorizontalAlignment="Center"/>
            <TextBlock Text="此功能仅在Debug模式下可用" 
                      FontSize="13"
                      Foreground="#A0A0A0"
                      HorizontalAlignment="Center"/>
            <TextBlock Text="使用 --debug 参数启动应用以启用此功能" 
                      FontSize="11"
                      Foreground="#707070"
                      HorizontalAlignment="Center"
                      FontStyle="Italic"/>
        </StackPanel>
    </Grid>
</Border>
```

## 使用方式

### 正常模式运行（CC面板禁用）
```bash
dotnet run --project Lumino/Lumino.csproj
```
**结果**：显示"🔧 CC绘制面板 - 此功能仅在Debug模式下可用"提示

### Debug模式运行（CC面板启用）
```bash
dotnet run --project Lumino/Lumino.csproj -- --debug
```
**结果**：CC面板完全启用，可以编辑CC事件

## 文件修改

### 修改的文件
1. **Lumino/App.axaml.cs**
   - 添加 `IsDebugMode` 静态属性
   - 在应用初始化时检测 `--debug` 参数

2. **Lumino/Views/Controls/EventViewPanel.axaml.cs**
   - 修改 `OnPropertyChanged` 方法，添加Debug模式检查
   - 非Debug模式下强制隐藏面板

3. **Lumino/Views/Controls/PianoRollView.axaml**
   - 添加"开发中"提示区域
   - 提示用户如何启用此功能

### 新增文件
1. **Docs/EVENT_PANEL_ENHANCEMENT.md** - 事件绘制面板增强文档

## 日志输出

应用会在启动时输出相关日志：

**正常模式**：
```
[INFO] [EventViewPanel] 正常模式下CC面板已禁用（仅Debug模式可用）。
```

**Debug模式**：
```
[INFO] [EventViewPanel] Debug模式已启用，IsEventViewVisible 已更新为 True。
```

## 技术优势

1. **条件编译的轻量化替代方案**
   - 不需要修改项目文件
   - 运行时动态控制
   - 更灵活的开发工作流

2. **用户友好的提示**
   - 明确告知用户功能状态
   - 提供启用方法说明
   - 视觉上突出开发中状态

3. **保留完整功能**
   - Debug模式下无任何限制
   - 易于测试和调试
   - 便于功能开发完善

## 后续扩展

可以将此模式应用到其他开发中的功能：

```csharp
// 示例：其他开发中的功能
if (App.IsDebugMode)
{
    // 启用高级编辑工具
    // 启用性能分析面板
    // 启用实验性渲染模式
}
```

## 总结

通过简单但有效的Debug模式控制，我们实现了：
- ✅ CC面板的条件启用
- ✅ 清晰的用户提示
- ✅ 灵活的开发工作流
- ✅ 易于扩展的架构

---

**实现日期**: 2025年11月9日
**提交信息**: Add CC panel debug mode control - enable only with --debug parameter
**编译状态**: ✅ 成功，0个错误
