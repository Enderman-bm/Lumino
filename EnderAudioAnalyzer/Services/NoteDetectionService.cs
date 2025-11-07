using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnderAudioAnalyzer.Interfaces;
using EnderAudioAnalyzer.Models;
using EnderDebugger;

namespace EnderAudioAnalyzer.Services
{
    /// <summary>
    /// 音符检测服务 - 负责从音频中检测音符和和弦
    /// </summary>
    public class NoteDetectionService : INoteDetectionService
    {
        private readonly EnderLogger _logger;
        private readonly ISpectrumAnalysisService _spectrumService;

        public NoteDetectionService()
        {
            _logger = new EnderLogger("NoteDetectionService");
            _spectrumService = new SpectrumAnalysisService();
        }

        /// <summary>
        /// 检测音频中的音符
        /// </summary>
        public async Task<List<DetectedNote>> DetectNotesAsync(float[] audioSamples, int sampleRate, NoteDetectionOptions options)
        {
            try
            {
                _logger.Debug("NoteDetectionService", $"开始音符检测: 样本数={audioSamples.Length}, 采样率={sampleRate}Hz");

                var detectedNotes = new List<DetectedNote>();
                int windowSize = 1024; // 分析窗口大小
                int hopSize = windowSize / 4; // 重叠75%
                double timePerFrame = (double)hopSize / sampleRate;

                // 分帧处理音频
                for (int start = 0; start < audioSamples.Length - windowSize; start += hopSize)
                {
                    var frameSamples = new float[windowSize];
                    Array.Copy(audioSamples, start, frameSamples, 0, windowSize);

                    // 分析当前帧
                    var frameNotes = await AnalyzeFrameAsync(frameSamples, sampleRate, start * timePerFrame, options);
                    detectedNotes.AddRange(frameNotes);
                }

                // 合并连续的音符
                var mergedNotes = MergeContinuousNotes(detectedNotes, options);

                // 量化音符时值
                var quantizedNotes = QuantizeNotes(mergedNotes, options);

                _logger.Info("NoteDetectionService", $"音符检测完成: 检测到 {quantizedNotes.Count} 个音符");
                return quantizedNotes;
            }
            catch (Exception ex)
            {
                _logger.Error("NoteDetectionService", $"音符检测失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 分析单个音频帧
        /// </summary>
        private async Task<List<DetectedNote>> AnalyzeFrameAsync(float[] frameSamples, int sampleRate, double startTime, NoteDetectionOptions options)
        {
            var frameNotes = new List<DetectedNote>();

            // 计算帧的能量
            float frameEnergy = CalculateEnergy(frameSamples);

            // 如果能量低于阈值，跳过分析
            if (frameEnergy < options.VolumeThreshold)
                return frameNotes;

            // 频谱分析选项
            var spectrumOptions = new SpectrumAnalysisOptions
            {
                WindowSize = 1024,
                WindowType = WindowType.Hann,
                MinPeakHeight = -50.0,
                PeakThreshold = 2.0
            };

            // 执行频谱分析
            var spectrumData = await _spectrumService.AnalyzeSpectrumAsync(frameSamples, sampleRate, spectrumOptions);

            // 检测音高
            var pitchOptions = new PitchDetectionOptions
            {
                MinFrequency = 80.0,
                MaxFrequency = 1000.0,
                Threshold = 0.15
            };

            var pitch = await _spectrumService.DetectPitchAsync(frameSamples, sampleRate, pitchOptions);

            if (pitch.HasValue)
            {
                // 将频率转换为MIDI音符编号
                int midiNote = FrequencyToMidiNote(pitch.Value);
                string noteName = MidiNoteToName(midiNote);

                // 计算置信度
                float confidence = CalculateNoteConfidence(spectrumData, pitch.Value, frameEnergy);

                // 创建检测到的音符
                var detectedNote = new DetectedNote
                {
                    StartTime = startTime,
                    EndTime = startTime + (double)frameSamples.Length / sampleRate,
                    Duration = (double)frameSamples.Length / sampleRate,
                    NoteName = noteName,
                    MidiNote = midiNote,
                    Frequency = pitch.Value,
                    Velocity = frameEnergy,
                    Confidence = confidence
                };

                frameNotes.Add(detectedNote);
            }

            return frameNotes;
        }

        /// <summary>
        /// 计算音频帧的能量
        /// </summary>
        private float CalculateEnergy(float[] samples)
        {
            float energy = 0.0f;
            foreach (float sample in samples)
            {
                energy += Math.Abs(sample);
            }
            return energy / samples.Length;
        }

        /// <summary>
        /// 将频率转换为MIDI音符编号
        /// </summary>
        private int FrequencyToMidiNote(double frequency)
        {
            if (frequency <= 0) return 0;
            return (int)Math.Round(69 + 12 * Math.Log2(frequency / 440.0));
        }

        /// <summary>
        /// 将MIDI音符编号转换为音符名称
        /// </summary>
        private string MidiNoteToName(int midiNote)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (midiNote / 12) - 1;
            int noteIndex = midiNote % 12;
            return $"{noteNames[noteIndex]}{octave}";
        }

        /// <summary>
        /// 计算音符置信度
        /// </summary>
        private float CalculateNoteConfidence(SpectrumData spectrumData, double frequency, float energy)
        {
            float confidence = 0.0f;

            // 基于频谱峰值的置信度
            var nearestPeak = spectrumData.Peaks
                .OrderBy(p => Math.Abs(p.Frequency - frequency))
                .FirstOrDefault();

            if (nearestPeak != null)
            {
                // 频率匹配度
                double freqError = Math.Abs(nearestPeak.Frequency - frequency);
                double freqConfidence = 1.0 - Math.Min(freqError / 10.0, 1.0); // 10Hz容差

                // 峰值强度置信度
                double magnitudeConfidence = Math.Max(0, (nearestPeak.Magnitude + 60) / 60.0); // -60dB到0dB映射到0-1

                confidence = (float)((freqConfidence + magnitudeConfidence) / 2.0);
            }

            // 能量置信度
            float energyConfidence = Math.Min(energy * 2.0f, 1.0f); // 假设能量在0-0.5范围内

            return (confidence + energyConfidence) / 2.0f;
        }

        /// <summary>
        /// 合并连续的音符
        /// </summary>
        private List<DetectedNote> MergeContinuousNotes(List<DetectedNote> notes, NoteDetectionOptions options)
        {
            if (notes.Count == 0)
                return notes;

            var mergedNotes = new List<DetectedNote>();
            var currentNote = notes[0];

            for (int i = 1; i < notes.Count; i++)
            {
                var nextNote = notes[i];

                // 检查是否为同一个音符的连续帧
                if (Math.Abs(nextNote.MidiNote - currentNote.MidiNote) <= options.PitchStabilityThreshold &&
                    nextNote.StartTime - (currentNote.StartTime + currentNote.Duration) <= options.NoteSeparationThreshold)
                {
                    // 合并音符：延长时长，平均其他属性
                    currentNote.EndTime = nextNote.StartTime + nextNote.Duration;
                    currentNote.Duration = currentNote.EndTime - currentNote.StartTime;
                    currentNote.Velocity = (currentNote.Velocity + nextNote.Velocity) / 2.0f;
                    currentNote.Confidence = (currentNote.Confidence + nextNote.Confidence) / 2.0f;
                }
                else
                {
                    // 保存当前音符并开始新的音符
                    if (currentNote.Duration >= options.MinNoteDuration && 
                        currentNote.Duration <= options.MaxNoteDuration)
                    {
                        mergedNotes.Add(currentNote);
                    }
                    currentNote = nextNote;
                }
            }

            // 添加最后一个音符
            if (currentNote.Duration >= options.MinNoteDuration && 
                currentNote.Duration <= options.MaxNoteDuration)
            {
                mergedNotes.Add(currentNote);
            }

            return mergedNotes;
        }

        /// <summary>
        /// 量化音符时值
        /// </summary>
        private List<DetectedNote> QuantizeNotes(List<DetectedNote> notes, NoteDetectionOptions options)
        {
            var quantizedNotes = new List<DetectedNote>();

            foreach (var note in notes)
            {
                var quantizedNote = note.Clone();
                
                // 根据量化精度调整开始时间和时长
                double quantizeValue = GetQuantizeValue(options.Quantization);
                
                // 量化开始时间
                quantizedNote.StartTime = Math.Round(note.StartTime / quantizeValue) * quantizeValue;
                
                // 量化时长
                quantizedNote.Duration = Math.Max(
                    quantizeValue, 
                    Math.Round(note.Duration / quantizeValue) * quantizeValue
                );

                quantizedNotes.Add(quantizedNote);
            }

            return quantizedNotes;
        }

        /// <summary>
        /// 获取量化值（秒）
        /// </summary>
        private double GetQuantizeValue(QuantizationPrecision precision)
        {
            // 假设BPM为120，四分音符=0.5秒
            double quarterNote = 0.5;

            return precision switch
            {
                QuantizationPrecision.WholeNote => quarterNote * 4,
                QuantizationPrecision.HalfNote => quarterNote * 2,
                QuantizationPrecision.QuarterNote => quarterNote,
                QuantizationPrecision.EighthNote => quarterNote / 2,
                QuantizationPrecision.SixteenthNote => quarterNote / 4,
                QuantizationPrecision.ThirtySecondNote => quarterNote / 8,
                QuantizationPrecision.SixtyFourthNote => quarterNote / 16,
                _ => quarterNote
            };
        }

        /// <summary>
        /// 检测和弦
        /// </summary>
        public async Task<List<Models.DetectedChord>> DetectChordsAsync(List<DetectedNote> notes, Models.ChordDetectionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Debug("NoteDetectionService", $"开始和弦检测: 音符数={notes.Count}");

                    var detectedChords = new List<DetectedChord>();

                    if (notes.Count < options.MinChordNotes)
                    {
                        _logger.Warn("NoteDetectionService", "音符数量不足，无法检测和弦");
                        return detectedChords;
                    }

                    // 按时间窗口分组音符
                    var timeWindows = GroupNotesByTimeWindow(notes, options.TimeWindow);

                    foreach (var window in timeWindows)
                    {
                        var chordsInWindow = FindChordsInWindow(window, options);
                        detectedChords.AddRange(chordsInWindow);
                    }

                    _logger.Info("NoteDetectionService", $"和弦检测完成: 检测到 {detectedChords.Count} 个和弦");
                    return detectedChords;
                }
                catch (Exception ex)
                {
                    _logger.Error("NoteDetectionService", $"和弦检测失败: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// 按时间窗口分组音符
        /// </summary>
        private List<List<DetectedNote>> GroupNotesByTimeWindow(List<DetectedNote> notes, double timeWindow)
        {
            var groups = new List<List<DetectedNote>>();
            
            if (notes.Count == 0)
                return groups;

            // 按开始时间排序
            var sortedNotes = notes.OrderBy(n => n.StartTime).ToList();

            var currentGroup = new List<DetectedNote> { sortedNotes[0] };
            double groupEndTime = sortedNotes[0].StartTime + timeWindow;

            for (int i = 1; i < sortedNotes.Count; i++)
            {
                var note = sortedNotes[i];

                if (note.StartTime <= groupEndTime)
                {
                    // 在同一个时间窗口内
                    currentGroup.Add(note);
                    groupEndTime = Math.Max(groupEndTime, note.StartTime + timeWindow);
                }
                else
                {
                    // 开始新的时间窗口
                    groups.Add(currentGroup);
                    currentGroup = new List<DetectedNote> { note };
                    groupEndTime = note.StartTime + timeWindow;
                }
            }

            // 添加最后一组
            groups.Add(currentGroup);

            return groups;
        }

        /// <summary>
        /// 在时间窗口内查找和弦
        /// </summary>
        private List<DetectedChord> FindChordsInWindow(List<DetectedNote> notes, ChordDetectionOptions options)
        {
            var chords = new List<DetectedChord>();

            if (notes.Count < options.MinChordNotes)
                return chords;

            // 获取所有可能的音符组合
            var noteCombinations = GetNoteCombinations(notes, options.MinChordNotes, options.MaxChordNotes);

            foreach (var combination in noteCombinations)
            {
                var chord = IdentifyChord(combination);
                if (chord != null)
                {
                    chords.Add(chord);
                }
            }

            return chords;
        }

        /// <summary>
        /// 获取音符组合
        /// </summary>
        private List<List<DetectedNote>> GetNoteCombinations(List<DetectedNote> notes, int minSize, int maxSize)
        {
            var combinations = new List<List<DetectedNote>>();

            for (int size = minSize; size <= Math.Min(maxSize, notes.Count); size++)
            {
                combinations.AddRange(GetCombinations(notes, size));
            }

            return combinations;
        }

        /// <summary>
        /// 获取指定大小的组合
        /// </summary>
        private List<List<DetectedNote>> GetCombinations(List<DetectedNote> notes, int size)
        {
            var combinations = new List<List<DetectedNote>>();
            GenerateCombinations(notes, size, 0, new List<DetectedNote>(), combinations);
            return combinations;
        }

        /// <summary>
        /// 递归生成组合
        /// </summary>
        private void GenerateCombinations(List<DetectedNote> notes, int size, int start, List<DetectedNote> current, List<List<DetectedNote>> combinations)
        {
            if (current.Count == size)
            {
                combinations.Add(new List<DetectedNote>(current));
                return;
            }

            for (int i = start; i < notes.Count; i++)
            {
                current.Add(notes[i]);
                GenerateCombinations(notes, size, i + 1, current, combinations);
                current.RemoveAt(current.Count - 1);
            }
        }

        /// <summary>
        /// 识别和弦类型
        /// </summary>
        private DetectedChord? IdentifyChord(List<DetectedNote> notes)
        {
            if (notes.Count < 2)
                return null;

            // 按MIDI音符编号排序
            var sortedNotes = notes.OrderBy(n => n.MidiNote).ToList();
            var intervals = new List<int>();

            // 计算音符之间的音程
            for (int i = 1; i < sortedNotes.Count; i++)
            {
                intervals.Add(sortedNotes[i].MidiNote - sortedNotes[0].MidiNote);
            }

            // 识别和弦类型
            string? chordType = IdentifyChordType(intervals);
            if (chordType == null || chordType == "Unknown")
                return null;

            // 计算和弦属性
            double startTime = notes.Min(n => n.StartTime);
            double endTime = notes.Max(n => n.StartTime + n.Duration);
            float averageConfidence = (float)notes.Average(n => n.Confidence);
            float averageVelocity = (float)notes.Average(n => n.Velocity);

            return new DetectedChord
            {
                RootNote = sortedNotes[0].MidiNote,
                ChordType = chordType,
                Notes = sortedNotes.Select(n => n.MidiNote).ToList(),
                StartTime = startTime,
                Duration = endTime - startTime,
                Confidence = averageConfidence,
                Velocity = averageVelocity
            };
        }

        /// <summary>
        /// 识别和弦类型
        /// </summary>
        private string? IdentifyChordType(List<int> intervals)
        {
            // 常见的和弦音程模式
            var chordPatterns = new Dictionary<string, int[]>
            {
                { "Major", new[] { 4, 7 } },        // 大三和弦: 根音 + 大三度 + 纯五度
                { "Minor", new[] { 3, 7 } },        // 小三和弦: 根音 + 小三度 + 纯五度
                { "Diminished", new[] { 3, 6 } },   // 减三和弦: 根音 + 小三度 + 减五度
                { "Augmented", new[] { 4, 8 } },    // 增三和弦: 根音 + 大三度 + 增五度
                { "Sus2", new[] { 2, 7 } },         // 挂二和弦
                { "Sus4", new[] { 5, 7 } },         // 挂四和弦
                { "Major7", new[] { 4, 7, 11 } },   // 大七和弦
                { "Minor7", new[] { 3, 7, 10 } },   // 小七和弦
                { "Dominant7", new[] { 4, 7, 10 } } // 属七和弦
            };

            foreach (var pattern in chordPatterns)
            {
                if (IntervalsMatch(intervals, pattern.Value))
                {
                    return pattern.Key;
                }
            }

            return null;
        }

        /// <summary>
        /// 检查音程是否匹配
        /// </summary>
        private bool IntervalsMatch(List<int> actualIntervals, int[] expectedIntervals)
        {
            if (actualIntervals.Count != expectedIntervals.Length)
                return false;

            for (int i = 0; i < actualIntervals.Count; i++)
            {
                if (Math.Abs(actualIntervals[i] - expectedIntervals[i]) > 1) // 允许1个半音的偏差
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 检测滑音
        /// </summary>
        public async Task<List<Models.Glissando>> DetectGlissandosAsync(List<DetectedNote> notes, Models.GlissandoDetectionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Debug("NoteDetectionService", $"开始滑音检测: 音符数={notes.Count}");

                    var glissandos = new List<Glissando>();

                    if (!options.EnableGlissandoDetection || notes.Count < 2)
                        return glissandos;

                    // 按时间顺序排序音符
                    var sortedNotes = notes.OrderBy(n => n.StartTime).ToList();

                    for (int i = 0; i < sortedNotes.Count - 1; i++)
                    {
                        var currentNote = sortedNotes[i];
                        var nextNote = sortedNotes[i + 1];

                        // 检查时间间隔
                        double timeGap = nextNote.StartTime - (currentNote.StartTime + currentNote.Duration);
                        if (timeGap > options.MaxGapBetweenNotes)
                            continue;

                        // 检查音高变化率
                        double pitchChange = Math.Abs(nextNote.MidiNote - currentNote.MidiNote);
                        double timeInterval = nextNote.StartTime - currentNote.StartTime;
                        double pitchChangeRate = pitchChange / timeInterval;

                        if (pitchChangeRate >= options.MinPitchChangeRate && 
                            pitchChangeRate <= options.MaxPitchChangeRate)
                        {
                            glissandos.Add(new Glissando
                            {
                                StartNote = currentNote,
                                EndNote = nextNote,
                                PitchChange = (int)Math.Round(pitchChange),
                                Duration = timeInterval,
                                Rate = pitchChangeRate,
                                Confidence = CalculateGlissandoConfidence(currentNote, nextNote, pitchChangeRate)
                            });
                        }
                    }

                    _logger.Info("NoteDetectionService", $"滑音检测完成: 检测到 {glissandos.Count} 个滑音");
                    return glissandos;
                }
                catch (Exception ex)
                {
                    _logger.Error("NoteDetectionService", $"滑音检测失败: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// 计算滑音置信度
        /// </summary>
        private float CalculateGlissandoConfidence(DetectedNote startNote, DetectedNote endNote, double pitchChangeRate)
        {
            float confidence = 0.0f;

            // 基于音符置信度
            confidence += ((float)(startNote.Confidence + endNote.Confidence) / 2.0f) * 0.5f;

            // 基于音高变化率的置信度（理想范围在10-50半音/秒）
            double rateConfidence = 1.0 - Math.Abs(pitchChangeRate - 30) / 30.0; // 以30半音/秒为中心
            confidence += (float)Math.Max(0, rateConfidence) * 0.5f;

            return Math.Min(confidence, 1.0f);
        }
    }

    /// <summary>
    /// 音符检测服务接口
    /// </summary>
    public interface INoteDetectionService
    {
        /// <summary>
        /// 检测音频中的音符
        /// </summary>
        Task<List<DetectedNote>> DetectNotesAsync(float[] audioSamples, int sampleRate, NoteDetectionOptions options);

        /// <summary>
        /// 检测和弦
        /// </summary>
        Task<List<Models.DetectedChord>> DetectChordsAsync(List<DetectedNote> notes, Models.ChordDetectionOptions options);

        /// <summary>
        /// 检测滑音
        /// </summary>
        Task<List<Models.Glissando>> DetectGlissandosAsync(List<DetectedNote> notes, Models.GlissandoDetectionOptions options);
    }
}