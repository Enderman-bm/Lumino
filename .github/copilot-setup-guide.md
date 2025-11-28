# GitHub Copilot 配置

为了在Lumino项目中获得最佳的GitHub Copilot体验，请在项目根目录创建`.github/copilot-instructions.md`文件，并包含以下内容：

```markdown
# GitHub Copilot Instructions for Lumino

你正在协助开发Lumino项目 - 一个基于Avalonia UI和Vulkan的音频可视化编辑器。

## 项目概述
Lumino是一个多模块C#/.NET应用程序，专注于音乐可视化和MIDI文件处理，使用Avalonia UI作为前端框架，Vulkan作为渲染引擎。

## 核心模块
- **Lumino**: 主应用程序 (Avalonia UI)
- **LuminoRenderEngine**: Vulkan渲染引擎
- **LuminoWaveTable**: 音频波形处理
- **MidiReader**: MIDI文件读取和处理
- **ImageToMidi.Core**: 图像转MIDI逻辑

## 开发指南
1. 严格遵循MVVM模式，保持Views/ViewModels/Models分离
2. 在Lumino目录下编译项目，而非项目根目录
3. 使用中文编写提交信息，提交到两个远程端
4. 禁止简化功能实现，必须完全按照用户需求完成功能
5. 实现适当的异常处理和性能优化
6. 确保Vulkan资源正确管理和音频数据线程安全
7. 考虑跨平台兼容性(Windows/Linux/macOS)

## 代码风格
- 类名、方法名、常量使用PascalCase
- 私有字段使用_camelCase
- 视图文件使用.axaml扩展名
- 为公共API提供XML文档注释
```

## 使用方法

1. 在项目根目录创建`.github`目录(如果不存在)
2. 将上述内容保存为`.github/copilot-instructions.md`
3. 重启VS Code或GitHub Copilot扩展
4. Copilot将自动识别并应用这些项目特定指令

这样配置后，GitHub Copilot将更好地理解Lumino项目的结构和需求，提供更准确的代码建议。