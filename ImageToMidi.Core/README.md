# ImageToMidi.Core

## 项目简介

ImageToMidi.Core 是一个功能强大的图片转MIDI类库，支持多种图像处理算法和MIDI生成技术。它可以将图片转换为音乐，支持多种调色板生成方法和音频输出格式。

## 功能特性

### 核心功能
- 🎨 支持多种图片格式：PNG, JPG, JPEG, BMP, GIF, WebP, SVG, EPS, AI, PDF
- 🎵 将图像像素转换为MIDI音符
- 🎼 生成标准MIDI文件
- 📊 支持CSV输出
- 🔊 支持WAV音频合成

### 调色板生成方法
- **简单WPF**：使用WPF内置颜色量化
- **K-Means聚类**：标准K-Means颜色量化
- **K-Means++**：改进的K-Means++初始化
- **八叉树算法**：高效的八叉树颜色量化
- **流行色算法**：基于颜色频率的量化
- **中位切割**：中位切割算法
- **PCA方向**：主成分分析颜色方向
- **最大最小距离**：最大最小距离算法
- **原生K-Means**：优化的K-Means实现
- **均值漂移**：Mean Shift聚类
- **DBSCAN**：DBSCAN密度聚类
- **GMM**：高斯混合模型
- **层次聚类**：层次聚类算法
- **谱聚类**：谱聚类算法
- **LAB K-Means**：LAB颜色空间K-Means
- **OPTICS**：OPTICS聚类算法
- **固定位调色板**：固定位深度调色板

### 抖动算法
- **无抖动**：直接量化
- **Floyd-Steinberg**：Floyd-Steinberg误差扩散
- **Bayer有序抖动**：2x2, 4x4, 8x8 Bayer矩阵

### MIDI功能
- 🥁 鼓点轨道支持
- 🎹 多轨道MIDI输出
- 🔇 背景（负面）轨道
- 🎛️ 音频参数控制

## 安装

### NuGet包

```bash
dotnet add package ImageToMidi.Core
```

### 项目引用

```xml
<ProjectReference Include="..\ImageToMidi.Core\ImageToMidi.Core.csproj" />
```

## 快速开始

### 基础示例

```csharp
using ImageToMidi;
using ImageToMidi.Contracts;
using ImageToMidi.Models;
using SkiaSharp;

// 创建图像处理器
var processor = new ImageProcessor();

// 加载图片
using var bitmap = await processor.LoadImageAsync("path/to/image.png");

// 配置转换选项
var options = new ImageConvertOptions
{
    Width = 100,
    Height = 100,
    ColorCount = 16,
    PaletteMethod = PaletteMethod.KMeansPlusPlus,
    DitherMethod = DitherMethod.FloydSteinberg,
    MidiOptions = new MidiOptions
    {
        ProjectName = "MyProject",
        OutputFolder = "Output"
    }
};

// 执行转换
var converter = new ImageToMidiConverter();
var midiTracks = await converter.ConvertImageToMidiAsync(
    bitmap.GetPixelData(),
    bitmap.Width,
    bitmap.Height,
    palette,
    options
);

// 生成MIDI文件
var midiProcessor = new MidiProcessor();
var midiData = await midiProcessor.CreateMidiFileAsync(midiTracks);

// 保存文件
await File.WriteAllBytesAsync("output.mid", midiData);
```

### 控制台应用示例

```csharp
using ImageToMidi.Console;

class Program
{
    static async Task Main(string[] args)
    {
        var app = new ConsoleApplication();
        await app.RunAsync(args);
    }
}
```

### WinForms应用示例

```csharp
using ImageToMidi.WinForms;

public partial class MainForm : Form
{
    private readonly ImageProcessor processor;
    private readonly ImageToMidiConverter converter;

    public MainForm()
    {
        InitializeComponent();
        processor = new ImageProcessor();
        converter = new ImageToMidiConverter();
    }

    private async void btnConvert_Click(object sender, EventArgs e)
    {
        // 选择图片
        using var openFileDialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.svg;*.eps;*.ai;*.pdf"
        };

        if (openFileDialog.ShowDialog() != DialogResult.OK)
            return;

        // 加载并转换
        using var bitmap = await processor.LoadImageAsync(openFileDialog.FileName);
        // ... conversion logic
    }
}
```

## API文档

### 图像处理器接口（IImageProcessor）

#### 方法

##### LoadImageAsync
```csharp
Task<SKBitmap> LoadImageAsync(string filePath)
```
加载图像文件并返回SKBitmap对象。

**参数：**
- `filePath` - 图像文件路径

**返回：**
- SKBitmap图像对象

**支持的格式：**
- 位图：PNG, JPG, JPEG, BMP, GIF, WebP
- 矢量图：SVG, EPS, AI, PDF

---

##### Resize
```csharp
SKBitmap Resize(SKBitmap source, int width, int height, ResizeQuality quality = ResizeQuality.Medium)
```
调整图像大小。

**参数：**
- `source` - 源图像
- `width` - 目标宽度
- `height` - 目标高度
- `quality` - 缩放质量（Low/Medium/High）

**返回：**
- 调整大小后的图像

---

### 转换选项（ImageConvertOptions）

#### 属性

##### Width
```csharp
public int Width { get; set; } = 100
```
输出宽度（像素），默认100

---

##### Height
```csharp
public int Height { get; set; } = 100
```
输出高度（像素），默认100

---

##### GenerateCSV
```csharp
public bool GenerateCSV { get; set; } = false
```
是否生成CSV数据文件

---

##### UseGrayPalette
```csharp
public bool UseGrayPalette { get; set; } = false
```
是否使用灰度调色板

---

##### GrayBitDepth
```csharp
public int GrayBitDepth { get; set; } = 4
```
灰度位深度（2-16），默认4位

---

##### PaletteMethod
```csharp
public PaletteMethod PaletteMethod { get; set; } = PaletteMethod.KMeansPlusPlus
```
调色板生成方法

---

##### ColorCount
```csharp
public int ColorCount { get; set; } = 16
```
调色板颜色数量，默认16色

---

##### DitherMethod
```csharp
public DitherMethod DitherMethod { get; set; } = DitherMethod.FloydSteinberg
```
抖动算法

---

##### PremultiplyAlpha
```csharp
public bool PremultiplyAlpha { get; set; } = false
```
是否预乘透明度

---

##### RotationAngle
```csharp
public int RotationAngle { get; set; } = 0
```
旋转角度（0, 90, 180, 270）

---

##### FlipHorizontal
```csharp
public bool FlipHorizontal { get; set; } = false
```
是否水平翻转

---

##### ResizeQuality
```csharp
public ResizeQuality ResizeQuality { get; set; } = ResizeQuality.Medium
```
图像缩放质量

---

##### MidiOptions
```csharp
public MidiOptions MidiOptions { get; set; } = new MidiOptions()
```
MIDI输出选项

---

### MIDI选项（MidiOptions）

#### 属性

##### OutputFolder
```csharp
public string OutputFolder { get; set; } = "Output"
```
输出文件夹路径

---

##### ProjectName
```csharp
public string ProjectName { get; set; } = "ImageToMidi"
```
项目名称

---

##### DrumTrack
```csharp
public DrumTrack DrumTrack { get; set; } = new DrumTrack()
```
鼓点轨道设置

---

##### BackgroundTrack
```csharp
public BackgroundTrack BackgroundTrack { get; set; } = new BackgroundTrack()
```
背景轨道设置

---

##### Audio
```csharp
public AudioSettings Audio { get; set; } = new AudioSettings()
```
音频合成设置

---

##### Metadata
```csharp
public TrackMetadata Metadata { get; set; } = new TrackMetadata()
```
轨道元数据

---

### 调色板方法（PaletteMethod）

```csharp
public enum PaletteMethod
{
    OnlyWpf = 0,           // 简单WPF方法
    KMeansPlusPlus = 1,    // K-Means++聚类
    KMeans = 2,            // K-Means聚类
    Octree = 3,            // 八叉树量化
    Popularity = 4,        // 流行色算法
    MedianCut = 5,         // 中位切割
    Pca = 6,               // PCA方向
    MaxMin = 7,            // 最大最小距离
    NativeKMeans = 8,      // 原生K-Means
    MeanShift = 9,         // 均值漂移
    Dbscan = 10,           // DBSCAN聚类
    Gmm = 11,              // 高斯混合模型
    Hierarchical = 12,     // 层次聚类
    Spectral = 13,         // 谱聚类
    LabKMeans = 14,        // LAB K-Means
    Optics = 15,           // OPTICS聚类
    FixedBitPalette = 16   // 固定位调色板
}
```

### 抖动方法（DitherMethod）

```csharp
public enum DitherMethod
{
    None = 0,              // 无抖动
    FloydSteinberg = 1,    // Floyd-Steinberg
    BayerOrdered = 2       // Bayer有序抖动
}
```

### 缩放质量（ResizeQuality）

```csharp
public enum ResizeQuality
{
    Low = 0,      // 低质量，速度快
    Medium = 1,   // 中等质量，平衡
    High = 2      // 高质量，速度慢
}
```

## 进阶用法

### 自定义进度回调

```csharp
public class ProgressHandler : IProgressCallback
{
    public async Task ReportProgressAsync(double progress, string message)
    {
        Console.WriteLine($"[{progress:P1}] {message}");
    }

    public bool IsCancellationRequested { get; set; }
}

var progress = new ProgressHandler();
var result = await converter.ConvertImageToMidiAsync(
    imageData, width, height, palette, options,
    cancellationToken: CancellationToken.None
);
```

### 批量处理

```csharp
public async Task BatchProcessAsync(string[] imageFiles)
{
    var tasks = imageFiles.Select(async file =>
    {
        using var bitmap = await processor.LoadImageAsync(file);
        // 处理逻辑
    });

    await Task.WhenAll(tasks);
}
```

### 自定义调色板

```csharp
var customPalette = new PaletteColor[]
{
    new PaletteColor(255, 0, 0, 0xFFFF0000),   // 红色
    new PaletteColor(0, 255, 0, 0xFF00FF00),   // 绿色
    new PaletteColor(0, 0, 255, 0xFF0000FF),   // 蓝色
    // ... 更多颜色
};

var options = new ImageConvertOptions
{
    PaletteMethod = PaletteMethod.FixedBitPalette
};
```

## 性能优化

### 大批量处理

```csharp
// 使用并行处理
var parallelOptions = new ParallelOptions
{
    MaxDegreeOfParallelism = Environment.ProcessorCount
};

await Parallel.ForEachAsync(imageFiles, parallelOptions, async (file, ct) =>
{
    // 处理单个文件
});
```

### 内存管理

```csharp
// 及时释放大对象
using (var bitmap = await processor.LoadImageAsync(file))
{
    // 使用bitmap
} // 自动释放

// 手动触发垃圾回收
GC.Collect();
GC.WaitForPendingFinalizers();
```

## 故障排除

### 常见问题

#### 1. 无法加载图像
**问题：** `FileNotFoundException`或`UnsupportedFormatException`

**解决方案：**
- 检查文件路径是否正确
- 确认文件格式在支持列表中
- 检查文件是否损坏

#### 2. MIDI文件无法播放
**问题：** 生成的MIDI文件在某些播放器中无法播放

**解决方案：**
- 尝试不同的MIDI播放器
- 检查音符范围（0-127）
- 验证时间签名和速度设置

#### 3. 内存不足
**问题：** 处理大图像时抛出`OutOfMemoryException`

**解决方案：**
- 减小输出尺寸（Width/Height）
- 增加虚拟内存
- 使用64位应用程序

#### 4. 转换速度慢
**问题：** 大批量处理速度慢

**解决方案：**
- 降低ResizeQuality
- 使用并行处理
- 选择更快的调色板方法（如SimpleWpf）

## 许可协议

MIT License

Copyright (c) 2025 ImageToMidi

## 贡献

欢迎提交Issue和Pull Request！

## 联系方式

- GitHub: https://github.com/yourusername/ImageToMidi
- Email: your.email@example.com

## 更新日志

### v1.4.6
- ✨ 转换为SDK风格项目
- ✨ 支持.NET 9.0
- ✨ 创建独立类库
- ✨ 添加控制台示例应用
- ✨ 添加WinForms示例应用
- 📚 创建完整API文档
- ⚡ 性能优化
- 🐛 修复内存泄漏问题
- 🔧 改进代码结构

### v1.4.5
- ✨ 新增OPTICS聚类算法
- ✨ 新增LabKMeans算法
- ✨ 支持Floyd-Steinberg抖动
- ✨ 支持Bayer有序抖动
- ⚡ 优化KMeans++性能
- 🐛 修复SVG加载问题
- 🐛 修复GIF动画帧处理

### v1.4.4
- ✨ 支持EPS/AI/PDF矢量图
- ✨ 新增15种调色板方法
- ⚡ 改进内存管理
- 📚 添加多语言支持
- 🔧 重构代码结构

## 致谢

- [SkiaSharp](https://github.com/mono/SkiaSharp) - 2D图形库
- [Magick.NET](https://github.com/dlemstra/Magick.NET) - 图像处理库
- [MIDIModificationFramework](https://github.com/arduano/MIDIModificationFramework) - MIDI处理框架

---

**Made with ❤️ and C#**
