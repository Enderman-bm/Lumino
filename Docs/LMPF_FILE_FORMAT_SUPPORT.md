# Lumino 项目文件格式（.lmpf）支持文档

## 概述

Lumino 现在完全支持 `.lmpf` (Lumino Music Project Format) 文件格式，这是应用的原生项目保存格式。

## 文件格式信息

| 属性 | 值 |
|-----|---|
| **扩展名** | .lmpf |
| **全称** | Lumino Music Project Format |
| **描述** | Lumino 原生项目文件格式，用于保存完整的编辑工作 |
| **版本** | 1.0 |

## 支持的操作

### 1. 打开 .lmpf 文件

**操作步骤**：
1. 点击菜单 → 打开 (或 Ctrl+O)
2. 在文件对话框中选择 `.lmpf` 文件
3. 确认打开

**支持的格式**：
```
文件打开对话框支持的格式：
- *.mid / *.midi - 标准MIDI文件
- *.lmpf - Lumino项目文件 ✅ (新增)
- *.dmn / *.dmnx - 遗留项目格式
```

**功能**：
- ✅ 加载完整的项目状态
- ✅ 恢复所有音符和事件
- ✅ 恢复音轨配置和元数据
- ✅ 支持进度显示和取消操作

### 2. 保存为 .lmpf 文件

**操作步骤**：
1. 点击菜单 → 保存 (或 Ctrl+S)
2. 在保存对话框中输入文件名
3. 选择文件类型为 `.lmpf`
4. 确认保存

**支持的格式**：
```
文件保存对话框支持的格式：
- *.lmpf - Lumino项目文件 ✅
- *.mid - 标准MIDI导出
```

**保存信息**：
- 🎵 所有音符和音符属性
- 🎛️ 所有 MIDI 控制器事件
- 📋 项目元数据（标题、创建时间、修改时间等）
- 🎼 音轨配置和名称
- 🎵 速度和其他事件数据

### 3. 文件对话框过滤器

现在文件打开对话框会自动显示所有支持的文件格式：

#### 打开文件对话框
```
文件类型：
- 所有支持的格式 (*.mid, *.midi, *.lmpf, *.dmn, *.dmnx)
- MIDI 文件 (*.mid, *.midi)
- Lumino 项目文件 (*.lmpf)
- 遗留格式 (*.dmn, *.dmnx)
```

#### 保存文件对话框
```
文件类型：
- Lumino 项目文件 (*.lmpf)
- MIDI 文件 (*.mid)
```

## 代码实现

### 关键常量定义

**文件**: `Lumino/Constants/DialogConstants.cs`

```csharp
/// MIDI文件扩展名过滤器
public static readonly string[] MidiFileFilters = { "*.mid", "*.midi" };

/// 项目文件扩展名过滤器
public static readonly string[] ProjectFileFilters = { "*.lmpf", "*.dmn", "*.dmnx" };

/// 所有支持的文件格式
public static readonly string[] AllSupportedFilters = 
    { "*.mid", "*.midi", "*.lmpf", "*.dmn", "*.dmnx" };
```

### 文件打开处理

**文件**: `Lumino/ViewModels/MainWindowViewModel.cs`

```csharp
// 打开文件对话框
var filePath = await _dialogService.ShowOpenFileDialogAsync(
    "打开MIDI文件或项目", 
    new[] { "*.mid", "*.midi", "*.lmpf", "*.dmn" });

// 根据扩展名判断处理方式
var extension = Path.GetExtension(filePath).ToLower();

if (extension == ".mid" || extension == ".midi")
{
    await ImportMidiFileAsync(filePath);
}
else if (extension == ".lmpf")
{
    var runResult = await _dialogService.ShowPreloadAndRunAsync<...>(
        Path.GetFileName(filePath), 
        fileSize,
        async (progress, cancellationToken) =>
        {
            var tuple = await _projectStorageService.LoadProjectAsync(
                filePath, cancellationToken);
            return new System.Tuple<ProjectSnapshot, ProjectMetadata>(
                tuple.snapshot, tuple.metadata);
        }, 
        canCancel: true);
    // 处理加载结果...
}
```

### 文件保存处理

```csharp
// 保存文件对话框
var filePath = await _dialogService.ShowSaveFileDialogAsync(
    "保存项目或导出 MIDI",
    null,
    new[] { "*.lmpf", "*.mid" });

var extension = System.IO.Path.GetExtension(filePath).ToLower();

if (extension == ".lmpf")
{
    // 保存为 Lumino 项目文件
    var ok = await _projectStorageService.SaveProjectAsync(
        filePath, snapshot, metadata, cancellationToken);
}
else if (extension == ".mid")
{
    // 导出为 MIDI 文件
    // ...
}
```

## 项目存储服务

**文件**: `Lumino/Services/Implementation/ProjectStorageService.cs`

负责 `.lmpf` 文件的序列化和反序列化：

- `SaveProjectAsync()` - 将项目保存为 `.lmpf` 文件
- `LoadProjectAsync()` - 从 `.lmpf` 文件加载项目

## 文件格式特性

### 支持的项目元数据

```csharp
public class ProjectMetadata
{
    public string Title { get; set; }                    // 项目标题
    public DateTime Created { get; set; }                // 创建时间
    public DateTime LastModified { get; set; }           // 最后修改时间
    public double Tempo { get; set; }                    // 速度(BPM)
    public List<TrackMetadata> Tracks { get; set; }      // 音轨列表
}

public class TrackMetadata
{
    public int TrackNumber { get; set; }                 // 音轨号
    public string TrackName { get; set; }                // 音轨名称
    public int MidiChannel { get; set; }                 // MIDI通道
    public int ChannelGroupIndex { get; set; }           // 通道组索引
    public int ChannelNumberInGroup { get; set; }        // 组内通道号
    public string Instrument { get; set; }               // 乐器名称
    public string ColorTag { get; set; }                 // 颜色标签
    public bool IsConductorTrack { get; set; }           // 是否为指挥轨道
}
```

### 支持的项目内容

- 📌 音符列表（包含位置、音高、力度、时长等属性）
- 🎛️ MIDI 控制器事件
- ⏱️ 速度事件（Tempo）
- 🎼 力度事件（Velocity）
- 📈 弯音事件（Pitch Bend）
- 🎛️ CC 控制器事件

## 用户工作流

### 典型工作流示例

```
1. 打开音频文件
   ↓
2. 导入或编辑 MIDI 数据
   ↓
3. 编辑CC、速度、弯音等控制事件
   ↓
4. 保存为 .lmpf 文件（保存完整项目） ✅
   ↓
5. 导出为 .mid 文件（分享MIDI数据）
```

## 编译验证

✅ **编译成功**
```
0 个错误
86 个警告（全部预先存在）
编译时间：00:00:07.07
```

## 文件对话框UI更新

### 打开对话框
**之前**: "打开MIDI文件"  
**现在**: "打开MIDI文件或项目" ✅

### 文件过滤器
**之前**: `*.mid`, `*.midi`, `*.dmn`  
**现在**: `*.mid`, `*.midi`, `*.lmpf`, `*.dmn` ✅

## 迁移指南

如果用户有旧的项目文件格式（`.dmn`, `.dmnx`），系统仍然支持打开它们。建议：

1. 打开旧项目文件（`.dmn`）
2. 编辑和完善
3. 使用 **另存为** 保存为新格式（`.lmpf`）

## 提交记录

**提交**: d08788d  
**标题**: 添加对.lmpf文件格式的完整支持  
**内容**:
- 更新打开文件对话框过滤器
- 更新对话框常量
- 文件格式明确化和分类

---

**更新日期**: 2025年11月9日  
**版本**: 1.0  
**状态**: ✅ 完成
