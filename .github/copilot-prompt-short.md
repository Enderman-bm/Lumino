# Lumino项目 Copilot 简易提示词

你正在协助开发Lumino项目 - 一个基于Avalonia UI和Vulkan的音频可视化编辑器。

## 核心技术栈
- C#/.NET + Avalonia UI (桌面应用)
- Vulkan (高性能渲染引擎)
- 自定义音频分析库

## 项目结构
- Lumino/: 主应用程序 (Avalonia UI)
- LuminoRenderEngine/: Vulkan渲染引擎
- LuminoWaveTable/: 音频波形处理
- MidiReader/: MIDI文件处理
- ImageToMidi.Core/: 图像转MIDI逻辑

## 关键规则
1. 严格遵循MVVM模式 (Views/ViewModels/Models分离)
2. 在Lumino目录下编译，而非项目根目录
3. 使用中文提交信息，提交到两个远程端
4. 禁止简化功能实现，必须完全按需求完成

## 代码风格
- PascalCase用于类名、方法名、常量
- _camelCase用于私有字段
- Views使用.axaml扩展名
- 实现适当的异常处理和性能优化

## 特殊注意
- 正确管理Vulkan资源生命周期
- 确保音频数据线程安全
- 考虑跨平台兼容性(Windows/Linux/macOS)

请根据这些指南为Lumino项目提供高质量的代码建议。