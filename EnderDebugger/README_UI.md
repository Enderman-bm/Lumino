# EnderDebugger UI 日志查看器

## 概述
EnderDebugger 已成功转换为 Avalonia UI 应用程序,提供了美观的图形界面来显示和管理日志。

## 主要功能

### ✅ 日志显示
- **实时日志更新**: 日志会自动显示在UI窗口中,不再输出到控制台
- **多级别支持**: Debug、Info、Warn、Error、Fatal
- **彩色标签**: 不同级别的日志使用不同颜色的标签
  - Debug: 灰色 (#808080)
  - Info: 绿色 (#008000)
  - Warn: 橙色 (#FFA500)
  - Error: 红色 (#FF0000)
  - Fatal: 深红色 (#8B0000)

### ✅ UI 布局
- **时间戳**: 精确到毫秒 (yyyy-MM-dd HH:mm:ss.fff)
- **级别标签**: 彩色圆角背景,清晰醒目
- **来源和事件类型**: 蓝色高亮显示
- **日志消息**: Consolas 字体,易于阅读
- **白色卡片**: 每条日志都有独立的背景卡片

### ✅ 过滤和搜索
- **级别过滤**: 下拉菜单选择要显示的日志级别
- **文本搜索**: 搜索框支持按消息、来源或事件类型过滤
- **实时过滤**: 过滤条件改变时立即更新显示

### ✅ 工具栏功能
- **清空日志**: 清除所有日志记录
- **保存日志**: 将日志导出为文本文件
- **自动滚动**: 可选择是否自动滚动到最新日志

### ✅ 状态栏
- **最新日志预览**: 显示最新接收到的日志摘要
- **总日志数**: 显示总共记录的日志数量
- **显示日志数**: 显示经过过滤后的日志数量

## 技术实现

### 架构
- **MVVM 模式**: 使用 CommunityToolkit.Mvvm
- **事件驱动**: 通过 LogEntryAdded 事件实时更新UI
- **线程安全**: 使用 Dispatcher.UIThread 确保UI线程安全

### 关键组件
1. **LogViewerWindow.axaml**: UI界面定义
2. **LogViewerViewModel.cs**: 业务逻辑和数据管理
3. **EnderLogger.cs**: 核心日志引擎,支持UI事件通知

### 输出方式
- **UI 显示**: 通过 LogEntryAdded 事件发送到UI (主要)
- **文件记录**: 保存到 EnderDebugger/Logs 目录
- **控制台输出**: 已禁用,所有日志仅通过UI显示

## 使用方法

### 启动应用
```powershell
cd EnderDebugger
dotnet run
```

### 查看日志
1. 启动后会自动显示测试日志
2. 使用级别过滤器筛选日志
3. 使用搜索框查找特定内容
4. 点击"保存日志"导出日志文件

### 清空日志
点击"清空日志"按钮清除所有显示的日志记录

### 自动滚动
勾选"自动滚动到底部"选项,新日志会自动滚动到可见区域

## 测试日志
应用启动时会自动生成以下测试日志:
- Debug: 应用程序启动
- Info: UI加载完成
- Info: 日志查看器初始化
- Debug: 事件订阅
- Info: 系统就绪
- Warn: 测试警告
- Error: 测试错误

## 集成说明
EnderLogger 可以在其他项目中使用,只需:
1. 引用 EnderDebugger 项目
2. 使用 `EnderLogger.Instance` 获取单例
3. 调用日志方法: `Debug()`, `Info()`, `Warn()`, `Error()`, `Fatal()`
4. 日志会自动显示在 EnderDebugger UI 窗口中

## 文件结构
```
EnderDebugger/
├── App.axaml                    # 应用程序定义
├── App.axaml.cs                 # 应用程序代码
├── Program.cs                   # 程序入口点
├── EnderLogger.cs               # 核心日志引擎
├── ViewLocator.cs               # MVVM视图定位器
├── ViewModels/
│   ├── ViewModelBase.cs         # ViewModel基类
│   └── LogViewerViewModel.cs    # 日志查看器ViewModel
├── Views/
│   └── LogViewerWindow.axaml    # 日志查看器UI
│   └── LogViewerWindow.axaml.cs # 日志查看器代码
└── Logs/                        # 日志文件目录
```

## 已完成的改进
✅ 移除控制台输出,只使用UI显示
✅ 优化日志条目视觉样式
✅ 添加彩色级别标签
✅ 实时状态更新
✅ 完整的过滤和搜索功能
✅ 日志保存功能
✅ 自动滚动功能

## 下一步
- [ ] 与 Lumino 主应用集成测试
- [ ] 添加更多日志导出格式 (JSON, CSV)
- [ ] 实现日志级别统计图表
- [ ] 添加日志高亮和标记功能
