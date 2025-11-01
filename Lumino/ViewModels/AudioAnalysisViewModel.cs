using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Services.Interfaces;
using EnderAudioAnalyzer.Interfaces;
using EnderAudioAnalyzer.Services;
using Lumino.ViewModels.Editor;
using System.Linq;
using EnderAudioAnalyzer.Models;
using System.IO;
using EnderDebugger;
using System.Runtime.InteropServices;
using DImage = System.Drawing.Image;
using Bitmap = System.Drawing.Bitmap;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using Color = System.Drawing.Color;
using Drawing2D = System.Drawing.Drawing2D;

namespace Lumino.ViewModels
{
    /// <summary>
    /// 音频分析视图模型 - 负责音频文件分析和音符检测
    /// </summary>
    public partial class AudioAnalysisViewModel : ViewModelBase
    {
        #region 字段
        private readonly IDialogService _dialogService;
        private readonly IAudioAnalysisService _audioAnalysisService;
        private readonly SpectrogramService _spectrogramService;
        private readonly EnderLogger _logger;
        
        [ObservableProperty]
        private string? _audioFilePath;
        
        [ObservableProperty]
        private bool _isAnalyzing;
        
        [ObservableProperty]
        private string _progressText = "就绪";
        
        [ObservableProperty]
        private ObservableCollection<DetectedNoteViewModel> _detectedNotes = new();

        /// <summary>
        /// 关联的钢琴卷帘视图模型（用于设置频谱图）
        /// </summary>
        public PianoRollViewModel? PianoRollViewModel { get; set; }
        #endregion

        #region 构造函数
        public AudioAnalysisViewModel(
            IDialogService dialogService,
            IAudioAnalysisService? audioAnalysisService = null)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logger = EnderLogger.Instance;
            _audioAnalysisService = audioAnalysisService ?? new AudioAnalysisService();
            _spectrogramService = new SpectrogramService();
        }
        
        private void SaveSpectrogramDataForDebug(double[,] spectrogramData, int sampleRate)
        {
            try
            {
                _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 开始保存频谱图数据用于调试");
                _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 频谱图数据维度: {spectrogramData.GetLength(0)}x{spectrogramData.GetLength(1)}");
                // 创建调试目录（桌面）
                string desktopDebugDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SpectrogramDebug");
                Directory.CreateDirectory(desktopDebugDir);
                
                // 创建AnalyzerOut目录（项目根目录）
                string analyzerOutDir = Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\AnalyzerOut");
                Directory.CreateDirectory(analyzerOutDir);
                
                // 生成唯一文件名
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                
                // 保存到桌面调试目录
                string desktopFilePath = Path.Combine(desktopDebugDir, $"spectrogram_data_{timestamp}.txt");
                SaveSpectrogramDataToFile(spectrogramData, sampleRate, desktopFilePath);
                
                // 保存到AnalyzerOut目录（符合用户期望的位置）
                string analyzerOutFilePath = Path.Combine(analyzerOutDir, $"spectrogram_data_{timestamp}.txt");
                SaveSpectrogramDataToFile(spectrogramData, sampleRate, analyzerOutFilePath);
                
                _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 频谱图数据已保存到桌面目录: {desktopFilePath}");
                _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 频谱图数据已保存到AnalyzerOut目录: {analyzerOutFilePath}");
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysis", $"Failed to save spectrogram data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 保存频谱图为PNG图像
        /// </summary>
        private void SaveSpectrogramImage(SpectrogramData spectrogramData)
        {   
            try
            {   
                _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 开始生成频谱图图像");
                _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 频谱图数据维度: {spectrogramData.Data.GetLength(0)}x{spectrogramData.Data.GetLength(1)}");
                
                // 创建AnalyzerOut目录（项目根目录）
                string analyzerOutDir = Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\AnalyzerOut");
                Directory.CreateDirectory(analyzerOutDir);
                
                // 生成唯一文件名
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string imagePath = Path.Combine(analyzerOutDir, $"spectrogram_{timestamp}.png");
                
                // 转换为二维数组以便处理
                double[,] spectrogram2D = ConvertTo2DArray(spectrogramData.Data);
                
                // 计算图像尺寸（限制宽度，保持比例）
                const int maxWidth = 1200;
                int width = Math.Min(spectrogram2D.GetLength(0), maxWidth);
                int height = spectrogram2D.GetLength(1);
                
                // 创建位图
                using (Bitmap bitmap = new Bitmap(width, height))
                {   
                    // 找到最大值和最小值用于归一化
                    double minValue = double.MaxValue;
                    double maxValue = double.MinValue;
                    
                    for (int x = 0; x < spectrogram2D.GetLength(0); x++)
                    {   
                        for (int y = 0; y < spectrogram2D.GetLength(1); y++)
                        {   
                            minValue = Math.Min(minValue, spectrogram2D[x, y]);
                            maxValue = Math.Max(maxValue, spectrogram2D[x, y]);
                        }
                    }
                    
                    // 归一化范围
                    double range = maxValue - minValue;
                    if (range == 0) range = 1; // 避免除以零
                    
                    // 填充图像数据
                    for (int x = 0; x < width; x++)
                    {   
                        for (int y = 0; y < height; y++)
                        {   
                            // 归一化到0-255范围
                            double normalizedValue = (spectrogram2D[x, y] - minValue) / range;
                            int intensity = (int)(255 * normalizedValue);
                            intensity = Math.Clamp(intensity, 0, 255);
                            
                            // 反转Y轴（可选，让低频在底部）
                            int yInverted = height - 1 - y;
                            
                            // 使用热图颜色（从蓝到红）
                            Color color = GetHeatMapColor(normalizedValue);
                            bitmap.SetPixel(x, yInverted, color);
                        }
                    }
                    
                    // 保存图像
                    bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
                    _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 频谱图图像已保存: {imagePath}");
                    _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 文件大小: {new FileInfo(imagePath).Length} 字节");
                }
            }
            catch (Exception ex)
            {   
                _logger.Error("AudioAnalysis", $"生成频谱图图像失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取热图颜色
        /// </summary>
        private Color GetHeatMapColor(double value) // value 0.0 to 1.0
        {   
            int r = 0, g = 0, b = 0;
            
            if (value < 0.25)
            {   
                // 从深蓝到浅蓝
                b = 255;
                g = (int)(value * 4 * 255);
            }
            else if (value < 0.5)
            {   
                // 从蓝到绿
                value -= 0.25;
                g = 255;
                b = 255 - (int)(value * 4 * 255);
            }
            else if (value < 0.75)
            {   
                // 从绿到黄
                value -= 0.5;
                r = (int)(value * 4 * 255);
                g = 255;
            }
            else
            {   
                // 从黄到红
                value -= 0.75;
                r = 255;
                g = 255 - (int)(value * 4 * 255);
            }
            
            return Color.FromArgb(r, g, b);
        }
        
        private void SaveSpectrogramDataToFile(double[,] spectrogramData, int sampleRate, string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine($"Sample Rate: {sampleRate}");
                writer.WriteLine($"Total Frames: {spectrogramData.GetLength(0)}");
                writer.WriteLine($"Total Bins: {spectrogramData.GetLength(1)}");
                writer.WriteLine("\nSpectrogram Data:");
                
                // 只写入前100帧和前128个频带（避免文件过大）
                int maxFrames = Math.Min(spectrogramData.GetLength(0), 100);
                int maxBins = Math.Min(spectrogramData.GetLength(1), 128);
                
                for (int frame = 0; frame < maxFrames; frame++)
                {
                    for (int bin = 0; bin < maxBins; bin++)
                    {
                        writer.Write($"{spectrogramData[frame, bin]:F6}\t");
                    }
                    writer.WriteLine();
                }
            }
        }
        #endregion

        #region 命令
        [RelayCommand]
        private async Task SelectAudioFileAsync()
        {
            try
            {
                ProgressText = "请选择音频文件...";
                
                // 使用对话框服务选择文件
                var filters = new[]
                {
                    "音频文件 (*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.aif)|*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.aif",
                    "所有文件 (*.*)|*.*"
                };
                
                var selectedFile = await _dialogService.ShowOpenFileDialogAsync("选择音频文件", filters);
                
                if (!string.IsNullOrEmpty(selectedFile))
                {
                    AudioFilePath = selectedFile;
                }
                else
                {
                    ProgressText = "已取消选择";
                }
            }
            catch (Exception ex)
            {
                ProgressText = $"错误: {ex.Message}";
            }
        }

        [RelayCommand(CanExecute = nameof(CanAnalyze))]
        private async Task AnalyzeAudioAsync()
        {
            if (string.IsNullOrEmpty(AudioFilePath))
                return;

            try
            {
                IsAnalyzing = true;
                ProgressText = "正在检查文件格式...";
                DetectedNotes.Clear();
                
                // 验证文件格式
                _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 验证音频文件格式");
                
                string analyzerOutDir = Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\AnalyzerOut");
                _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 计算的AnalyzerOut路径: {analyzerOutDir}");
                Directory.CreateDirectory(analyzerOutDir);
                _logger.Info("AudioAnalysis", $"[{DateTime.Now}] AnalyzerOut目录已创建或存在");

                // 验证文件格式
                var isSupported = await _audioAnalysisService.IsSupportedFormatAsync(AudioFilePath);
                if (!isSupported)
                {
                    ProgressText = "不支持的音频格式";
                    return;
                }

                // 创建分析选项
                var options = new AudioAnalysisOptions
                {
                    TargetSampleRate = 44100,
                    NoteOptions = new NoteDetectionOptions
                    {
                        MinNoteDuration = 0.05,
                        VolumeThreshold = 0.1,
                        EnableChordDetection = false
                    }
                };

                // 创建进度报告
                var progress = new Progress<(double Progress, string Status)>(p =>
                {
                    ProgressText = $"{p.Status} ({p.Progress:F0}%)";
                });

                // 执行分析
                ProgressText = "正在分析音频...";
                var result = await _audioAnalysisService.AnalyzeAudioAsync(
                    AudioFilePath,
                    options,
                    progress);

                // 生成频谱图（不再检测音符，只生成频谱）
                ProgressText = "正在生成频谱图...";
                
                // 读取音频数据
                var audioData = result.ProcessedSamples;
                if (audioData != null && audioData.Length > 0)
                {
                    // 生成完整的频谱图数据
                    ProgressText = "正在生成频谱图数据...";
                    var spectrogramData = await Task.Run(() =>
                        _spectrogramService.GenerateSpectrogramAsync(audioData, result.ProcessedSampleRate, 1, 2048, 512));

                    // 检查频谱图数据是否有效
                    if (spectrogramData?.Data == null)
                    {
                        ProgressText = "频谱图数据为空，生成失败";
                        return;
                    }

                    // 检查频谱图数据是否全为零
                    bool allZero = true;
                    double maxSpectrogramValue = 0;
                    for (int i = 0; i < Math.Min(10, spectrogramData.Data.Length); i++)
                    {
                        if (spectrogramData.Data[i] != null)
                        {
                            for (int j = 0; j < Math.Min(10, spectrogramData.Data[i].Length); j++)
                            {
                                double absValue = Math.Abs(spectrogramData.Data[i][j]);
                                maxSpectrogramValue = Math.Max(maxSpectrogramValue, absValue);
                                if (absValue > 1e-10)
                                {
                                    allZero = false;
                                    break;
                                }
                            }
                            if (!allZero) break;
                        }
                    }

                    // 重新计算数据的最大值和最小值用于日志
                    double localMaxValue = double.MinValue;
                    double localMinValue = double.MaxValue;
                    
                    // 检查spectrogramData.Data是否有数据（它是float[][]类型）
                    if (spectrogramData.Data != null && spectrogramData.Data.Length > 0)
                    {
                        // 简单检查前10x10区域
                        int rowCount = Math.Min(spectrogramData.Data.Length, 10);
                        for (int i = 0; i < rowCount; i++)
                        {
                            if (spectrogramData.Data[i] != null && spectrogramData.Data[i].Length > 0)
                            {
                                int colCount = Math.Min(spectrogramData.Data[i].Length, 10);
                                for (int j = 0; j < colCount; j++)
                                {
                                    localMaxValue = Math.Max(localMaxValue, spectrogramData.Data[i][j]);
                                    localMinValue = Math.Min(localMinValue, spectrogramData.Data[i][j]);
                                }
                            }
                        }
                    }
                    
                    if (allZero)
                    {
                        ProgressText = "警告：频谱图数据全为零，可能是音频数据问题";
                        // 继续处理，但记录警告
                        _logger.Warn("AudioAnalysis", $"[{DateTime.Now}] 警告: 频谱图数据前10x10区域全为零值，可能存在问题");
                        System.Diagnostics.Debug.WriteLine($"AudioAnalysisViewModel: 频谱图数据全为零，最大值: {localMaxValue}");
                    }
                    else
                    {
                        _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 频谱图数据检查正常 - 最大值: {localMaxValue}, 最小值: {localMinValue}");
                        System.Diagnostics.Debug.WriteLine($"AudioAnalysisViewModel: 频谱图数据正常，最大值: {localMaxValue}");
                    }

                   // 将频谱图数据传递给钢琴卷帘（使用现有的导入方法）
                   if (spectrogramData != null && spectrogramData.Data != null) 
                   {   
                       // 立即生成并保存频谱图图像
                       _logger.Info("AudioAnalysis", $"[{DateTime.Now}] 准备生成MIDI对齐频谱图图像");
                       SaveSpectrogramImage(spectrogramData);
                       _logger.Info("AudioAnalysis", $"[{DateTime.Now}] MIDI对齐频谱图图像生成完成");
                       // 生成MIDI音高对齐的频谱数据（21-108对应A0-C8）
                       int minMidiNote = 21;  // A0
                       int maxMidiNote = 108; // C8
                        
                       var midiSpectrogram = _spectrogramService.GetSpectrogramForMidiRange(
                           spectrogramData, minMidiNote, maxMidiNote);
                        
                       // 转换为二维数组格式
                       double[,] midiSpectrogramArray = ConvertMidiSpectrogramTo2DArray(midiSpectrogram);
                        
                       // 立即保存频谱图数据到文件进行验证
                        SaveSpectrogramDataForDebug(midiSpectrogramArray, result.ProcessedSampleRate);
                        
                       if (PianoRollViewModel != null)
                       {
                           // 计算音频时长（秒）
                           double duration = (double)audioData.Length / result.ProcessedSampleRate;
                            
                           // 计算最大频率（通常是奈奎斯特频率的一半）
                           double maxFrequency = result.ProcessedSampleRate / 2.0;
                            
                           // 使用现有的 ImportSpectrogramData 方法
                           PianoRollViewModel.ImportSpectrogramData(
                               midiSpectrogramArray,
                               result.ProcessedSampleRate,
                               duration,
                               maxFrequency); // 使用实际的奈奎斯特频率
                            
                           // 启用频谱图可见性
                           PianoRollViewModel.IsSpectrogramVisible = true;
                            
                           // 计算对应的MIDI时长（四分音符数量）
                           // 使用当前Tempo计算：duration(秒) * (BPM / 60) = 四分音符数量
                           double midiDuration = duration * (PianoRollViewModel.CurrentTempo / 60.0);
                           PianoRollViewModel.MidiFileDuration = Math.Max(midiDuration, 32.0); // 至少32个四分音符
                            
                           ProgressText = $"频谱图已生成并设置到钢琴卷帘背景 ({spectrogramData.NumFrames}帧 × {maxMidiNote - minMidiNote + 1}音高), 时长: {midiDuration:F1}拍";
                       }
                       else
                       {
                           ProgressText = spectrogramData != null
                               ? "频谱图已生成，但未连接到钢琴卷帘"
                               : "频谱图生成失败";
                       }
                   }
                   else
                   {
                       ProgressText = "频谱图数据为空，无法设置到钢琴卷帘";
                   }
                }
                else
                {
                    ProgressText = "音频数据为空，无法生成频谱图";
                }
            }
            catch (Exception ex)
            {
                ProgressText = $"分析失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"音频分析错误: {ex}");
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        private bool CanAnalyze() => !string.IsNullOrEmpty(AudioFilePath) && !IsAnalyzing;
        #endregion

        #region 属性变化处理
        partial void OnAudioFilePathChanged(string? value)
        {
            AnalyzeAudioCommand.NotifyCanExecuteChanged();
            ProgressText = string.IsNullOrEmpty(value) ? "就绪" : $"已选择: {value}";
        }

        partial void OnIsAnalyzingChanged(bool value)
        {
            AnalyzeAudioCommand.NotifyCanExecuteChanged();
        }
        #endregion
    
        /// <summary>
        /// 将MIDI音高对齐的频谱数据转换为二维数组
        /// </summary>
        private static double[,] ConvertMidiSpectrogramTo2DArray(float[][] midiSpectrogram)
        {
            if (midiSpectrogram == null || midiSpectrogram.Length == 0)
                return new double[0, 0];
    
            int timeFrames = midiSpectrogram.Length;
            int midiNotes = midiSpectrogram[0].Length;
            
            double[,] result = new double[timeFrames, midiNotes];
            
            // 检查数据有效性
            bool allZero = true;
            double maxValue = 0;
            
            for (int t = 0; t < timeFrames; t++)
            {
                for (int n = 0; n < midiNotes; n++)
                {
                    double value = midiSpectrogram[t][n];
                    result[t, n] = value;
                    maxValue = Math.Max(maxValue, Math.Abs(value));
                    if (Math.Abs(value) > 1e-10)
                    {
                        allZero = false;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"ConvertMidiSpectrogramTo2DArray: {timeFrames}帧 × {midiNotes}音高, 最大值: {maxValue:F4}, 全零: {allZero}");
            
            return result;
        }
    
        /// <summary>
        /// 将交错数组转换为二维数组（原始频谱）
        /// </summary>
        private static double[,] ConvertTo2DArray(float[][] jaggedArray)
        {
            if (jaggedArray == null || jaggedArray.Length == 0)
                return new double[0, 0];
    
            int rows = jaggedArray.Length;
            int cols = jaggedArray[0].Length;
            
            double[,] result = new double[rows, cols];
            
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i, j] = jaggedArray[i][j];
                }
            }
            
            return result;
        }
    }

    /// <summary>
    /// 检测到的音符视图模型
    /// </summary>
    public partial class DetectedNoteViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _noteNumber;
        
        [ObservableProperty]
        private double _startTime;
        
        [ObservableProperty]
        private double _duration;
        
        [ObservableProperty]
        private int _velocity;

        public string NoteName => GetNoteName(NoteNumber);

        private static string GetNoteName(int noteNumber)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (noteNumber / 12) - 1;
            int note = noteNumber % 12;
            return $"{noteNames[note]}{octave}";
        }
    }
}