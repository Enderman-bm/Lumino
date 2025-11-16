namespace ImageToMidi.Config
{
    /// <summary>
    /// 图片转MIDI的配置类
    /// </summary>
    public class ConversionConfig
    {
        /// <summary>
        /// 是否使用动态音高算法
        /// </summary>
        public bool UseDynamicPitch { get; set; } = true;

        /// <summary>
        /// 最小音符持续时间（毫秒）
        /// </summary>
        public int MinNoteDuration { get; set; } = 50;

        /// <summary>
        /// 最大音符持续时间（毫秒）
        /// </summary>
        public int MaxNoteDuration { get; set; } = 500;

        /// <summary>
        /// 基础音高（MIDI音符编号）
        /// </summary>
        public int BasePitch { get; set; } = 60; // Middle C

        /// <summary>
        /// 音高范围（半音数量）
        /// </summary>
        public int PitchRange { get; set; } = 24;

        /// <summary>
        /// 是否启用量化
        /// </summary>
        public bool EnableQuantization { get; set; } = true;

        /// <summary>
        /// 量化精度（拍子数）
        /// </summary>
        public int QuantizationPrecision { get; set; } = 16; // 1/16音符

        /// <summary>
        /// 是否启用速度映射
        /// </summary>
        public bool EnableVelocityMapping { get; set; } = true;

        /// <summary>
        /// 最小速度值（0-127）
        /// </summary>
        public int MinVelocity { get; set; } = 40;

        /// <summary>
        /// 最大速度值（0-127）
        /// </summary>
        public int MaxVelocity { get; set; } = 100;

        /// <summary>
        /// 是否启用和弦检测
        /// </summary>
        public bool EnableChordDetection { get; set; } = false;

        /// <summary>
        /// 和弦检测阈值
        /// </summary>
        public double ChordDetectionThreshold { get; set; } = 0.8;

        /// <summary>
        /// 输出MIDI通道（0-15）
        /// </summary>
        public int OutputChannel { get; set; } = 0;

        /// <summary>
        /// 是否生成多个音轨
        /// </summary>
        public bool GenerateMultipleTracks { get; set; } = false;

        /// <summary>
        /// 颜色到音符的映射模式
        /// 0: 灰度映射，1: RGB红色通道，2: RGB绿色通道，3: RGB蓝色通道
        /// </summary>
        public int ColorMappingMode { get; set; } = 0;

        /// <summary>
        /// 是否反转音高映射（暗部高音，亮部低音）
        /// </summary>
        public bool InvertPitchMapping { get; set; } = false;

        /// <summary>
        /// 是否反转速度映射（暗部高速度，亮部低速度）
        /// </summary>
        public bool InvertVelocityMapping { get; set; } = false;

        /// <summary>
        /// 像素采样步长（降低分辨率以提高性能）
        /// </summary>
        public int PixelSampleStep { get; set; } = 1;

        /// <summary>
        /// 是否启用边缘检测（轮廓优先）
        /// </summary>
        public bool EnableEdgeDetection { get; set; } = false;

        /// <summary>
        /// 边缘检测敏感度
        /// </summary>
        public double EdgeDetectionSensitivity { get; set; } = 0.5;

        public ConversionConfig()
        {
        }
    }
}
