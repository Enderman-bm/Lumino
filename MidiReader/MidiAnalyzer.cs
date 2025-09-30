using System;
using System.Collections.Generic;
using System.Linq;

namespace MidiReader
{
    /// <summary>
    /// MIDI事件扩展方法
    /// </summary>
    public static class MidiEventExtensions
    {
        /// <summary>
        /// 获取音符号对应的音名
        /// </summary>
        public static string GetNoteName(this MidiEvent evt)
        {
            if (!evt.IsChannelEvent || (evt.EventType != MidiEventType.NoteOn && evt.EventType != MidiEventType.NoteOff))
                return string.Empty;

            int noteNumber = evt.Data1;
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (noteNumber / 12) - 1;
            string noteName = noteNames[noteNumber % 12];
            return $"{noteName}{octave}";
        }

        /// <summary>
        /// 获取音符的频率（Hz）
        /// </summary>
        public static double GetNoteFrequency(this MidiEvent evt)
        {
            if (!evt.IsChannelEvent || (evt.EventType != MidiEventType.NoteOn && evt.EventType != MidiEventType.NoteOff))
                return 0;

            int noteNumber = evt.Data1;
            // A4 (音符号69) = 440Hz
            return 440.0 * Math.Pow(2.0, (noteNumber - 69) / 12.0);
        }

        /// <summary>
        /// 检查是否为有效的音符开始事件
        /// </summary>
        public static bool IsNoteOnEvent(this MidiEvent evt)
        {
            return evt.EventType == MidiEventType.NoteOn && evt.Data2 > 0;
        }

        /// <summary>
        /// 检查是否为音符结束事件（NoteOff 或 velocity=0的NoteOn）
        /// </summary>
        public static bool IsNoteOffEvent(this MidiEvent evt)
        {
            return evt.EventType == MidiEventType.NoteOff || 
                   (evt.EventType == MidiEventType.NoteOn && evt.Data2 == 0);
        }

        /// <summary>
        /// 获取控制器变化事件的控制器名称
        /// </summary>
        public static string GetControllerName(this MidiEvent evt)
        {
            if (evt.EventType != MidiEventType.ControlChange)
                return string.Empty;

            return evt.Data1 switch
            {
                0 => "Bank Select (MSB)",
                1 => "Modulation Wheel",
                2 => "Breath Controller",
                4 => "Foot Controller",
                5 => "Portamento Time",
                6 => "Data Entry (MSB)",
                7 => "Channel Volume",
                8 => "Balance",
                10 => "Pan",
                11 => "Expression Controller",
                32 => "Bank Select (LSB)",
                64 => "Sustain Pedal",
                65 => "Portamento On/Off",
                66 => "Sostenuto Pedal",
                67 => "Soft Pedal",
                68 => "Legato Footswitch",
                69 => "Hold 2",
                120 => "All Sound Off",
                121 => "Reset All Controllers",
                122 => "Local Control",
                123 => "All Notes Off",
                124 => "Omni Mode Off",
                125 => "Omni Mode On",
                126 => "Mono Mode On",
                127 => "Poly Mode On",
                _ => $"Controller {evt.Data1}"
            };
        }

        /// <summary>
        /// 获取Meta事件的文本内容
        /// </summary>
        public static string GetMetaText(this MidiEvent evt)
        {
            if (!evt.IsMetaEvent)
                return string.Empty;

            return evt.MetaEventType switch
            {
                MetaEventType.TextEvent or 
                MetaEventType.CopyrightNotice or 
                MetaEventType.TrackName or 
                MetaEventType.InstrumentName or 
                MetaEventType.Lyric or 
                MetaEventType.Marker or 
                MetaEventType.CuePoint => System.Text.Encoding.UTF8.GetString(evt.AdditionalData.Span),
                _ => string.Empty
            };
        }

        /// <summary>
        /// 获取Tempo事件的BPM值
        /// </summary>
        public static double GetTempoBpm(this MidiEvent evt)
        {
            if (!evt.IsMetaEvent || evt.MetaEventType != MetaEventType.SetTempo || evt.AdditionalData.Length != 3)
                return 0;

            var data = evt.AdditionalData.Span;
            uint microsecondsPerQuarterNote = (uint)((data[0] << 16) | (data[1] << 8) | data[2]);
            return 60_000_000.0 / microsecondsPerQuarterNote;
        }

        /// <summary>
        /// 获取拍号信息
        /// </summary>
        public static (int Numerator, int Denominator, int ClocksPerClick, int ThirtySecondNotesPerQuarter) GetTimeSignature(this MidiEvent evt)
        {
            if (!evt.IsMetaEvent || evt.MetaEventType != MetaEventType.TimeSignature || evt.AdditionalData.Length != 4)
                return (0, 0, 0, 0);

            var data = evt.AdditionalData.Span;
            int numerator = data[0];
            int denominator = 1 << data[1]; // 2^data[1]
            int clocksPerClick = data[2];
            int thirtySecondNotesPerQuarter = data[3];

            return (numerator, denominator, clocksPerClick, thirtySecondNotesPerQuarter);
        }
    }

    /// <summary>
    /// 高性能MIDI分析器
    /// 提供快速的音符和事件分析功能
    /// </summary>
    public static class MidiAnalyzer
    {
        /// <summary>
        /// 分析MIDI文件中的音符分布
        /// </summary>
        public static Dictionary<int, int> AnalyzeNoteDistribution(MidiFile midiFile)
        {
            var distribution = new Dictionary<int, int>();

            foreach (var track in midiFile.Tracks)
            {
                foreach (var evt in track.GetEventEnumerator())
                {
                    if (evt.IsNoteOnEvent())
                    {
                        int noteNumber = evt.Data1;
                        distribution[noteNumber] = distribution.GetValueOrDefault(noteNumber) + 1;
                    }
                }
            }

            return distribution;
        }

        /// <summary>
        /// 分析每个通道的使用情况
        /// </summary>
        public static Dictionary<byte, ChannelUsage> AnalyzeChannelUsage(MidiFile midiFile)
        {
            var channelUsage = new Dictionary<byte, ChannelUsage>();

            foreach (var track in midiFile.Tracks)
            {
                foreach (var evt in track.GetEventEnumerator())
                {
                    if (evt.IsChannelEvent)
                    {
                        if (!channelUsage.ContainsKey(evt.Channel))
                        {
                            channelUsage[evt.Channel] = new ChannelUsage();
                        }

                        var usage = channelUsage[evt.Channel];
                        switch (evt.EventType)
                        {
                            case MidiEventType.NoteOn when evt.Data2 > 0:
                                usage.NoteCount++;
                                break;
                            case MidiEventType.ProgramChange:
                                usage.ProgramChanges.Add(evt.Data1);
                                break;
                            case MidiEventType.ControlChange:
                                usage.ControllerChanges.Add(evt.Data1);
                                break;
                        }
                        usage.TotalEvents++;
                    }
                }
            }

            return channelUsage;
        }

        /// <summary>
        /// 提取所有Tempo变化
        /// </summary>
        public static List<(uint AbsoluteTime, double Bpm)> ExtractTempoChanges(MidiFile midiFile)
        {
            var tempoChanges = new List<(uint, double)>();
            uint absoluteTime = 0;

            foreach (var track in midiFile.Tracks)
            {
                absoluteTime = 0;
                foreach (var evt in track.GetEventEnumerator())
                {
                    absoluteTime += evt.DeltaTime;
                    if (evt.IsMetaEvent && evt.MetaEventType == MetaEventType.SetTempo)
                    {
                        double bpm = evt.GetTempoBpm();
                        tempoChanges.Add((absoluteTime, bpm));
                    }
                }
            }

            return tempoChanges.OrderBy(x => x.Item1).ToList();
        }

        /// <summary>
        /// 计算音符的实际持续时间（需要匹配NoteOn和NoteOff事件）
        /// </summary>
        public static List<NoteInfo> ExtractNoteInformation(MidiFile midiFile)
        {
            var notes = new List<NoteInfo>();
            var activeNotes = new Dictionary<(byte Channel, byte Note), (uint StartTime, byte Velocity)>();

            foreach (var (evt, trackIndex, absoluteTime) in midiFile.GetAllNotesStreamable())
            {
                var key = (evt.Channel, evt.Data1); // Data1 是音符号

                if (evt.IsNoteOnEvent())
                {
                    // 如果同一个音符已经在播放，先结束它
                    if (activeNotes.ContainsKey(key))
                    {
                        var (startTime, velocity) = activeNotes[key];
                        notes.Add(new NoteInfo(key.Channel, key.Data1, startTime, absoluteTime - startTime, velocity, trackIndex));
                    }
                    
                    activeNotes[key] = (absoluteTime, evt.Data2);
                }
                else if (evt.IsNoteOffEvent() && activeNotes.ContainsKey(key))
                {
                    var (startTime, velocity) = activeNotes[key];
                    notes.Add(new NoteInfo(key.Channel, key.Data1, startTime, absoluteTime - startTime, velocity, trackIndex));
                    activeNotes.Remove(key);
                }
            }

            return notes;
        }
    }

    /// <summary>
    /// 通道使用情况统计
    /// </summary>
    public class ChannelUsage
    {
        public int NoteCount { get; set; }
        public int TotalEvents { get; set; }
        public HashSet<byte> ProgramChanges { get; } = new();
        public HashSet<byte> ControllerChanges { get; } = new();
    }

    /// <summary>
    /// 音符信息
    /// </summary>
    public readonly struct NoteInfo
    {
        public readonly byte Channel;
        public readonly byte Note;
        public readonly uint StartTime;
        public readonly uint Duration;
        public readonly byte Velocity;
        public readonly int TrackIndex;

        public NoteInfo(byte channel, byte note, uint startTime, uint duration, byte velocity, int trackIndex)
        {
            Channel = channel;
            Note = note;
            StartTime = startTime;
            Duration = duration;
            Velocity = velocity;
            TrackIndex = trackIndex;
        }

        public string NoteName => GetNoteName(Note);
        public double Frequency => GetNoteFrequency(Note);

        private static string GetNoteName(byte noteNumber)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (noteNumber / 12) - 1;
            string noteName = noteNames[noteNumber % 12];
            return $"{noteName}{octave}";
        }

        private static double GetNoteFrequency(byte noteNumber)
        {
            return 440.0 * Math.Pow(2.0, (noteNumber - 69) / 12.0);
        }
    }
}