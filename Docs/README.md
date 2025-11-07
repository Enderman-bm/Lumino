# Lumino 项目文档

## 📖 文档概览

本文件夹包含 Lumino 项目的完整文档，涵盖日志库合并、优化和使用指南。

### 🎯 快速导航

- **[项目状态](./PROJECT_STATUS.md)** - 🔴 **从这里开始** - 最新的项目状态和完成情况
- **[优化完成](./OPTIMIZATION_COMPLETE.md)** - 最新的优化总结报告
- **[使用指南](./ENDERLOGGER_USAGE_GUIDE.md)** - EnderDebugger 日志库的使用方法
- **[合并总结](./MERGE_SUMMARY.md)** - 合并工作简要说明

### 📚 完整文档列表

#### 1. [MERGE_SUMMARY.md](./MERGE_SUMMARY.md)
简明的合并总结，包含：
- 项目概述
- 完成的任务清单
- 编译结果
- 项目结构对比
- 技术改进说明

**适合**: 快速了解合并内容

#### 2. [MERGE_COMPLETE_REPORT.md](./MERGE_COMPLETE_REPORT.md)
详细的合并完成报告，包含：
- 项目概述
- 完成内容详解
- 编译结果
- 输出文件清单
- 功能对比
- 技术实现说明
- 依赖关系

**适合**: 深入了解合并细节

#### 3. [ENDERLOGGER_USAGE_GUIDE.md](./ENDERLOGGER_USAGE_GUIDE.md)
EnderDebugger 的使用指南，包含：
- 项目合并说明
- 使用 EnderLogger 进行日志查看
- 命令行选项
- 日志文件位置
- 支持的日志格式
- 颜色编码
- 关键类和方法
- 示例代码
- 迁移指南
- 故障排除

**适合**: 学习如何使用 EnderDebugger

#### 4. [MERGE_VERIFICATION_CHECKLIST.md](./MERGE_VERIFICATION_CHECKLIST.md)
合并验证清单，包含：
- 所有完成任务的检查
- 编译结果验证
- 项目结构确认
- 功能验证
- 向后兼容性检查

**适合**: 验证合并的完整性

### 🎯 快速开始

#### 如果你想...

**了解发生了什么**
→ 查看 [MERGE_SUMMARY.md](./MERGE_SUMMARY.md)

**学习如何使用日志系统**
→ 查看 [ENDERLOGGER_USAGE_GUIDE.md](./ENDERLOGGER_USAGE_GUIDE.md)

**深入了解实现细节**
→ 查看 [MERGE_COMPLETE_REPORT.md](./MERGE_COMPLETE_REPORT.md)

**验证合并的正确性**
→ 查看 [MERGE_VERIFICATION_CHECKLIST.md](./MERGE_VERIFICATION_CHECKLIST.md)

### ✨ 关键信息概览

#### 项目结构
```
Lumino.sln
├── Lumino                 (主应用)
├── MidiReader            (依赖库)
├── EnderDebugger         (日志库 ← 所有日志功能集中在这里)
├── EnderWaveTableAccessingParty
└── EnderAudioAnalyzer
```

#### 日志系统功能

**EnderDebugger** 库现在包含：

1. **日志记录**
   - Debug、Info、Warn、Error、Fatal 五个级别
   - 异常日志记录
   - 调试模式支持

2. **日志查看**
   - 读取现有日志
   - 日志级别过滤
   - 日志搜索
   - 多格式解析（新格式、旧格式、JSON）

3. **输出处理**
   - 彩色控制台输出
   - 文件输出
   - JSON 格式输出

#### 使用方式

**在代码中使用**
```csharp
using EnderDebugger;

var logger = EnderLogger.Instance;
var logs = logger.ReadExistingLogs(new LogViewerConfig 
{
    EnabledLevels = new HashSet<string> { "ERROR" },
    MaxLines = 500
});
```

#### 命令行工具

EnderDebugger 包含了 LogViewerProgram 的所有功能，可以通过：
- 创建独立的 Main 入口调用 LogViewerProgram.Main()
- 或者在 Lumino 的其他工具中集成

### 🔗 相关资源

- **项目根目录**: `/`
- **EnderDebugger 库**: `/EnderDebugger/`
- **日志输出位置**: `/EnderDebugger/Logs/`

### 📝 版本信息

- **合并日期**: 2025年11月7日
- **完成状态**: ✅ 完成
- **编译状态**: ✅ 成功 (0 错误)

---

**如有问题，请参考相应的文档文件。** 📖
