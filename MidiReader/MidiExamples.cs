using System;
using System.IO;
using System.Linq;

namespace MidiReader
{
    /// <summary>
    /// MIDI读取器的示例用法和实用工具
    /// </summary>
    public static class MidiReaderExamples
    {
        /// <summary>
        /// 基本用法示例：加载并分析MIDI文件
        /// </summary>
        public static void BasicUsageExample(string filePath)
        {
            // 加载MIDI文件
            using var midiFile = MidiFile.LoadFromFile(filePath);

            // 获取基本信息
            var stats = midiFile.GetStatistics();
            Console.WriteLine($"文件格式: {stats.Header.Format}");
            Console.WriteLine($"轨道数量: {stats.TrackCount}");
            Console.WriteLine($"总音符数: {stats.TotalNotes}");
            Console.WriteLine($"总事件数: {stats.TotalEvents}");
            Console.WriteLine($"文件大小: {stats.FileSizeBytes / 1024.0:F2} KB");

            // 分析每个轨道
            for (int i = 0; i < midiFile.Tracks.Count; i++)
            {
                var track = midiFile.Tracks[i];
                var trackStats = track.GetStatistics();
                Console.WriteLine($"轨道 {i + 1}: {track.Name ?? "无名称"} - {trackStats.NoteCount} 音符, {trackStats.EventCount} 事件");
            }
        }

        /// <summary>
        /// 高性能流式处理示例：处理大型MIDI文件
        /// </summary>
        public static void StreamingProcessingExample(string filePath)
        {
            using var midiFile = MidiFile.LoadFromFile(filePath);

            Console.WriteLine("开始流式处理...");
            int processedNotes = 0;

            // 使用流式处理避免将所有事件加载到内存
            foreach (var (evt, trackIndex, absoluteTime) in midiFile.GetAllNotesStreamable())
            {
                if (evt.IsNoteOnEvent())
                {
                    processedNotes++;
                    
                    // 处理每个音符事件
                    string noteName = evt.GetNoteName();
                    double frequency = evt.GetNoteFrequency();
                    
                    // 每10000个音符报告一次进度
                    if (processedNotes % 10000 == 0)
                    {
                        Console.WriteLine($"已处理 {processedNotes} 个音符...");
                    }
                }
            }

            Console.WriteLine($"流式处理完成，总共处理了 {processedNotes} 个音符");
        }

        /// <summary>
        /// 音符分析示例
        /// </summary>
        public static void NoteAnalysisExample(string filePath)
        {
            using var midiFile = MidiFile.LoadFromFile(filePath);

            // 分析音符分布
            var noteDistribution = MidiAnalyzer.AnalyzeNoteDistribution(midiFile);
            Console.WriteLine("音符分布（前10个最常用的音符）:");
            
            var topNotes = noteDistribution
                .OrderByDescending(x => x.Value)
                .Take(10);

            foreach (var (noteNumber, count) in topNotes)
            {
                string noteName = GetNoteName(noteNumber);
                Console.WriteLine($"  {noteName}: {count} 次");
            }

            // 分析通道使用情况
            var channelUsage = MidiAnalyzer.AnalyzeChannelUsage(midiFile);
            Console.WriteLine($"\n通道使用情况（共 {channelUsage.Count} 个活跃通道）:");
            
            foreach (var (channel, usage) in channelUsage.OrderBy(x => x.Key))
            {
                Console.WriteLine($"  通道 {channel + 1}: {usage.NoteCount} 音符, {usage.TotalEvents} 事件");
                if (usage.ProgramChanges.Count > 0)
                {
                    Console.WriteLine($"    音色: [{string.Join(", ", usage.ProgramChanges)}]");
                }
            }

            // 提取Tempo信息
            var tempoChanges = MidiAnalyzer.ExtractTempoChanges(midiFile);
            if (tempoChanges.Count > 0)
            {
                Console.WriteLine($"\nTempo变化 ({tempoChanges.Count} 次):");
                foreach (var (time, bpm) in tempoChanges.Take(5))
                {
                    Console.WriteLine($"  时间 {time}: {bpm:F1} BPM");
                }
            }
        }

        /// <summary>
        /// 内存效率测试示例
        /// </summary>
        public static void MemoryEfficiencyTest(string filePath)
        {
            Console.WriteLine("内存效率测试开始...");

            // 记录初始内存使用
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long initialMemory = GC.GetTotalMemory(false);

            using var midiFile = MidiFile.LoadFromFile(filePath);
            
            // 记录加载后的内存使用
            long afterLoadMemory = GC.GetTotalMemory(false);
            long loadMemoryUsage = afterLoadMemory - initialMemory;

            Console.WriteLine($"文件加载内存使用: {loadMemoryUsage / 1024.0:F2} KB");

            // 流式处理测试
            int noteCount = 0;
            foreach (var (evt, _, _) in midiFile.GetAllNotesStreamable())
            {
                if (evt.IsNoteOnEvent())
                {
                    noteCount++;
                }
            }

            // 记录处理后的内存使用
            long afterProcessMemory = GC.GetTotalMemory(false);
            long processMemoryUsage = afterProcessMemory - afterLoadMemory;

            Console.WriteLine($"流式处理内存增量: {processMemoryUsage / 1024.0:F2} KB");
            Console.WriteLine($"处理的音符数量: {noteCount}");
            Console.WriteLine($"每个音符平均内存: {(double)loadMemoryUsage / noteCount:F3} 字节");
        }

        /// <summary>
        /// 性能基准测试示例
        /// </summary>
        public static void PerformanceBenchmark(string filePath)
        {
            Console.WriteLine("性能基准测试开始...");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 测试文件加载速度
            using var midiFile = MidiFile.LoadFromFile(filePath);
            stopwatch.Stop();

            var stats = midiFile.GetStatistics();
            double loadTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            
            Console.WriteLine($"文件加载时间: {loadTimeMs:F2} ms");
            Console.WriteLine($"加载速度: {stats.FileSizeBytes / loadTimeMs:F2} KB/ms");

            // 测试事件解析速度
            stopwatch.Restart();
            int eventCount = 0;
            
            foreach (var track in midiFile.Tracks)
            {
                foreach (var evt in track.GetEventEnumerator())
                {
                    eventCount++;
                }
            }
            
            stopwatch.Stop();
            double parseTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            
            Console.WriteLine($"事件解析时间: {parseTimeMs:F2} ms");
            Console.WriteLine($"解析速度: {eventCount / parseTimeMs:F0} 事件/ms");

            // 测试音符提取速度
            stopwatch.Restart();
            var noteInfo = MidiAnalyzer.ExtractNoteInformation(midiFile);
            stopwatch.Stop();
            
            double extractTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            Console.WriteLine($"音符提取时间: {extractTimeMs:F2} ms");
            Console.WriteLine($"提取的音符数量: {noteInfo.Count}");
            Console.WriteLine($"音符提取速度: {noteInfo.Count / extractTimeMs:F0} 音符/ms");
        }

        private static string GetNoteName(int noteNumber)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (noteNumber / 12) - 1;
            string noteName = noteNames[noteNumber % 12];
            return $"{noteName}{octave}";
        }
    }

    /// <summary>
    /// MIDI文件验证工具
    /// </summary>
    public static class MidiValidator
    {
        /// <summary>
        /// 验证MIDI文件的完整性
        /// </summary>
        public static ValidationResult ValidateMidiFile(string filePath)
        {
            var result = new ValidationResult();

            try
            {
                if (!File.Exists(filePath))
                {
                    result.AddError("文件不存在");
                    return result;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    result.AddError("文件为空");
                    return result;
                }

                using var midiFile = MidiFile.LoadFromFile(filePath);
                var stats = midiFile.GetStatistics();

                // 基本验证
                if (stats.TrackCount == 0)
                {
                    result.AddWarning("文件中没有轨道");
                }

                if (stats.TotalEvents == 0)
                {
                    result.AddWarning("文件中没有MIDI事件");
                }

                // 检查每个轨道
                for (int i = 0; i < midiFile.Tracks.Count; i++)
                {
                    var track = midiFile.Tracks[i];
                    ValidateTrack(track, i, result);
                }

                result.IsValid = result.Errors.Count == 0;
                result.Summary = $"验证完成: {stats.TrackCount} 轨道, {stats.TotalEvents} 事件, {stats.TotalNotes} 音符";
            }
            catch (Exception ex)
            {
                result.AddError($"验证过程中发生错误: {ex.Message}");
            }

            return result;
        }

        private static void ValidateTrack(MidiTrack track, int trackIndex, ValidationResult result)
        {
            try
            {
                bool hasEndOfTrack = false;
                int eventCount = 0;

                foreach (var evt in track.GetEventEnumerator())
                {
                    eventCount++;

                    if (evt.IsMetaEvent && evt.MetaEventType == MetaEventType.EndOfTrack)
                    {
                        hasEndOfTrack = true;
                    }

                    // 验证音符范围
                    if (evt.IsChannelEvent && (evt.EventType == MidiEventType.NoteOn || evt.EventType == MidiEventType.NoteOff))
                    {
                        if (evt.Data1 > 127)
                        {
                            result.AddError($"轨道 {trackIndex}: 无效的音符号 {evt.Data1}");
                        }
                        if (evt.Data2 > 127)
                        {
                            result.AddError($"轨道 {trackIndex}: 无效的力度值 {evt.Data2}");
                        }
                    }
                }

                if (!hasEndOfTrack)
                {
                    result.AddWarning($"轨道 {trackIndex}: 缺少 End of Track 事件");
                }

                if (eventCount == 0)
                {
                    result.AddWarning($"轨道 {trackIndex}: 轨道为空");
                }
            }
            catch (Exception ex)
            {
                result.AddError($"轨道 {trackIndex}: 解析错误 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; } = false;
        public string Summary { get; set; } = string.Empty;
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);

        public override string ToString()
        {
            var result = $"验证结果: {(IsValid ? "通过" : "失败")}\n";
            result += $"{Summary}\n";
            
            if (Errors.Count > 0)
            {
                result += $"\n错误 ({Errors.Count}):\n";
                foreach (var error in Errors)
                {
                    result += $"  - {error}\n";
                }
            }

            if (Warnings.Count > 0)
            {
                result += $"\n警告 ({Warnings.Count}):\n";
                foreach (var warning in Warnings)
                {
                    result += $"  - {warning}\n";
                }
            }

            return result;
        }
    }
}