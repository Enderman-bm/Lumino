# Lumino项目日志输出规范

## 概述

Lumino项目使用EnderDebugger作为统一的日志管理系统，提供结构化、分类的日志输出。本规范定义了日志输出的标准格式和使用方法。

## 日志系统架构

### EnderLogger类

EnderLogger是单例模式的日志管理器，负责：
- 统一日志格式化
- 多输出目标（调试控制台、控制台、文件）
- 日志级别控制
- 异常记录

### 日志级别

```csharp
public enum LogLevel
{
    Debug = 0,    // 调试信息，仅开发环境
    Info = 1,     // 一般信息
    Warn = 2,     // 警告信息
    Error = 3,    // 错误信息
    Fatal = 4     // 致命错误
}
```

## 使用方法

### 1. 获取Logger实例

```csharp
using EnderDebugger;

private readonly EnderLogger _logger = EnderLogger.Instance;
```

### 2. 日志输出方法

```csharp
// 调试日志
_logger.Debug("ComponentName", "调试信息内容");

// 信息日志
_logger.Info("ComponentName", "一般信息内容");

// 警告日志
_logger.Warn("ComponentName", "警告信息内容");

// 错误日志
_logger.Error("ComponentName", "错误信息内容");

// 致命错误日志
_logger.Fatal("ComponentName", "致命错误内容");

// 异常记录
_logger.LogException(ex, "ComponentName", "异常上下文信息");
```

### 3. 日志格式

所有日志输出遵循统一格式：
```
[EnderDebugger][2025-10-06 14:30:25.123][Source][ComponentName]日志内容
```

- **EnderDebugger**: 日志系统标识
- **时间戳**: yyyy-MM-dd HH:mm:ss.fff 格式
- **Source**: 日志来源（通常是类名或模块名）
- **ComponentName**: 事件类型或组件名称
- **日志内容**: 具体的日志消息

## 最佳实践

### 1. 事件类型命名规范

- 使用PascalCase命名
- 简洁明了，体现操作或事件类型
- 示例：`FileOperation`, `UIInteraction`, `DataProcessing`

### 2. 日志内容规范

- 使用中文描述，便于理解
- 包含关键上下文信息
- 避免敏感信息泄露
- 示例：
  ```csharp
  _logger.Info("FileOperation", $"成功加载文件: {fileName}, 大小: {fileSize} bytes");
  _logger.Error("DataProcessing", $"处理数据失败: ID={dataId}, 错误: {errorMessage}");
  ```

### 3. 日志级别使用指南

- **Debug**: 开发调试信息，生产环境通常关闭
- **Info**: 重要操作的成功完成、状态变化
- **Warn**: 潜在问题、不影响正常功能的情况
- **Error**: 功能失败、异常情况
- **Fatal**: 系统级严重错误，可能导致程序崩溃

### 4. 异常处理

```csharp
try
{
    // 风险操作
}
catch (Exception ex)
{
    _logger.LogException(ex, "ComponentName", "操作失败的上下文信息");
    // 处理异常
}
```

### 5. 性能考虑

- 避免在循环中输出大量Debug日志
- 日志内容格式化应避免复杂计算
- 大量数据输出时考虑分批或摘要

## 配置

### 调试模式

通过命令行参数控制：
- `--debug all`: 启用所有调试输出
- `--debug info`: 启用Info及以上级别
- 默认: Info级别及以上

### 日志文件

日志文件自动创建在项目Logs目录下：
- 文件名格式: `EnderDebugger_YYYYMMDD_HHMMSS.log`
- 每日自动轮转

## 代码示例

### ViewModel中的使用

```csharp
using EnderDebugger;

namespace Lumino.ViewModels
{
    public partial class MyViewModel : ViewModelBase
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;

        public MyViewModel()
        {
            _logger.Info("MyViewModel", "ViewModel已创建");
        }

        private void SomeOperation()
        {
            try
            {
                _logger.Debug("MyViewModel", "开始执行SomeOperation");
                // 执行操作
                _logger.Info("MyViewModel", "SomeOperation执行成功");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "MyViewModel", "SomeOperation执行失败");
            }
        }
    }
}
```

### 服务类中的使用

```csharp
using EnderDebugger;

namespace Lumino.Services
{
    public class MyService
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;

        public void ProcessData(object data)
        {
            _logger.Info("MyService", $"开始处理数据: {data.GetType().Name}");

            if (data == null)
            {
                _logger.Warn("MyService", "输入数据为空");
                return;
            }

            try
            {
                // 处理逻辑
                _logger.Info("MyService", "数据处理完成");
            }
            catch (ValidationException ex)
            {
                _logger.Error("MyService", $"数据验证失败: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "MyService", "数据处理异常");
                throw;
            }
        }
    }
}
```

## 迁移指南

### 从System.Diagnostics.Debug.WriteLine迁移

**原代码:**
```csharp
System.Diagnostics.Debug.WriteLine("[Component] 操作完成");
```

**新代码:**
```csharp
_logger.Debug("Component", "操作完成");
```

### 从Console.WriteLine迁移

**原代码:**
```csharp
Console.WriteLine("信息: 操作完成");
```

**新代码:**
```csharp
_logger.Info("Component", "操作完成");
```

### 从其他日志框架迁移

根据原有日志级别映射到EnderLogger对应的方法。

## 注意事项

1. **线程安全**: EnderLogger是线程安全的，可以在多线程环境中使用
2. **性能**: Debug级别日志在生产环境会自动关闭
3. **文件大小**: 日志文件会自动轮转，避免占用过多磁盘空间
4. **敏感信息**: 避免在日志中输出密码、密钥等敏感信息
5. **国际化**: 日志内容使用中文，便于团队理解和维护

## 常见问题

### Q: 为什么看不到Debug日志？
A: 检查是否启用了调试模式 (`--debug all`) 或者日志级别设置

### Q: 日志文件在哪里？
A: 默认在项目根目录的Logs文件夹下

### Q: 如何自定义日志格式？
A: 修改EnderLogger.LogInternal方法中的格式化逻辑

### Q: 如何添加新的日志输出目标？
A: 继承EnderLogger类并重写LogInternal方法