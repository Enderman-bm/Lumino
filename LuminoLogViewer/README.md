# Lumino 日志查看器 - 美化日志输出

## 概述

本次任务成功美化了Lumino项目的日志输出系统，包括改进的控制台版本和全新的Avalonia UI界面。

## 完成的功能

### 1. 改进的控制台日志查看器 (`LuminoLogViewer`)

#### 主要特性：
- ✨ **多格式支持**：支持JSON、新格式、旧格式三种日志格式
- 🎨 **彩色输出**：不同日志级别使用不同颜色显示
- 🏷️ **级别过滤**：支持DEBUG、INFO、WARN、ERROR、FATAL级别过滤
- 🔍 **搜索功能**：实时搜索日志内容
- 📝 **实时监控**：自动跟踪文件变化
- ⚙️ **配置选项**：可自定义最大行数、时间戳显示等

#### 新增命令行参数：
```bash
--levels <levels>    # 指定日志级别 (DEBUG,INFO,WARN,ERROR,FATAL)
--search <term>      # 搜索日志内容
--max-lines <n>      # 最大显示行数
--no-follow          # 不跟踪文件变化
--no-timestamp       # 不显示时间戳
--help               # 显示帮助信息
```

#### 使用示例：
```bash
# 查看所有日志
LuminoLogViewer.exe

# 只查看INFO和ERROR级别，并搜索"error"
LuminoLogViewer.exe --levels INFO,ERROR --search "error"

# 显示最多500行，不跟踪文件变化
LuminoLogViewer.exe --max-lines 500 --no-follow
```

### 2. 新的Avalonia UI日志查看器

#### 主要特性：
- 🖥️ **现代化界面**：基于Avalonia的桌面应用
- 🔍 **实时搜索**：搜索框实时过滤日志
- 📊 **级别过滤**：通过单选按钮过滤日志级别
- 📁 **文件跟踪**：实时监控日志文件变化
- 💾 **导出功能**：支持导出日志到文件
- ⌨️ **快捷键支持**：F5刷新、Ctrl+S导出等

#### 界面功能：
- **工具栏**：搜索框、刷新、清除、导出、跟踪切换
- **过滤器**：日志级别选择、显示选项、最大行数设置
- **日志列表**：分列显示时间、级别、来源、组件、消息
- **状态栏**：显示当前状态、总日志数、文件路径

#### 快捷键：
- `F5` - 刷新日志
- `Ctrl+Delete` - 清除日志
- `Ctrl+S` - 导出日志
- `Ctrl+F` - 聚焦搜索框

## 技术实现

### 支持的日志格式

1. **JSON格式**：
```json
{"Timestamp":"2025-11-01T10:59:43.979Z","Level":"INFO","Component":"EnderLogger","Message":"调试模式已启用"}
```

2. **新格式**：
```
[10:59:43.979] [INFO] [EnderLogger] [Program] 调试模式已启用
```

3. **旧格式**：
```
[EnderDebugger][2025-10-01 11:41:03.454][App][EnderLogger]调试模式已启用
```

### 颜色编码

| 日志级别 | 颜色 | 说明 |
|---------|------|------|
| DEBUG | 亮青色 | 调试信息 |
| INFO | 亮绿色 | 一般信息 |
| WARN | 亮黄色 | 警告信息 |
| ERROR | 亮红色 | 错误信息 |
| FATAL | 亮紫色 | 致命错误 |

## 文件结构

### 控制台版本
- `LuminoLogViewer/Program.cs` - 增强的主程序

### Avalonia UI版本
- `Lumino/ViewModels/LogViewerViewModel.cs` - 视图模型
- `Lumino/Views/LogViewerWindow.axaml` - 窗口界面
- `Lumino/Views/LogViewerWindow.axaml.cs` - 代码后台

### 测试文件
- `LuminoLogViewer/test_log.txt` - 测试日志文件

## 使用建议

1. **开发阶段**：使用控制台版本进行实时监控
2. **调试阶段**：使用Avalonia UI版本进行详细分析
3. **生产环境**：部署控制台版本进行日志监控

## 扩展性

代码设计具有良好的扩展性：
- 支持新的日志格式解析
- 可添加更多过滤器选项
- 可集成到Lumino主程序中作为工具窗口
- 支持导出多种格式（日志、CSV等）

## 总结

本次优化显著提升了Lumino项目的日志查看体验：
- 提供了两种不同使用场景的日志查看器
- 支持多种日志格式的自动识别和美化显示
- 实现了强大的过滤和搜索功能
- 提供了现代化的桌面界面
- 增强了实时监控和导出功能

日志系统现在能够更好地支持开发、调试和运维工作。