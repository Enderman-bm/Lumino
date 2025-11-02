# Lumino 单文件发布配置总结

## ✅ 配置完成

已成功为 Lumino 项目启用单文件发布功能。

## 配置内容

### 1. 项目文件修改 (Lumino.csproj)

```xml
<!-- 启用单文件发布 -->
<PublishSingleFile Condition="'$(IsPublishing)' == 'true'">true</PublishSingleFile>

<!-- 自包含模式（仅在发布时启用） -->
<SelfContained Condition="'$(IsPublishing)' == 'true'">true</SelfContained>
<SelfContained Condition="'$(IsPublishing)' != 'true'">false</SelfContained>
```

**关键点**:
- `PublishSingleFile=true`: 启用单文件发布
- `SelfContained=true`: 包含完整的 .NET 运行时
- 使用条件式配置：仅在发布时启用这些选项，避免在开发时引起问题

### 2. 发布脚本 (publish-single-file.ps1)

提供了 PowerShell 脚本简化发布流程：

```powershell
# 默认发布为 x64
.\publish-single-file.ps1

# 指定平台
.\publish-single-file.ps1 -RuntimeIdentifier win-x86
.\publish-single-file.ps1 -RuntimeIdentifier win-arm64
```

### 3. 文档

创建了完整的文档：
- `SINGLE_FILE_PUBLISH_GUIDE.md` - 详细发布指南
- `QUICK_PUBLISH.md` - 快速开始指南

## 支持的发布平台

| 平台 | 运行时标识符 | 用途 |
|------|-----------|------|
| Windows x64 | win-x64 | 64 位 Windows（推荐，默认） |
| Windows x86 | win-x86 | 32 位 Windows |
| Windows ARM64 | win-arm64 | ARM64 Windows |

## 发布命令示例

### 快速发布（推荐）

```powershell
# 使用脚本（最简单）
.\publish-single-file.ps1 -RuntimeIdentifier win-x64
```

### 命令行发布

```bash
# x64
dotnet publish Lumino/Lumino.csproj -c Release -r win-x64 -o publish --self-contained -p:PublishSingleFile=true

# x86
dotnet publish Lumino/Lumino.csproj -c Release -r win-x86 -o publish --self-contained -p:PublishSingleFile=true

# ARM64
dotnet publish Lumino/Lumino.csproj -c Release -r win-arm64 -o publish --self-contained -p:PublishSingleFile=true
```

## 输出结构

发布后将生成以下文件：

```
publish/
├── Lumino.exe              ← 单个可执行文件（包含所有依赖和运行时）
├── appsettings.json        ← 应用配置
└── assets/                 ← 应用资源
```

## 文件大小预期

- **win-x64**: 约 150-200 MB
- **win-x86**: 约 120-150 MB
- **win-arm64**: 约 150-180 MB

> 实际大小取决于依赖项和资源

## 配置原理

### 为什么使用条件式配置？

项目中有多个子项目引用（MidiReader、EnderAudioAnalyzer 等），这些项目不支持 self-contained 模式。

使用条件式配置解决了这个问题：

1. **开发时** (`IsPublishing != 'true'`):
   - `PublishSingleFile = false`
   - `SelfContained = false`
   - 子项目可以正常编译

2. **发布时** (`IsPublishing == 'true'`):
   - `PublishSingleFile = true`
   - `SelfContained = true`
   - 生成单个独立的可执行文件

### 发布流程

```
↓ dotnet publish 命令
↓
IsPublishing 属性设置为 true
↓
项目文件中的条件式配置生效
↓
PublishSingleFile = true
SelfContained = true
↓
编译 + 链接 + 打包
↓
生成 Lumino.exe（单文件）
```

## 特点和优势

### ✅ 优势

- **易于分发**: 只需一个 `.exe` 文件
- **无需安装**: 可直接运行，无需安装程序
- **独立性**: 包含完整运行时，无需系统已安装 .NET
- **跨平台发布**: 支持 Windows x64、x86、ARM64

### ⚠️ 注意事项

- **文件体积大**: 包含完整运行时，大约 150-200 MB
- **启动时间**: 首次启动可能比分散模式慢 1-2 秒
- **更新困难**: 无法独立更新 .NET 运行时，需重新发布整个应用
- **临时文件**: 运行时会在临时目录提取文件

## 后续优化建议

### 1. 减小文件体积（可选）

启用修剪 (trimming) 可以减少文件大小：

```powershell
# 编辑 publish-single-file.ps1
# 将 -p:PublishTrimmed=false 改为 -p:PublishTrimmed=true
```

**注意**: 修剪可能会破坏反射依赖，需充分测试。

### 2. 改进启动性能（可选）

启用 ReadyToRun (R2R) 编译：

```powershell
# 在发布命令中添加
-p:PublishReadyToRun=true
```

### 3. 版本管理

在 `.csproj` 中添加版本信息：

```xml
<PropertyGroup>
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
</PropertyGroup>
```

## 故障排查

### 问题: PowerShell 执行策略限制

```powershell
# 解决方案: 允许运行本地脚本
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### 问题: 找不到项目文件

确保：
1. 在项目根目录运行脚本（包含 `Lumino.sln` 的目录）
2. 路径正确：`Lumino/Lumino.csproj`

### 问题: 发布失败

检查：
1. 是否安装了 .NET 9.0 SDK
2. 是否有足够的磁盘空间（至少 2 GB）
3. 文件权限是否正确

## 集成 CI/CD

### GitHub Actions 示例

```yaml
- name: Publish Lumino (Single File)
  run: |
    dotnet publish Lumino/Lumino.csproj `
      -c Release -r win-x64 `
      -o publish `
      --self-contained `
      -p:PublishSingleFile=true
```

### Azure DevOps Pipeline 示例

```yaml
- task: DotNetCoreCLI@2
  inputs:
    command: 'publish'
    projects: 'Lumino/Lumino.csproj'
    arguments: '-c Release -r win-x64 -o publish --self-contained -p:PublishSingleFile=true'
```

## 参考资源

- [.NET 单文件发布官方文档](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file)
- [运行时标识符 (RID) 目录](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
- [.NET 应用发布配置](https://learn.microsoft.com/en-us/dotnet/core/publishing/publish-net-app)

## 相关文件清单

| 文件 | 说明 |
|------|------|
| `Lumino/Lumino.csproj` | 项目配置（包含单文件发布设置） |
| `publish-single-file.ps1` | PowerShell 发布脚本 |
| `SINGLE_FILE_PUBLISH_GUIDE.md` | 详细发布指南 |
| `QUICK_PUBLISH.md` | 快速开始指南 |
| `SINGLE_FILE_PUBLISH_CONFIG.md` | 本文档 |

## 验证清单

- [x] 项目文件已配置单文件发布
- [x] 使用条件式配置避免编译问题
- [x] 提供 PowerShell 发布脚本
- [x] 支持 x64、x86、ARM64 平台
- [x] 创建详细文档
- [ ] 实际发布测试（待执行）

## 后续步骤

1. **执行首次发布**:
   ```powershell
   .\publish-single-file.ps1 -RuntimeIdentifier win-x64
   ```

2. **验证输出**:
   - 检查 `publish/Lumino.exe` 是否生成
   - 验证文件大小约 150-200 MB

3. **测试应用**:
   ```powershell
   .\publish\Lumino.exe
   ```

4. **发布到 GitHub Releases** (可选):
   - 创建新 Release
   - 上传 `Lumino.exe`
   - 添加发布说明

---

**配置日期**: 2025-11-02  
**.NET 版本**: 9.0+  
**状态**: ✅ 已启用  
**发布模式**: 单文件 + 自包含  
**支持平台**: Windows (x64, x86, ARM64)
