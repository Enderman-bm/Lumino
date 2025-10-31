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
            _audioAnalysisService = audioAnalysisService ?? new AudioAnalysisService();
            _spectrogramService = new SpectrogramService();
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

                    if (allZero)
                    {
                        ProgressText = "警告：频谱图数据全为零，可能是音频数据问题";
                        // 继续处理，但记录警告
                        System.Diagnostics.Debug.WriteLine($"AudioAnalysisViewModel: 频谱图数据全为零，最大值: {maxSpectrogramValue}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"AudioAnalysisViewModel: 频谱图数据正常，最大值: {maxSpectrogramValue}");
                    }

                   // 将频谱图数据传递给钢琴卷帘（使用现有的导入方法）
                   if (PianoRollViewModel != null && spectrogramData != null)
                   {
                       // 计算音频时长（秒）
                       double duration = (double)audioData.Length / result.ProcessedSampleRate;
                       
                       // 计算最大频率（通常是奈奎斯特频率的一半）
                       double maxFrequency = result.ProcessedSampleRate / 2.0;
                       
                       // 生成MIDI音高对齐的频谱数据（21-108对应A0-C8）
                       int minMidiNote = 21;  // A0
                       int maxMidiNote = 108; // C8
                       
                       var midiSpectrogram = _spectrogramService.GetSpectrogramForMidiRange(
                           spectrogramData, minMidiNote, maxMidiNote);
                       
                       // 转换为二维数组格式
                       double[,] midiSpectrogramArray = ConvertMidiSpectrogramTo2DArray(midiSpectrogram);
                       
                       // 使用现有的 ImportSpectrogramData 方法
                       PianoRollViewModel.ImportSpectrogramData(
                           midiSpectrogramArray,
                           result.ProcessedSampleRate,
                           duration,
                           maxMidiNote - minMidiNote + 1); // 使用音高数量作为频率范围
                       
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