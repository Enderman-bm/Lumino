# Lumino 单文件发布指南

## 概述

Lumino 项目已启用 **单文件发布** (Single-File Deployment) 功能，可以将应用程序及其所有依赖项打包为单个可执行文件。

## 配置信息

### 启用的设置

在 `Lumino.csproj` 中启用了以下属性：

```xml
<PropertyGroup>
    <!-- 启用单文件发布 -->
    <PublishSingleFile>true</PublishSingleFile>
    <!-- 自包含模式（包含运行时） -->
    <SelfContained>true</SelfContained>
</PropertyGroup>
```

## 发布方法

### 方法 1: 使用发布脚本（推荐）

项目提供了 PowerShell 脚本 `publish-single-file.ps1` 来简化发布过程。

#### 快速发布（默认 x64）
```powershell
.\publish-single-file.ps1
```

#### 指定平台发布

**Windows x64（64位）**
```powershell
.\publish-single-file.ps1 -RuntimeIdentifier win-x64
```

**Windows x86（32位）**
```powershell
.\publish-single-file.ps1 -RuntimeIdentifier win-x86
```

**Windows ARM64**
```powershell
.\publish-single-file.ps1 -RuntimeIdentifier win-arm64
```

**指定输出路径**
```powershell
.\publish-single-file.ps1 -RuntimeIdentifier win-x64 -OutputPath "C:\release\v1.0"
```

### 方法 2: 使用命令行

如果不使用脚本，可以直接运行 `dotnet publish` 命令：

#### Windows x64
```bash
dotnet publish Lumino/Lumino.csproj -c Release -r win-x64 -o publish --self-contained -p:PublishSingleFile=true
```

#### Windows x86
```bash
dotnet publish Lumino/Lumino.csproj -c Release -r win-x86 -o publish --self-contained -p:PublishSingleFile=true
```

#### Windows ARM64
```bash
dotnet publish Lumino/Lumino.csproj -c Release -r win-arm64 -o publish --self-contained -p:PublishSingleFile=true
```

## 发布参数说明

| 参数 | 说明 | 值 |
|------|------|-----|
| `-c Release` | 编译配置 | Debug / Release |
| `-r <RID>` | 运行时标识符 | win-x64, win-x86, win-arm64 |
| `-o <path>` | 输出目录 | 任何有效路径 |
| `--self-contained` | 自包含模式 | 包含 .NET 运行时 |
| `-p:PublishSingleFile=true` | 单文件发布 | true / false |
| `-p:PublishTrimmed=false` | 发布修剪 | false（保留完整文件） |

## 输出文件

发布成功后，输出目录中会生成：

```
publish/
├── Lumino.exe              ← 单个可执行文件（包含所有依赖）
├── appsettings.json        ← 应用配置文件
└── assets/                 ← 应用资源文件夹
```

## 文件大小预期

- **单文件 (win-x64)**：约 150-200 MB
  - 包含 .NET 9.0 运行时
  - 包含所有依赖库和资源

- **单文件 (win-x86)**：约 120-150 MB
- **单文件 (win-arm64)**：约 150-180 MB

> 文件大小取决于依赖项和资源文件的大小

## 性能提示

### 启动时间

单文件发布的应用程序启动时间可能会比多文件发布略长（通常增加 1-2 秒），因为需要：
1. 提取内部文件到临时目录
2. 初始化应用程序

### 大小优化

如果需要减小文件大小，可以启用修剪 (trimming)：

```powershell
.\publish-single-file.ps1 -RuntimeIdentifier win-x64 -Configuration Release
# 然后编辑 PowerShell 脚本，将 -p:PublishTrimmed=false 改为 -p:PublishTrimmed=true
```

**注意**：启用修剪可能会破坏反射依赖，建议仅在确认应用正常工作后使用。

## 运行发布的应用

### 命令行运行
```bash
.\publish\Lumino.exe
```

### 带参数运行
```bash
.\publish\Lumino.exe --debug info
```

### 创建快捷方式

1. 右键点击 `Lumino.exe`
2. 选择"发送到" → "桌面（创建快捷方式）"
3. 根据需要编辑快捷方式属性

## 常见问题

### Q: 单文件应用可以在其他计算机上运行吗？

**A**: 是的，但需要满足以下条件：
- 目标计算机运行相同或更新版本的 Windows
- 对于 x64 发布，目标计算机需要 x64 架构
- 对于 x86 发布，可以在 x64 机器上运行

### Q: 单文件应用可以卸载吗？

**A**: 可以。单文件应用不需要安装程序，只需删除 `.exe` 文件即可卸载。

### Q: 如何更新应用程序？

**A**: 重新发布新版本，替换旧的 `.exe` 文件即可。

### Q: 单文件应用是否支持运行时更新？

**A**: 不支持。单文件发布包含特定版本的 .NET 运行时。如需更新运行时，需要重新发布应用。

### Q: 发布时出现"找不到项目"错误？

**A**: 确保：
1. 当前目录是项目根目录（包含 `Lumino.sln` 的目录）
2. 脚本有执行权限
3. 使用正确的相对路径

## 脚本执行策略

如果遇到 PowerShell 执行策略错误，运行以下命令：

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## 后续步骤

### 创建发布版本

```powershell
# 发布 x64 版本
.\publish-single-file.ps1 -RuntimeIdentifier win-x64

# 发布 x86 版本
.\publish-single-file.ps1 -RuntimeIdentifier win-x86

# 发布 ARM64 版本
.\publish-single-file.ps1 -RuntimeIdentifier win-arm64
```

### 上传到 GitHub Release

1. 在 GitHub 上创建新的 Release
2. 上传 `publish\Lumino.exe` 文件
3. 添加发布说明和更新日志

## 相关资源

- [.NET 单文件发布文档](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file)
- [运行时标识符 (RID)](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
- [发布应用配置](https://learn.microsoft.com/en-us/dotnet/core/publishing/publish-net-app)

## 配置文件

| 文件 | 说明 |
|------|------|
| `Lumino.csproj` | 项目配置文件（包含单文件发布设置） |
| `publish-single-file.ps1` | PowerShell 发布脚本 |
| `SINGLE_FILE_PUBLISH_GUIDE.md` | 本文档 |

---

**更新日期**: 2025-11-02  
**支持版本**: .NET 9.0 及以上  
**发布模式**: 单文件 + 自包含
