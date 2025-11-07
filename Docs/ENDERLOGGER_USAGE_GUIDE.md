# 合并后的 EnderDebugger 使用指南

## 项目合并说明

LuminoLogViewer 已完全合并到 EnderDebugger 库中。所有日志查看功能现在都集成在 EnderDebugger 中。

## 使用 EnderLogger 进行日志查看

### 方式一：从代码中调用

```csharp
using EnderDebugger;

// 获取单例实例
var logger = EnderLogger.Instance;

// 配置日志查看器
var config = new LogViewerConfig
{
    EnabledLevels = new HashSet<string> { "DEBUG", "INFO", "WARN", "ERROR", "FATAL" },
    SearchTerm = null,
    FollowFile = true,
    MaxLines = 1000,
    ShowTimestamp = true
};

// 读取现有日志
var logs = logger.ReadExistingLogs(config);

// 输出日志
foreach (var log in logs)
{
    Console.WriteLine(log);
}
```

### 方式二：运行独立程序

从命令行运行 LogViewerProgram（日志查看器程序）：

```powershell
# 基本用法
./LogViewerProgram.exe

# 指定日志级别
./LogViewerProgram.exe --levels ERROR,FATAL

# 搜索日志
./LogViewerProgram.exe --search "exception"

# 组合使用
./LogViewerProgram.exe --levels INFO,ERROR --search "error" --max-lines 500
```

## 命令行选项

| 选项 | 缩写 | 说明 | 示例 |
|------|------|------|------|
| --levels | -l | 指定日志级别 | --levels DEBUG,INFO,ERROR |
| --search | -s | 搜索日志内容 | --search "exception" |
| --max-lines | -n | 最大显示行数 | --max-lines 500 |
| --no-follow | -f | 不跟踪文件变化 | --no-follow |
| --no-timestamp | -t | 不显示时间戳 | --no-timestamp |
| --help | -h | 显示帮助 | --help |

## 日志文件位置

日志文件存储在：
```
{ProjectRoot}/EnderDebugger/Logs/
├── EnderDebugger_{timestamp}.log     # 主日志文件
└── LuminoLogViewer.log               # 日志查看器监控文件（JSON格式）
```

## 支持的日志格式

### JSON 格式
```json
{"Timestamp":"2025-11-07T10:30:00Z","Level":"INFO","Component":"MyComponent","Message":"操作成功"}
```

### 新格式
```
[10:30:00.123] [INFO] [MyComponent] [MyMethod] 操作成功
```

### 旧格式
```
[EnderDebugger][2025-11-07 10:30:00.123][MyComponent][MyMethod]操作成功
```

## 颜色编码

| 日志级别 | 颜色 | RGB 代码 |
|---------|------|---------|
| DEBUG | 亮青色 | 38;5;14 |
| INFO | 亮绿色 | 38;5;10 |
| WARN | 亮黄色 | 38;5;11 |
| ERROR | 亮红色 | 38;5;9 |
| FATAL | 亮紫色 | 38;5;13 |

## 关键类和方法

### EnderLogger 类

#### 静态属性
- `Instance`: 获取日志系统单例

#### 公共方法
- `Debug(string eventType, string content)`: 记录调试日志
- `Info(string eventType, string content)`: 记录信息日志
- `Warn(string eventType, string content)`: 记录警告日志
- `Error(string eventType, string content)`: 记录错误日志
- `Fatal(string eventType, string content)`: 记录致命错误日志
- `LogException(Exception exception, string eventType, string content)`: 记录异常
- `EnableDebugMode(string logLevel)`: 启用调试模式
- `DisableDebugMode()`: 禁用调试模式
- `ReadExistingLogs(LogViewerConfig config)`: 读取现有日志
- `GetCurrentLogFilePath()`: 获取当前日志文件路径
- `GetLogDirectory()`: 获取日志目录

### LogViewerConfig 类

```csharp
public class LogViewerConfig
{
    public HashSet<string> EnabledLevels { get; set; }  // 启用的日志级别
    public string SearchTerm { get; set; }              // 搜索词
    public bool FollowFile { get; set; }                // 是否监控文件变化
    public int MaxLines { get; set; }                   // 最大显示行数
    public bool ShowTimestamp { get; set; }             // 是否显示时间戳
}
```

### LogViewerProgram 类

```csharp
public sealed class LogViewerProgram
{
    // 作为独立程序的入口点
    [STAThread]
    public static void Main(string[] args)
}
```

## 示例代码

### 示例 1: 在应用中读取特定级别的日志

```csharp
using EnderDebugger;

var logger = EnderLogger.Instance;
var config = new LogViewerConfig
{
    EnabledLevels = new HashSet<string> { "ERROR", "FATAL" },
    MaxLines = 100,
    ShowTimestamp = true
};

var errorLogs = logger.ReadExistingLogs(config);
Console.WriteLine($"找到 {errorLogs.Count} 条错误日志");
```

### 示例 2: 搜索特定内容

```csharp
var config = new LogViewerConfig
{
    SearchTerm = "NullReferenceException",
    MaxLines = 500
};

var matchingLogs = logger.ReadExistingLogs(config);
foreach (var log in matchingLogs)
{
    Console.WriteLine(log);
}
```

### 示例 3: 启用调试模式

```csharp
// 启用调试模式，显示所有日志
logger.EnableDebugMode("all");

// 记录日志
logger.Info("MyComponent", "这是一条信息日志");
logger.Debug("MyComponent", "这是一条调试日志");

// 禁用调试模式
logger.DisableDebugMode();
```

## 注意事项

1. **调试模式**: 非调试模式下不会输出任何日志信息
2. **线程安全**: EnderLogger 是线程安全的单例
3. **日志文件**: 日志文件会自动创建和管理
4. **JSON 监听文件**: LuminoLogViewer.log 中的日志为 JSON 格式，用于程序化处理
5. **VT100 颜色支持**: 需要支持 VT100 的终端（Windows 10+、Linux、macOS）

## 迁移指南

如果之前使用 LuminoLogViewer 项目：

### 原来的引用
```xml
<ProjectReference Include="..\LuminoLogViewer\LuminoLogViewer.csproj" />
```

### 现在的引用
```xml
<ProjectReference Include="..\EnderDebugger\EnderDebugger.csproj" />
```

### 原来的代码
```csharp
// 运行独立程序
Process.Start("LuminoLogViewer.exe");
```

### 现在的代码
```csharp
// 方式1: 在代码中使用日志查看功能
var logs = EnderLogger.Instance.ReadExistingLogs(config);

// 方式2: 运行独立程序
Process.Start("LogViewerProgram.exe");
```

## 故障排除

### 找不到日志文件
- 检查日志目录是否存在: `{ProjectRoot}/EnderDebugger/Logs/`
- 确保应用程序有写入权限

### 看不到彩色输出
- 确保终端支持 VT100 转义序列（Windows 10+）
- 尝试运行: `chcp 65001` 以启用 UTF-8 支持

### 日志查看器没有更新
- 确保使用了 `--no-follow` 标志才能禁用实时监控
- 检查日志文件是否被其他程序占用

---
最后更新：2025年11月7日
