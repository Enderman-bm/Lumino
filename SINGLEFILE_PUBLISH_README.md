# Lumino 单文件发布指南

本指南介绍如何将 Lumino 发布为单个可执行文件，以方便分发和部署。

## 发布特性

- **单文件发布**: 将所有依赖打包到一个可执行文件中
- **自包含**: 不需要安装 .NET 运行时
- **代码裁剪**: 移除未使用的代码以减小文件大小
- **ReadyToRun**: 预编译以提高启动性能
- **可选压缩**: 使用 UPX 进一步减小文件大小

## 快速开始

### 方法1: 使用批处理脚本（推荐）

```batch
# 发布为单文件（无压缩）
publish.bat

# 发布为单文件并压缩
publish.bat compress

# 指定平台发布
publish.bat win-x64
publish.bat compress win-x64
```

### 方法2: 使用 PowerShell 脚本

```powershell
# 发布为单文件（无压缩）
.\publish-singlefile.ps1

# 发布为单文件并压缩
.\publish-singlefile.ps1 -Compress

# 指定平台发布
.\publish-singlefile.ps1 -Platform win-x64
.\publish-singlefile.ps1 -Compress -Platform win-x64
```

### 方法3: 手动发布

```batch
# 发布为单文件
dotnet publish Lumino/Lumino.csproj -c Release -r win-x64 --self-contained -p:IsPublishing=true -o publish/win-x64

# 发布并压缩（需要先安装 UPX）
dotnet publish Lumino/Lumino.csproj -c Release -r win-x64 --self-contained -p:IsPublishing=true -o publish/win-x64
upx --best --lzma publish/win-x64/Lumino.exe
```

## 支持的平台

- `win-x64` (默认) - Windows x64
- `win-x86` - Windows x86
- `win-arm64` - Windows ARM64
- `linux-x64` - Linux x64
- `linux-arm64` - Linux ARM64
- `osx-x64` - macOS x64
- `osx-arm64` - macOS ARM64

## 输出文件

发布后将在 `publish/<platform>/` 目录下生成：

- `Lumino.exe` - 主可执行文件（单文件）
- 可能的其他文件（如配置文件）

## 文件大小对比

| 配置 | 大小 | 说明 |
|------|------|------|
| 标准发布 | ~50-80 MB | 包含所有依赖 |
| 单文件发布 | ~40-60 MB | 打包为单个文件 |
| 单文件+裁剪 | ~30-50 MB | 移除未使用代码 |
| 单文件+裁剪+压缩 | ~15-30 MB | 使用 UPX 压缩 |

## 系统要求

### 运行时要求
- Windows 10 版本 1607 或更高版本 (win-x64/x86)
- Windows 10 版本 1703 或更高版本 (win-arm64)

### 开发要求
- .NET 9.0 SDK
- 可选: UPX (用于压缩)

## 安装 UPX（用于压缩）

### Windows
1. 下载 UPX: https://github.com/upx/upx/releases
2. 解压到 `C:\Program Files\UPX\` 或用户目录
3. 或者脚本会自动下载最新版本

### Linux/macOS
```bash
# Ubuntu/Debian
sudo apt-get install upx

# macOS
brew install upx
```

## 故障排除

### 发布失败
- 确保已安装 .NET 9.0 SDK
- 运行 `dotnet restore` 恢复依赖
- 检查平台 RID 是否正确

### 压缩失败
- 确保 UPX 已安装并在 PATH 中
- 或者脚本会自动下载 UPX

### 运行时错误
- 确保目标平台与运行平台匹配
- 检查 Windows 版本是否满足要求

## 高级配置

可以在 `Lumino.csproj` 中修改以下属性来自定义发布：

```xml
<!-- 启用单文件发布 -->
<PublishSingleFile>true</PublishSingleFile>

<!-- 启用代码裁剪 -->
<PublishTrimmed>true</PublishTrimmed>

<!-- 启用 ReadyToRun -->
<PublishReadyToRun>true</PublishReadyToRun>

<!-- 启用压缩 -->
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

## 注意事项

- 单文件发布会增加启动时间（因为需要解压）
- ReadyToRun 会增加文件大小但提高启动性能
- 代码裁剪可能移除某些动态加载的代码，请谨慎使用
- 压缩会进一步减小大小但需要 UPX 工具