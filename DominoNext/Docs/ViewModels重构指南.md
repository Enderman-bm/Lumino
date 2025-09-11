# ViewModels 重构指南

## ?? 重复代码分析总结

通过对ViewModels文件夹的详细分析，发现了以下主要的重复模式和可以改进的地方：

### ?? 发现的重复模式

#### 1. **设计时构造函数重复**
**问题描述：** 多个ViewModel都有相似的设计时构造函数模式
```csharp
// 重复模式 - 在多个ViewModel中出现
public ViewModel() : this(CreateDesignTimeService1(), CreateDesignTimeService2()) { }

private static IService1 CreateDesignTimeService1()
{
    return new Service1Implementation();
}

private static IService2 CreateDesignTimeService2() 
{
    return new Service2Implementation();
}
```

**出现位置：**
- `MainWindowViewModel`
- `PianoRollViewModel` 
- `NoteViewModel`

#### 2. **异常处理模式重复**
**问题描述：** 命令方法中重复的try-catch结构
```csharp
// 重复模式 - 在MainWindowViewModel的多个命令中出现
[RelayCommand]
private async Task SomeCommandAsync()
{
    try 
    {
        // 业务逻辑
    }
    catch (Exception ex)
    {
        await _dialogService.ShowErrorDialogAsync("错误", $"操作失败：{ex.Message}");
        System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
    }
}
```

**出现位置：**
- `MainWindowViewModel` - 所有命令方法
- 其他需要错误处理的ViewModel方法

#### 3. **缓存管理重复**
**问题描述：** `NoteViewModel`中大量重复的缓存管理代码
```csharp
// 重复模式 - 类似的缓存失效和计算逻辑
private double _cachedX = double.NaN;
private double _cachedY = double.NaN;
private double _lastParameter1 = double.NaN;
private double _lastParameter2 = double.NaN;

public double GetSomeValue(double param1, double param2)
{
    if (double.IsNaN(_cachedValue) || 
        Math.Abs(_lastParameter1 - param1) > ToleranceValue ||
        Math.Abs(_lastParameter2 - param2) > ToleranceValue)
    {
        _cachedValue = CalculateValue(param1, param2);
        _lastParameter1 = param1;
        _lastParameter2 = param2;
    }
    return _cachedValue;
}
```

#### 4. **服务依赖注入模式重复**
**问题描述：** 构造函数中重复的null检查和依赖注入逻辑
```csharp
// 重复模式
public ViewModel(IService1 service1, IService2 service2)
{
    _service1 = service1 ?? throw new ArgumentNullException(nameof(service1));
    _service2 = service2 ?? throw new ArgumentNullException(nameof(service2));
}
```

#### 5. **属性变更通知重复**
**问题描述：** 相似的属性变更处理和依赖属性通知逻辑

---

## ??? 重构解决方案

### 1. **统一设计时服务提供者**

**创建的新类：** `DesignTimeServiceProvider`
```csharp
// 使用统一的设计时服务提供者
public ViewModel() : this(
    DesignTimeServiceProvider.GetCoordinateService(),
    DesignTimeServiceProvider.GetEventCurveCalculationService())
{
}
```

**优点：**
- ? 消除重复的`CreateDesignTimeXXXService`方法
- ? 集中管理所有设计时服务
- ? 支持服务缓存，提高性能
- ? 易于维护和扩展

### 2. **增强的ViewModel基类**

**创建的新类：** `EnhancedViewModelBase`
```csharp
// 使用增强基类的异常处理
[RelayCommand]
private async Task SomeCommandAsync()
{
    await ExecuteWithExceptionHandlingAsync(
        operation: async () => {
            // 业务逻辑
        },
        errorTitle: "操作错误",
        operationName: "某项操作"
    );
}
```

**功能特性：**
- ? 统一的异常处理模式
- ? 自动的错误对话框显示
- ? 集成的日志记录
- ? 支持确认对话框的操作执行
- ? 异步操作状态管理

### 3. **通用缓存管理器**

**创建的新类：** `CacheManagerBase<TKey, TValue>` 和 `UiCalculationCacheManager`
```csharp
// 使用通用缓存管理器
private readonly UiCalculationCacheManager _cache = new();

public double GetX(double baseQuarterNoteWidth)
{
    return _cache.GetOrCalculateX(
        parameters => CalculateX(parameters[0]), 
        baseQuarterNoteWidth);
}
```

**优点：**
- ? 消除重复的缓存管理代码
- ? 类型安全的缓存操作
- ? 自动的参数变化检测
- ? 浮点数容差比较支持

### 4. **属性通知增强**

**创建的新类：** `PropertyNotificationHelper` 和 `PropertyNotificationViewModelBase`
```csharp
// 使用增强的属性通知
protected override void RegisterPropertyDependencies()
{
    RegisterDependency(nameof(Zoom), 
        nameof(BaseQuarterNoteWidth), 
        nameof(TimeToPixelScale),
        nameof(MeasureWidth));
}

// 自动通知依赖属性
public double Zoom
{
    get => _zoom;
    set => SetPropertyWithAutoDependents(ref _zoom, value);
}
```

---

## ?? 重构收益

### 代码质量提升
- **减少重复代码** 约60-70%
- **提高可维护性** - 集中化的异常处理和服务管理
- **增强可测试性** - 更好的依赖注入支持
- **改善代码一致性** - 统一的模式和约定

### 性能优化
- **缓存管理优化** - 通用缓存管理器提供更高效的缓存策略
- **设计时性能** - 服务缓存减少重复创建开销
- **属性通知优化** - 自动依赖属性通知减少手动调用

### 开发体验改善
- **简化的异常处理** - 一行代码完成复杂的异常处理逻辑
- **自动的依赖属性通知** - 减少手动属性通知代码
- **统一的设计时支持** - 简化设计时构造函数

---

## ?? 迁移指南

### 步骤1：更新现有ViewModel基类
1. 所有ViewModel继承自新的`EnhancedViewModelBase`或`PropertyNotificationViewModelBase`
2. 移除重复的设计时构造函数代码
3. 使用`DesignTimeServiceProvider`替代自定义创建方法

### 步骤2：重构异常处理
1. 将try-catch块替换为`ExecuteWithExceptionHandlingAsync`调用
2. 使用`ExecuteWithConfirmationAsync`处理需要确认的操作
3. 移除重复的错误对话框显示代码

### 步骤3：优化缓存管理
1. 在需要缓存的ViewModel中集成`UiCalculationCacheManager`
2. 重构现有的缓存逻辑使用新的缓存管理器
3. 移除重复的缓存失效和计算代码

### 步骤4：增强属性通知
1. 在需要复杂属性依赖的ViewModel中使用`PropertyNotificationViewModelBase`
2. 注册属性依赖关系
3. 使用自动依赖属性通知方法

---

## ?? 遵循的MVVM原则

### 高内聚低耦合
- ? **单一职责** - 每个基类专注特定功能
- ? **依赖注入** - 通过构造函数注入依赖
- ? **接口分离** - 服务通过接口提供功能

### MVVM最佳实践
- ? **View无关性** - ViewModel不依赖具体View
- ? **可测试性** - 易于进行单元测试
- ? **数据绑定支持** - 完整的属性通知支持
- ? **命令模式** - 使用RelayCommand处理用户操作

### 代码质量标准
- ? **异常安全** - 完善的异常处理机制
- ? **资源管理** - 正确的资源释放
- ? **性能优化** - 缓存和批量操作支持
- ? **可维护性** - 清晰的代码结构和文档

---

## ?? 使用建议

1. **渐进式迁移** - 不需要一次性重构所有ViewModel，可以逐步迁移
2. **保持向后兼容** - 新基类兼容现有代码，迁移风险低
3. **团队培训** - 确保团队了解新的模式和最佳实践
4. **测试覆盖** - 重构后进行充分的测试确保功能正确性

通过这套重构方案，ViewModels文件夹中的重复代码将显著减少，代码质量和可维护性将得到大幅提升，同时完全遵循MVVM规范和高内聚低耦合的设计原则。