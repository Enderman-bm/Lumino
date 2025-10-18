using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using EnderAudioAnalyzer.Interfaces;
using EnderAudioAnalyzer.Models;
using EnderDebugger;

namespace EnderAudioAnalyzer.Services
{
    /// <summary>
    /// 主音频分析服务 - 整合所有音频分析功能
    /// </summary>
    public class AudioAnalysisService : IAudioAnalysisService
    {
        private readonly EnderLogger _logger;
        private readonly IAudioFileService _audioFileService;
        private readonly ISpectrumAnalysisService _spectrumService;
        private readonly IRhythmAnalysisService _rhythmService;
        private readonly INoteDetectionService _noteService;

        private CancellationTokenSource _cancellationTokenSource;
        private AnalysisStatus _currentStatus;
        private AudioAnalysisResult _currentResult;

        public event EventHandler<AnalysisProgressEventArgs> AnalysisProgress;
        public event EventHandler<AnalysisCompletedEventArgs> AnalysisCompleted;

        public AudioAnalysisService()
        {
            _logger = new EnderLogger("AudioAnalysisService");
            _audioFileService = new AudioFileService();
            _spectrumService = new SpectrumAnalysisService();
            _rhythmService = new RhythmAnalysisService();
            _noteService = new NoteDetectionService();

            _currentStatus = AnalysisStatus.NotStarted;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 执行完整的音频分析
        /// </summary>
        public async Task<AudioAnalysisResult> AnalyzeAudioAsync(
            string filePath,
            Models.AudioAnalysisOptions options,
            IProgress<(double Progress, string Status)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info("AudioAnalysisService", $"开始音频分析: {filePath}");
                _currentStatus = AnalysisStatus.InProgress;
                _cancellationTokenSource = new CancellationTokenSource();

                var result = new AudioAnalysisResult
                {
                    FilePath = filePath,
                    StartTime = DateTime.Now,
                    Status = AnalysisStatus.InProgress
                };

                // 步骤1: 加载和预处理音频文件
                OnProgressChanged("正在加载音频文件...", 0);
                var audioInfo = await _audioFileService.LoadAudioFileAsync(filePath);
                result.AudioInfo = audioInfo;

                if (!audioInfo.IsSupported)
                {
                    throw new NotSupportedException($"不支持的音频格式: {audioInfo.Format}");
                }

                // 步骤2: 预处理音频数据
                OnProgressChanged("正在预处理音频数据...", 20);
                var processedSamples = await _audioFileService.PreprocessAudioAsync(filePath, options.TargetSampleRate);
                result.ProcessedSamples = processedSamples;
                result.ProcessedSampleRate = options.TargetSampleRate;

                // 步骤3: 频谱分析
                OnProgressChanged("正在执行频谱分析...", 40);
                var spectrumData = await _spectrumService.AnalyzeSpectrumAsync(
                    processedSamples, options.TargetSampleRate, options.SpectrumOptions);
                result.SpectrumData = new List<SpectrumData> { spectrumData };

                // 步骤4: 节奏分析
                OnProgressChanged("正在分析节奏...", 60);
                var rhythmAnalysis = await _rhythmService.AnalyzeRhythmAsync(
                    processedSamples, options.TargetSampleRate, options.RhythmOptions);
                result.RhythmAnalysis = rhythmAnalysis;

                // 步骤5: 音符检测
                OnProgressChanged("正在检测音符...", 80);
                var detectedNotes = await _noteService.DetectNotesAsync(
                    processedSamples, options.TargetSampleRate, options.NoteOptions);
                result.DetectedNotes = detectedNotes;

                // 步骤6: 和弦检测（如果启用）
                if (options.NoteOptions.EnableChordDetection && detectedNotes.Count > 0)
                {
                    OnProgressChanged("正在检测和弦...", 90);
                    var chordOptions = new ChordDetectionOptions
                    {
                        TimeWindow = 0.1, // 100ms时间窗口
                        MinChordNotes = 2
                    };
                    var detectedChords = await _noteService.DetectChordsAsync(detectedNotes, chordOptions);
                    result.DetectedChords = detectedChords;
                }

                // 完成分析
                result.EndTime = DateTime.Now;
                result.Status = AnalysisStatus.Completed;

                _currentStatus = AnalysisStatus.Completed;
                _currentResult = result;

                OnProgressChanged("分析完成", 100);
                OnAnalysisCompleted(result);

                _logger.Info("AudioAnalysisService", 
                    $"音频分析完成: 时长={result.AnalysisDuration.TotalSeconds:F2}s, " +
                    $"音符数={detectedNotes.Count}, BPM={rhythmAnalysis.BPM:F1}");

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("AudioAnalysisService", "音频分析被用户取消");
                _currentStatus = AnalysisStatus.Cancelled;
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"音频分析失败: {ex.Message}");
                _currentStatus = AnalysisStatus.Failed;
                throw;
            }
        }

        /// <summary>
        /// 实时音频分析
        /// </summary>
        public async Task<Models.AudioAnalysisResult> AnalyzeRealtimeAsync(float[] audioSamples, int sampleRate, Models.AudioAnalysisOptions options)
        {
            try
            {
                _logger.Debug("AudioAnalysisService", $"开始实时分析: 样本数={audioSamples.Length}, 采样率={sampleRate}Hz");

                var result = new Models.AudioAnalysisResult
                {
                    DetectedNotes = new List<DetectedNote>(),
                    SpectrumData = new List<SpectrumData>(),
                    QualityScore = 0
                };

                // 并行执行不同的分析任务
                var analysisTasks = new List<Task>();

                // 频谱分析
                var spectrumTask = _spectrumService.AnalyzeSpectrumAsync(audioSamples, sampleRate, options.SpectrumOptions);
                analysisTasks.Add(spectrumTask);

                // 音高检测
                var pitchTask = _spectrumService.DetectPitchAsync(audioSamples, sampleRate, options.PitchOptions);
                analysisTasks.Add(pitchTask);

                // 节拍跟踪（如果有当前BPM）
                Task<BeatTrackingResult> beatTask = null;
                if (options.RhythmOptions.EnableBeatTracking)
                {
                    beatTask = _rhythmService.TrackBeatsAsync(audioSamples, sampleRate, 120.0, options.RhythmOptions);
                    analysisTasks.Add(beatTask);
                }

                // 等待所有分析完成
                await Task.WhenAll(analysisTasks);

                // 收集结果
                result.SpectrumData = new List<SpectrumData> { await spectrumTask };
                // 暂时注释掉不存在的属性
                // result.DetectedPitch = await pitchTask;

                if (beatTask != null)
                {
                    var beatResult = await beatTask;
                    result.RhythmAnalysis = new RhythmAnalysis
                    {
                        BPM = beatResult.CurrentBPM,
                        Beats = beatResult.DetectedBeats ?? new List<Beat>(),
                        Confidence = beatResult.Confidence
                    };
                }

                // 如果有检测到音高，进行谐波分析
                // 暂时注释掉不存在的属性和方法
                // if (result.DetectedPitch.HasValue)
                // {
                //     result.HarmonicAnalysis = await _spectrumService.AnalyzeHarmonicsAsync(
                //         result.SpectrumData, result.DetectedPitch.Value);
                // }

                _logger.Debug("AudioAnalysisService", $"实时分析完成");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"实时分析失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 取消当前分析
        /// </summary>
        public void CancelAnalysis()
        {
            _logger.Info("AudioAnalysisService", "用户请求取消分析");
            _cancellationTokenSource?.Cancel();
            _currentStatus = AnalysisStatus.Cancelled;
        }

        /// <summary>
        /// 获取当前分析状态
        /// </summary>
        public AnalysisStatus GetCurrentStatus()
        {
            return _currentStatus;
        }

        /// <summary>
        /// 获取最近的分析结果
        /// </summary>
        public AudioAnalysisResult GetLastResult()
        {
            return _currentResult;
        }

        /// <summary>
        /// 验证音频文件
        /// </summary>
        public async Task<AudioValidationResult> ValidateAudioFileAsync(string filePath)
        {
            try
            {
                _logger.Debug("AudioAnalysisService", $"验证音频文件: {filePath}");

                var result = new AudioValidationResult
                {
                    FilePath = filePath,
                    IsValid = false
                };

                // 检查文件是否存在
                if (!System.IO.File.Exists(filePath))
                {
                    result.Errors.Add("文件不存在");
                    return result;
                }

                // 检查文件大小
                var fileInfo = new System.IO.FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    result.Errors.Add("文件为空");
                    return result;
                }

                // 检查文件格式
                var isSupported = await _audioFileService.IsSupportedFormatAsync(filePath);
                if (!isSupported)
                {
                    result.Errors.Add("不支持的音频格式");
                    return result;
                }

                // 尝试加载音频信息
                try
                {
                    var audioInfo = await _audioFileService.LoadAudioFileAsync(filePath);
                    result.AudioInfo = audioInfo;
                    result.Duration = audioInfo.Duration;
                    result.SampleRate = audioInfo.SampleRate;

                    // 检查音频时长
                    if (audioInfo.Duration < 0.1) // 最少100ms
                    {
                        result.Warnings.Add("音频时长过短，可能无法进行有效分析");
                    }

                    if (audioInfo.Duration > 300) // 最长5分钟
                    {
                        result.Warnings.Add("音频时长过长，分析可能需要较长时间");
                    }

                    // 检查采样率
                    if (audioInfo.SampleRate < 8000)
                    {
                        result.Warnings.Add("采样率较低，可能影响分析精度");
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"音频文件解析失败: {ex.Message}");
                    return result;
                }

                result.IsValid = result.Errors.Count == 0;
                _logger.Info("AudioAnalysisService", $"音频文件验证完成: 有效={result.IsValid}, 时长={result.Duration:F2}s");

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"音频文件验证失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取分析统计信息
        /// </summary>
        public AnalysisStatistics GetStatistics(AudioAnalysisResult result)
        {
            var statistics = new AnalysisStatistics
            {
                TotalNotes = result.DetectedNotes?.Count ?? 0,
                TotalChords = result.DetectedChords?.Count ?? 0,
                DetectedBPM = result.RhythmAnalysis?.BPM ?? 0,
                AnalysisDuration = result.AnalysisDuration,
                AudioDuration = result.AudioInfo?.Duration ?? 0
            };

            if (result.DetectedNotes?.Count > 0)
            {
                statistics.AverageNoteDuration = result.DetectedNotes.Average(n => n.Duration);
                statistics.AverageNoteConfidence = (float)result.DetectedNotes.Average(n => n.Confidence);
                statistics.NoteRange = CalculateNoteRange(result.DetectedNotes);
            }

            if (result.SpectrumData != null && result.SpectrumData.Count > 0)
            {
                statistics.SpectralPeaks = result.SpectrumData[0].Peaks?.Count ?? 0;
            }

            return statistics;
        }

        /// <summary>
        /// 计算音符范围
        /// </summary>
        private (int MinNote, int MaxNote) CalculateNoteRange(List<DetectedNote> notes)
        {
            if (notes.Count == 0)
                return (0, 0);

            var midiNotes = notes.Select(n => n.MidiNote).ToList();
            return (midiNotes.Min(), midiNotes.Max());
        }

        /// <summary>
        /// 导出分析结果
        /// </summary>
        public async Task<bool> ExportAnalysisResultAsync(AudioAnalysisResult result, string exportPath, ExportFormat format)
        {
            try
            {
                _logger.Info("AudioAnalysisService", $"导出分析结果: {exportPath}, 格式={format}");

                switch (format)
                {
                    case ExportFormat.JSON:
                        return await ExportToJsonAsync(result, exportPath);
                    case ExportFormat.MIDI:
                        return await ExportToMidiAsync(result, exportPath);
                    case ExportFormat.CSV:
                        return await ExportToCsvAsync(result, exportPath);
                    default:
                        throw new NotSupportedException($"不支持的导出格式: {format}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"导出分析结果失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 导出为JSON格式
        /// </summary>
        private async Task<bool> ExportToJsonAsync(AudioAnalysisResult result, string filePath)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await System.IO.File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"JSON导出失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出为MIDI格式
        /// </summary>
        private async Task<bool> ExportToMidiAsync(AudioAnalysisResult result, string filePath)
        {
            try
            {
                // 这里需要实现MIDI导出逻辑
                // 暂时返回false，表示功能待实现
                _logger.Warn("AudioAnalysisService", "MIDI导出功能待实现");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"MIDI导出失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出为CSV格式
        /// </summary>
        private async Task<bool> ExportToCsvAsync(AudioAnalysisResult result, string filePath)
        {
            try
            {
                var csvLines = new List<string>
                {
                    "NoteName,MidiNote,StartTime,Duration,Velocity,Confidence"
                };

                if (result.DetectedNotes != null)
                {
                    foreach (var note in result.DetectedNotes)
                    {
                        csvLines.Add($"{note.NoteName},{note.MidiNote},{note.StartTime:F3},{note.Duration:F3},{note.Velocity:F3},{note.Confidence:F3}");
                    }
                }

                await System.IO.File.WriteAllLinesAsync(filePath, csvLines);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"CSV导出失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 进度变化事件
        /// </summary>
        protected virtual void OnProgressChanged(string message, int progress)
        {
            AnalysisProgress?.Invoke(this, new AnalysisProgressEventArgs
            {
                Message = message,
                Progress = progress,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// 分析完成事件
        /// </summary>
        protected virtual void OnAnalysisCompleted(AudioAnalysisResult result)
        {
            AnalysisCompleted?.Invoke(this, new AnalysisCompletedEventArgs
            {
                Result = result,
                Timestamp = DateTime.Now
            });
        }

        #region 接口实现

        /// <summary>
        /// 获取指定时间点的频谱数据
        /// </summary>
        public async Task<SpectrumData> GetSpectrumDataAsync(string filePath, double time, double windowSize = 0.1)
        {
            try
            {
                _logger.Debug("AudioAnalysisService", $"获取频谱数据: {filePath}, 时间={time}s, 窗口={windowSize}s");
                
                // 这里需要实现从音频文件获取指定时间点频谱数据的逻辑
                // 暂时返回空数据
                return new SpectrumData
                {
                    Frequencies = new double[0],
                    Magnitudes = new double[0],
                    Peaks = new List<SpectralPeak>(),
                    SampleRate = 44100,
                    FrequencyResolution = 1.0,
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"获取频谱数据失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检测音频中的音符
        /// </summary>
        public async Task<List<DetectedNote>> DetectNotesAsync(
            string filePath,
            NoteDetectionOptions options,
            IProgress<(double Progress, string Status)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("AudioAnalysisService", $"开始音符检测: {filePath}");

                // 加载音频文件
                var audioSamples = await _audioFileService.PreprocessAudioAsync(filePath, 44100); // 使用默认采样率
                var audioInfo = await _audioFileService.LoadAudioFileAsync(filePath);

                // 使用音符检测服务
                return await _noteService.DetectNotesAsync(audioSamples, audioInfo.SampleRate, options);
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"音符检测失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 分析音频节奏
        /// </summary>
        public async Task<RhythmAnalysis> AnalyzeRhythmAsync(string filePath, RhythmAnalysisOptions? options = null)
        {
            try
            {
                _logger.Debug("AudioAnalysisService", $"开始节奏分析: {filePath}");

                // 加载音频文件
                var audioSamples = await _audioFileService.PreprocessAudioAsync(filePath, 44100); // 使用默认采样率
                var audioInfo = await _audioFileService.LoadAudioFileAsync(filePath);

                // 使用节奏分析服务
                return await _rhythmService.AnalyzeRhythmAsync(audioSamples, audioInfo.SampleRate, options ?? new RhythmAnalysisOptions());
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"节奏分析失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取支持的音频格式
        /// </summary>
        public List<string> GetSupportedFormats()
        {
            return new List<string> { ".wav", ".mp3", ".flac", ".ogg", ".aiff", ".aif" };
        }

        /// <summary>
        /// 检查文件格式是否支持
        /// </summary>
        public async Task<bool> IsSupportedFormatAsync(string filePath)
        {
            try
            {
                var extension = System.IO.Path.GetExtension(filePath).ToLower();
                var supportedFormats = GetSupportedFormats();
                return supportedFormats.Contains(extension);
            }
            catch (Exception ex)
            {
                _logger.Error("AudioAnalysisService", $"检查文件格式失败: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// 分析进度事件参数
    /// </summary>
    public class AnalysisProgressEventArgs : EventArgs
    {
        public string Message { get; set; }
        public int Progress { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 分析完成事件参数
    /// </summary>
    public class AnalysisCompletedEventArgs : EventArgs
    {
        public AudioAnalysisResult Result { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 音频验证结果
    /// </summary>
    public class AudioValidationResult
    {
        public string FilePath { get; set; }
        public bool IsValid { get; set; }
        public AudioFileInfo AudioInfo { get; set; }
        public double Duration { get; set; }
        public int SampleRate { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// 分析统计信息
    /// </summary>
    public class AnalysisStatistics
    {
        public int TotalNotes { get; set; }
        public int TotalChords { get; set; }
        public int SpectralPeaks { get; set; }
        public double DetectedBPM { get; set; }
        public TimeSpan AnalysisDuration { get; set; }
        public double AudioDuration { get; set; }
        public double AverageNoteDuration { get; set; }
        public float AverageNoteConfidence { get; set; }
        public (int MinNote, int MaxNote) NoteRange { get; set; }
    }

    /// <summary>
    /// 导出格式
    /// </summary>
    public enum ExportFormat
    {
        JSON,
        MIDI,
        CSV
    }
}