using System;
using System.Threading.Tasks;
using SkiaSharp;

namespace ImageToMidi.Contracts
{
    /// <summary>
    /// 图像处理器接口，定义图像加载和处理的基本操作
    /// </summary>
    public interface IImageProcessor
    {

        /// <summary>
        /// 加载图像文件
        /// </summary>
        /// <param name="filePath">图像文件路径</param>
        /// <returns>SKBitmap图像对象</returns>
        Task<SKBitmap> LoadImageAsync(string filePath);

        /// <summary>
        /// 调整图像大小
        /// </summary>
        /// <param name="source">源图像</param>
        /// <param name="width">目标宽度</param>
        /// <param name="height">目标高度</param>
        /// <param name="quality">缩放质量</param>
        /// <returns>调整大小后的图像</returns>
        SKBitmap Resize(SKBitmap source, int width, int height, ResizeQuality quality = ResizeQuality.Medium);

        /// <summary>
        /// 释放资源
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// 缩放质量选项
    /// </summary>
    public enum ResizeQuality
    {
        /// <summary>低质量，速度快</summary>
        Low = 0,
        /// <summary>中等质量，平衡性能和质量</summary>
        Medium = 1,
        /// <summary>高质量，速度慢</summary>
        High = 2
    }

    /// <summary>
    /// 图像转换选项
    /// </summary>
    public class ImageConvertOptions
    {
        /// <summary>输出宽度</summary>
        public int Width { get; set; } = 100;

        /// <summary>输出高度</summary>
        public int Height { get; set; } = 100;

        /// <summary>是否生成CSV数据</summary>
        public bool GenerateCSV { get; set; } = false;

        /// <summary>是否使用灰度调色板</summary>
        public bool UseGrayPalette { get; set; } = false;

        /// <summary>灰度位深度 (2-16)</summary>
        public int GrayBitDepth { get; set; } = 4;

        /// <summary>调色板生成方法</summary>
        public PaletteMethod PaletteMethod { get; set; } = PaletteMethod.KMeansPlusPlus;

        /// <summary>调色板颜色数量</summary>
        public int ColorCount { get; set; } = 16;

        /// <summary>抖动方法</summary>
        public DitherMethod DitherMethod { get; set; } = DitherMethod.FloydSteinberg;

        /// <summary>是否预乘透明度</summary>
        public bool PremultiplyAlpha { get; set; } = false;

        /// <summary>旋转角度 (0, 90, 180, 270)</summary>
        public int RotationAngle { get; set; } = 0;

        /// <summary>是否水平翻转</summary>
        public bool FlipHorizontal { get; set; } = false;

        /// <summary>缩放质量</summary>
        public ResizeQuality ResizeQuality { get; set; } = ResizeQuality.Medium;

        /// <summary>MIDI设置</summary>
        public MidiOptions MidiOptions { get; set; } = new MidiOptions();
    }

    /// <summary>
    /// 调色板生成方法
    /// </summary>
    public enum PaletteMethod
    {
        /// <summary>简单WPF方法</summary>
        SimpleWpf = 0,
        /// <summary>K-Means++聚类</summary>
        KMeansPlusPlus = 1,
        /// <summary>K-Means聚类</summary>
        KMeans = 2,
        /// <summary>八叉树量化</summary>
        Octree = 3,
        /// <summary>流行色算法</summary>
        Popularity = 4,
        /// <summary>中位切割</summary>
        MedianCut = 5,
        /// <summary>PCA方向</summary>
        Pca = 6,
        /// <summary>最大最小距离</summary>
        MaxMin = 7,
        /// <summary>原生K-Means</summary>
        NativeKMeans = 8,
        /// <summary>均值漂移</summary>
        MeanShift = 9,
        /// <summary>DBSCAN聚类</summary>
        Dbscan = 10,
        /// <summary>高斯混合模型</summary>
        Gmm = 11,
        /// <summary>层次聚类</summary>
        Hierarchical = 12,
        /// <summary>谱聚类</summary>
        Spectral = 13,
        /// <summary>LAB空间K-Means</summary>
        LabKMeans = 14,
        /// <summary>OPTICS聚类</summary>
        Optics = 15,
        /// <summary>固定位调色板</summary>
        FixedBitPalette = 16
    }

    /// <summary>
    /// 抖动方法
    /// </summary>
    public enum DitherMethod
    {
        /// <summary>无抖动</summary>
        None = 0,
        /// <summary>Floyd-Steinberg抖动</summary>
        FloydSteinberg = 1,
        /// <summary>Bayer有序抖动</summary>
        BayerOrdered = 2
    }

    /// <summary>
    /// MIDI输出选项
    /// </summary>
    public class MidiOptions
    {
        /// <summary>输出文件夹</summary>
        public string OutputFolder { get; set; } = "Output";

        /// <summary>项目名称</summary>
        public string ProjectName { get; set; } = "ImageToMidi";

        /// <summary>鼓点轨道</summary>
        public DrumTrack DrumTrack { get; set; } = new DrumTrack();

        /// <summary>负面轨道</summary>
        public BackgroundTrack BackgroundTrack { get; set; } = new BackgroundTrack();

        /// <summary>音频合成选项</summary>
        public AudioSettings Audio { get; set; } = new AudioSettings();

        /// <summary>元数据</summary>
        public TrackMetadata Metadata { get; set; } = new TrackMetadata();
    }

    /// <summary>
    /// 鼓点轨道设置
    /// </summary>
    public class DrumTrack
    {
        /// <summary>是否启用鼓点</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>鼓点类型</summary>
        public int DrumType { get; set; } = 2;

        /// <summary>鼓点音符力度</summary>
        public int Velocity { get; set; } = 120;

        /// <summary>鼓点间隔</summary>
        public int Interval { get; set; } = 24;
    }

    /// <summary>
    /// 背景轨道设置
    /// </summary>
    public class BackgroundTrack
    {
        /// <summary>是否启用背景音符</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>正面音符轨道</summary>
        public int PositiveTrack { get; set; } = 1;

        /// <summary>负面音符轨道</summary>
        public int NegativeTrack { get; set; } = 2;

        /// <summary>正面音符力度</summary>
        public int PositiveVelocity { get; set; } = 127;

        /// <summary>负面音符力度</summary>
        public int NegativeVelocity { get; set; } = 64;

        /// <summary>负面音符数量</summary>
        public int NegativeNoteCount { get; set; } = 200;

        /// <summary>负面音符长度</summary>
        public int NegativeNoteLength { get; set; } = 32;
    }

    /// <summary>
    /// 音频设置
    /// </summary>
    public class AudioSettings
    {
        /// <summary>创建WAV音频文件</summary>
        public bool CreateWav { get; set; } = false;

        /// <summary>SoundFont文件路径</summary>
        public string? SoundFontPath { get; set; } = null;

        /// <summary>采样率</summary>
        public int SampleRate { get; set; } = 44100;

        /// <summary>音频质量 (0-100)</summary>
        public int Quality { get; set; } = 80;
    }

    /// <summary>
    /// 轨道元数据
    /// </summary>
    public class TrackMetadata
    {
        /// <summary>作者</summary>
        public string Author { get; set; } = "ImageToMidi";

        /// <summary>描述</summary>
        public string Description { get; set; } = "Converted from image";

        /// <summary>版权信息</summary>
        public string Copyright { get; set; } = string.Empty;
    }

    /// <summary>
    /// 转换进度回调
    /// </summary>
    public interface IProgressCallback
    {
        /// <summary>更新进度</summary>
        Task ReportProgressAsync(double progress, string message);

        /// <summary>检查是否取消</summary>
        bool IsCancellationRequested { get; }
    }
}
