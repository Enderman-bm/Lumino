# MainWindowViewModel MVVM规范修复总结

## 修复前的问题

### 1. 依赖注入缺失
```csharp
// ? 硬编码依赖
public MainWindowViewModel() : this(new Lumino.Services.Implementation.SettingsService())
```

### 2. ViewModel直接操作View
```csharp
// ? ViewModel不应该直接创建View
var settingsWindow = new SettingsWindow();
await settingsWindow.ShowDialog(desktop.MainWindow);
```

### 3. 应用程序生命周期管理混入ViewModel
```csharp
// ? ViewModel不应该直接操作应用程序生命周期
if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    desktop.Shutdown();
```

### 4. PianoRollViewModel硬编码创建
```csharp
// ? 直接new，无法控制依赖
public PianoRollViewModel PianoRoll { get; } = new();
```

## 修复后的方案

### 1. ? 新增服务接口

#### IDialogService
- 负责所有对话框操作
- 符合MVVM原则，ViewModel不直接操作View

#### IApplicationService
- 负责应用程序生命周期管理
- 将系统级操作从ViewModel中分离

#### IViewModelFactory
- 负责创建ViewModel实例
- 确保依赖正确注入

### 2. ? 服务实现

#### DialogService
```csharp
public async Task<bool> ShowSettingsDialogAsync()
{
    var settingsViewModel = new SettingsWindowViewModel(_settingsService);
    var settingsWindow = new SettingsWindow { DataContext = settingsViewModel };
    // 安全的对话框显示逻辑
}
```

#### ApplicationService
```csharp
public void Shutdown()
{
    if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        desktop.Shutdown();
}
```

#### ViewModelFactory
```csharp
public PianoRollViewModel CreatePianoRollViewModel()
{
    return new PianoRollViewModel(_coordinateService);
}
```

### 3. ? 修复后的MainWindowViewModel

#### 依赖注入构造函数
```csharp
public MainWindowViewModel(
    ISettingsService settingsService,
    IDialogService dialogService,
    IApplicationService applicationService,
    IViewModelFactory viewModelFactory)
{
    // 通过构造函数注入所有依赖
}
```

#### 符合MVVM的命令实现
```csharp
[RelayCommand]
private async Task OpenSettingsAsync()
{
    var result = await _dialogService.ShowSettingsDialogAsync();
    // 业务逻辑委托给服务处理
}

[RelayCommand]
private async Task ExitApplicationAsync()
{
    if (await _applicationService.CanShutdownSafelyAsync())
        _applicationService.Shutdown();
    // 应用程序管理委托给专门的服务
}
```

### 4. ? App.axaml.cs中的依赖注入容器

```csharp
private async Task InitializeServicesAsync()
{
    // 按依赖顺序初始化服务
    _settingsService = new SettingsService();
    _coordinateService = new CoordinateService();
    _applicationService = new ApplicationService(_settingsService);
    _dialogService = new DialogService(_settingsService);
    _viewModelFactory = new ViewModelFactory(_coordinateService);
}

private MainWindowViewModel CreateMainWindowViewModel()
{
    return new MainWindowViewModel(
        _settingsService,
        _dialogService,
        _applicationService,
        _viewModelFactory);
}
```

## MVVM规范修复效果

### ? 职责分离
- **ViewModel**: 只负责UI逻辑和数据绑定
- **DialogService**: 负责对话框操作
- **ApplicationService**: 负责应用程序管理
- **ViewModelFactory**: 负责ViewModel创建

### ? 依赖解耦
- 通过接口定义依赖
- 通过构造函数注入实现
- 可测试性大幅提升

### ? 异常处理
- 每个操作都有适当的异常处理
- 用户友好的错误提示
- 完整的错误日志记录

### ? 可维护性
- 清晰的代码结构
- 单一职责原则
- 易于扩展和修改

## 后续优化建议

1. **引入成熟的DI容器**：如Microsoft.Extensions.DependencyInjection
2. **添加单元测试**：利用依赖注入的可测试性
3. **实现消息总线**：用于ViewModel间的松耦合通信
4. **配置管理**：将硬编码的字符串移到配置文件
5. **本地化支持**：多语言界面支持