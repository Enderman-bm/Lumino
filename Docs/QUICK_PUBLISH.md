# Lumino 单文件发布 - 快速开始

## 启用情况

✅ **已启用单文件发布**

Lumino 项目已配置为支持单文件发布。

## 快速发布

### 1. 最快方式（仅需一行命令）

```powershell
dotnet publish Lumino/Lumino.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

输出目录: `Lumino\bin\Release\net9.0\win-x64\publish\`

### 2. 使用发布脚本

```powershell
# 进入项目根目录
cd d:\source\Lumino

# 默认发布为 x64
.\publish-single-file.ps1

# 发布为 x86
.\publish-single-file.ps1 -RuntimeIdentifier win-x86

# 发布为 ARM64
.\publish-single-file.ps1 -RuntimeIdentifier win-arm64
```

## 发布平台选项

| 平台 | 运行时标识符 | 命令 |
|------|-----------|------|
| **Windows x64** | win-x64 | `.\publish-single-file.ps1 -RuntimeIdentifier win-x64` |
| **Windows x86** | win-x86 | `.\publish-single-file.ps1 -RuntimeIdentifier win-x86` |
| **Windows ARM64** | win-arm64 | `.\publish-single-file.ps1 -RuntimeIdentifier win-arm64` |

## 发布后

发布完成后，在输出目录中会看到：

```
publish/
├── Lumino.exe          ← 单个可执行文件
├── appsettings.json    ← 配置文件
└── assets/             ← 资源文件夹
```

### 运行应用

```powershell
# 进入输出目录
cd publish

# 运行应用
.\Lumino.exe

# 带参数运行
.\Lumino.exe --debug info
```

## 文件大小

- **x64 版本**: 约 150-200 MB
- **x86 版本**: 约 120-150 MB
- **ARM64 版本**: 约 150-180 MB

## 相关配置

### .csproj 文件设置

```xml
<PropertyGroup>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
</PropertyGroup>
```

- `PublishSingleFile=true` : 启用单文件发布
- `SelfContained=true` : 包含完整的 .NET 运行时

### 详细指南

参考: `SINGLE_FILE_PUBLISH_GUIDE.md`

## 常见命令

```powershell
# 仅编译（不发布）
dotnet build Lumino/Lumino.csproj -c Release

# 发布到自定义路径
.\publish-single-file.ps1 -RuntimeIdentifier win-x64 -OutputPath "D:\releases\v1.0"

# 清理发布文件
Remove-Item -Recurse -Force publish

# 检查 exe 文件大小
(Get-Item publish\Lumino.exe).Length / 1MB | ForEach-Object { "$_ MB" }
```

## 发布检查清单

- [ ] 代码已提交到 git
- [ ] 项目编译成功 (`dotnet build`)
- [ ] 所有测试通过
- [ ] 版本号已更新
- [ ] 发布脚本有执行权限
- [ ] 执行发布命令
- [ ] 验证 exe 文件可运行
- [ ] 上传到发布渠道

---

**更新日期**: 2025-11-02  
**版本**: Lumino v1.0+  
**.NET 版本**: 9.0+
