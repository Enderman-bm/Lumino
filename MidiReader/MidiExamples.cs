using System;
using System.IO;
using System.Linq;

namespace MidiReader
{
    /// <summary>
    /// MIDI��ȡ����ʾ���÷���ʵ�ù���
    /// </summary>
    public static class MidiReaderExamples
    {
        /// <summary>
        /// �����÷�ʾ�������ز�����MIDI�ļ�
        /// </summary>
        public static void BasicUsageExample(string filePath)
        {
            // ����MIDI�ļ�
            using var midiFile = MidiFile.LoadFromFile(filePath);

            // ��ȡ������Ϣ
            var stats = midiFile.GetStatistics();
            Console.WriteLine($"�ļ���ʽ: {stats.Header.Format}");
            Console.WriteLine($"�������: {stats.TrackCount}");
            Console.WriteLine($"��������: {stats.TotalNotes}");
            Console.WriteLine($"���¼���: {stats.TotalEvents}");
            Console.WriteLine($"�ļ���С: {stats.FileSizeBytes / 1024.0:F2} KB");

            // ����ÿ�����
            for (int i = 0; i < midiFile.Tracks.Count; i++)
            {
                var track = midiFile.Tracks[i];
                var trackStats = track.GetStatistics();
                Console.WriteLine($"��� {i + 1}: {track.Name ?? "������"} - {trackStats.NoteCount} ����, {trackStats.EventCount} �¼�");
            }
        }

        /// <summary>
        /// ��������ʽ����ʾ������������MIDI�ļ�
        /// </summary>
        public static void StreamingProcessingExample(string filePath)
        {
            using var midiFile = MidiFile.LoadFromFile(filePath);

            Console.WriteLine("��ʼ��ʽ����...");
            int processedNotes = 0;

            // ʹ����ʽ�������⽫�����¼����ص��ڴ�
            foreach (var (evt, trackIndex, absoluteTime) in midiFile.GetAllNotesParallel())
            {
                if (evt.IsNoteOnEvent())
                {
                    processedNotes++;
                    
                    // ����ÿ�������¼�
                    string noteName = evt.GetNoteName();
                    double frequency = evt.GetNoteFrequency();
                    
                    // ÿ10000����������һ�ν���
                    if (processedNotes % 10000 == 0)
                    {
                        Console.WriteLine($"�Ѵ��� {processedNotes} ������...");
                    }
                }
            }

            Console.WriteLine($"��ʽ������ɣ��ܹ������� {processedNotes} ������");
        }

        /// <summary>
        /// ��������ʾ��
        /// </summary>
        public static void NoteAnalysisExample(string filePath)
        {
            using var midiFile = MidiFile.LoadFromFile(filePath);

            // ���������ֲ�
            var noteDistribution = MidiAnalyzer.AnalyzeNoteDistribution(midiFile);
            Console.WriteLine("�����ֲ���ǰ10����õ�������:");
            
            var topNotes = noteDistribution
                .OrderByDescending(x => x.Value)
                .Take(10);

            foreach (var (noteNumber, count) in topNotes)
            {
                string noteName = GetNoteName(noteNumber);
                Console.WriteLine($"  {noteName}: {count} ��");
            }

            // ����ͨ��ʹ�����
            var channelUsage = MidiAnalyzer.AnalyzeChannelUsage(midiFile);
            Console.WriteLine($"\nͨ��ʹ��������� {channelUsage.Count} ����Ծͨ����:");
            
            foreach (var (channel, usage) in channelUsage.OrderBy(x => x.Key))
            {
                Console.WriteLine($"  ͨ�� {channel + 1}: {usage.NoteCount} ����, {usage.TotalEvents} �¼�");
                if (usage.ProgramChanges.Count > 0)
                {
                    Console.WriteLine($"    ��ɫ: [{string.Join(", ", usage.ProgramChanges)}]");
                }
            }

            // ��ȡTempo��Ϣ
            var tempoChanges = MidiAnalyzer.ExtractTempoChanges(midiFile);
            if (tempoChanges.Count > 0)
            {
                Console.WriteLine($"\nTempo�仯 ({tempoChanges.Count} ��):");
                foreach (var (time, bpm) in tempoChanges.Take(5))
                {
                    Console.WriteLine($"  ʱ�� {time}: {bpm:F1} BPM");
                }
            }
        }

        /// <summary>
        /// �ڴ�Ч�ʲ���ʾ��
        /// </summary>
        public static void MemoryEfficiencyTest(string filePath)
        {
            Console.WriteLine("�ڴ�Ч�ʲ��Կ�ʼ...");

            // ��¼��ʼ�ڴ�ʹ��
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long initialMemory = GC.GetTotalMemory(false);

            using var midiFile = MidiFile.LoadFromFile(filePath);
            
            // ��¼���غ���ڴ�ʹ��
            long afterLoadMemory = GC.GetTotalMemory(false);
            long loadMemoryUsage = afterLoadMemory - initialMemory;

            Console.WriteLine($"�ļ������ڴ�ʹ��: {loadMemoryUsage / 1024.0:F2} KB");

            // ��ʽ��������
            int noteCount = 0;
            foreach (var (evt, _, _) in midiFile.GetAllNotesParallel())
            {
                if (evt.IsNoteOnEvent())
                {
                    noteCount++;
                }
            }

            // ��¼��������ڴ�ʹ��
            long afterProcessMemory = GC.GetTotalMemory(false);
            long processMemoryUsage = afterProcessMemory - afterLoadMemory;

            Console.WriteLine($"��ʽ�����ڴ�����: {processMemoryUsage / 1024.0:F2} KB");
            Console.WriteLine($"��������������: {noteCount}");
            Console.WriteLine($"ÿ������ƽ���ڴ�: {(double)loadMemoryUsage / noteCount:F3} �ֽ�");
        }

        /// <summary>
        /// ���ܻ�׼����ʾ��
        /// </summary>
        public static void PerformanceBenchmark(string filePath)
        {
            Console.WriteLine("���ܻ�׼���Կ�ʼ...");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // �����ļ������ٶ�
            using var midiFile = MidiFile.LoadFromFile(filePath);
            stopwatch.Stop();

            var stats = midiFile.GetStatistics();
            double loadTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            
            Console.WriteLine($"�ļ�����ʱ��: {loadTimeMs:F2} ms");
            Console.WriteLine($"�����ٶ�: {stats.FileSizeBytes / loadTimeMs:F2} KB/ms");

            // �����¼������ٶ�
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
            
            Console.WriteLine($"�¼�����ʱ��: {parseTimeMs:F2} ms");
            Console.WriteLine($"�����ٶ�: {eventCount / parseTimeMs:F0} �¼�/ms");

            // ����������ȡ�ٶ�
            stopwatch.Restart();
            var noteInfo = MidiAnalyzer.ExtractNoteInformation(midiFile);
            stopwatch.Stop();
            
            double extractTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            Console.WriteLine($"������ȡʱ��: {extractTimeMs:F2} ms");
            Console.WriteLine($"��ȡ����������: {noteInfo.Count}");
            Console.WriteLine($"������ȡ�ٶ�: {noteInfo.Count / extractTimeMs:F0} ����/ms");
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
    /// MIDI�ļ���֤����
    /// </summary>
    public static class MidiValidator
    {
        /// <summary>
        /// ��֤MIDI�ļ���������
        /// </summary>
        public static ValidationResult ValidateMidiFile(string filePath)
        {
            var result = new ValidationResult();

            try
            {
                if (!File.Exists(filePath))
                {
                    result.AddError("�ļ�������");
                    return result;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    result.AddError("�ļ�Ϊ��");
                    return result;
                }

                using var midiFile = MidiFile.LoadFromFile(filePath);
                var stats = midiFile.GetStatistics();

                // ������֤
                if (stats.TrackCount == 0)
                {
                    result.AddWarning("�ļ���û�й��");
                }

                if (stats.TotalEvents == 0)
                {
                    result.AddWarning("�ļ���û��MIDI�¼�");
                }

                // ���ÿ�����
                for (int i = 0; i < midiFile.Tracks.Count; i++)
                {
                    var track = midiFile.Tracks[i];
                    ValidateTrack(track, i, result);
                }

                result.IsValid = result.Errors.Count == 0;
                result.Summary = $"��֤���: {stats.TrackCount} ���, {stats.TotalEvents} �¼�, {stats.TotalNotes} ����";
            }
            catch (Exception ex)
            {
                result.AddError($"��֤�����з�������: {ex.Message}");
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

                    // ��֤������Χ
                    if (evt.IsChannelEvent && (evt.EventType == MidiEventType.NoteOn || evt.EventType == MidiEventType.NoteOff))
                    {
                        if (evt.Data1 > 127)
                        {
                            result.AddError($"��� {trackIndex}: ��Ч�������� {evt.Data1}");
                        }
                        if (evt.Data2 > 127)
                        {
                            result.AddError($"��� {trackIndex}: ��Ч������ֵ {evt.Data2}");
                        }
                    }
                }

                if (!hasEndOfTrack)
                {
                    result.AddWarning($"��� {trackIndex}: ȱ�� End of Track �¼�");
                }

                if (eventCount == 0)
                {
                    result.AddWarning($"��� {trackIndex}: ���Ϊ��");
                }
            }
            catch (Exception ex)
            {
                result.AddError($"��� {trackIndex}: �������� - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ��֤���
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
            var result = $"��֤���: {(IsValid ? "ͨ��" : "ʧ��")}\n";
            result += $"{Summary}\n";
            
            if (Errors.Count > 0)
            {
                result += $"\n���� ({Errors.Count}):\n";
                foreach (var error in Errors)
                {
                    result += $"  - {error}\n";
                }
            }

            if (Warnings.Count > 0)
            {
                result += $"\n���� ({Warnings.Count}):\n";
                foreach (var warning in Warnings)
                {
                    result += $"  - {warning}\n";
                }
            }

            return result;
        }
    }
}