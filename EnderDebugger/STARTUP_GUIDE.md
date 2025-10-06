# EnderDebugger 启动指南

## 问题已解决 ✅

EnderDebugger 现在可以成功单独启动了!

## 解决的问题

### 1. 缺少 Assets 资源
**问题**: LogViewerWindow.axaml 引用了 `/Assets/avalonia-logo.ico`,但项目中没有这个文件
**解决**: 
- 创建了 `Assets` 目录
- 从 Lumino 项目复制了 `avalonia-logo.ico`
- 在 `EnderDebugger.csproj` 中添加了 `<AvaloniaResource Include="Assets\**" />`

### 2. Program.cs 阻塞问题
**问题**: `Console.ReadKey()` 导致程序无法正常运行
**解决**: 移除了 `Console.ReadKey()` 和不必要的控制台输出

### 3. 启动流程优化
**问题**: 复杂的错误处理和调试输出
**解决**: 简化了 Program.cs 的 Main 方法,保留必要的异常处理

## 启动方法

### 方法 1: 使用命令行
```powershell
# 切换到项目根目录
cd D:\source\Lumino

# 运行 EnderDebugger
cd EnderDebugger
dotnet run
```

### 方法 2: 使用批处理脚本
双击 `StartEnderDebugger.bat` 文件

### 方法 3: 直接运行可执行文件
```powershell
cd D:\source\Lumino\EnderDebugger\bin\Debug\net9.0
.\EnderDebugger.exe
```

## 项目配置

### EnderDebugger.csproj
```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>  <!-- 窗口应用程序,无控制台 -->
  <TargetFramework>net9.0</TargetFramework>
  <Nullable>enable</Nullable>
  <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
  <ApplicationManifest>app.manifest</ApplicationManifest>
</PropertyGroup>

<ItemGroup>
  <AvaloniaResource Include="Assets\**" />  <!-- 包含 Assets 资源 -->
</ItemGroup>
```

### 关键文件
- ✅ `Assets/avalonia-logo.ico` - 窗口图标
- ✅ `App.axaml` - 应用程序定义
- ✅ `Program.cs` - 入口点(已优化)
- ✅ `Views/LogViewerWindow.axaml` - 主窗口UI
- ✅ `ViewModels/LogViewerViewModel.cs` - 视图模型

## 验证启动成功

启动后应该看到:
1. ✅ EnderDebugger 窗口打开
2. ✅ 显示测试日志(Debug、Info、Warn、Error)
3. ✅ 工具栏功能正常(清空、保存、过滤、搜索)
4. ✅ 状态栏显示日志统计
5. ✅ 无控制台窗口(WinExe模式)

## 常见问题

### Q: 运行后立即退出
A: 检查是否有异常,可以临时将 OutputType 改为 Exe 查看控制台输出

### Q: 找不到图标资源
A: 确保 Assets 目录存在并且包含 avalonia-logo.ico

### Q: 窗口不显示
A: 检查 App.axaml.cs 中 MainWindow 设置是否正确

## 测试日志输出

启动后会自动显示以下测试日志:
- 🔍 **Debug**: "应用程序启动", "事件订阅"  
- ℹ️ **Info**: "UI加载完成", "日志查看器初始化", "系统就绪"
- ⚠️ **Warn**: "测试警告日志"
- ❌ **Error**: "测试错误日志"

## 下一步

现在 EnderDebugger 可以:
- ✅ 独立启动运行
- ✅ 显示美观的日志UI
- ✅ 实时接收日志更新
- ✅ 过滤和搜索日志
- ✅ 保存日志到文件

可以开始集成到 Lumino 主应用程序进行测试!
