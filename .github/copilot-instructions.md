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

## 特殊功能实现
- 音频可视化使用Vulkan渲染引擎进行高性能图形渲染
- MIDI处理使用MidiReader库读取和解析MIDI文件
- 实现高效的批处理渲染和适当的缓冲区管理策略
- 注意渲染性能，避免不必要的重绘
- 优化音频处理算法并实现适当的缓存策略

## 常见任务模式
1. 添加新功能: 先定义Models，再实现ViewModels，最后创建Views
2. 修复Bug: 定位问题代码，实现最小化修复，添加适当测试
3. 性能优化: 识别性能瓶颈，实现优化方案，测试优化效果

请根据这些指南为Lumino项目提供高质量的代码建议。