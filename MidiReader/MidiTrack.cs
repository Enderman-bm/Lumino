using System;
using System.Collections.Generic;

namespace MidiReader
{
    /// <summary>
    /// MIDI文件头信息
    /// </summary>
    public readonly struct MidiFileHeader
    {
        /// <summary>
        /// 文件格式类型 (0, 1, 或 2)
        /// </summary>
        public readonly MidiFileFormat Format;

        /// <summary>
        /// 轨道数量
        /// </summary>
        public readonly ushort TrackCount;

        /// <summary>
        /// 时间分辨率 (ticks per quarter note 或 SMPTE)
        /// </summary>
        public readonly ushort TimeDivision;

        public MidiFileHeader(MidiFileFormat format, ushort trackCount, ushort timeDivision)
        {
            Format = format;
            TrackCount = trackCount;
            TimeDivision = timeDivision;
        }

        /// <summary>
        /// 检查时间分辨率是否为SMPTE格式
        /// </summary>
        public bool IsSmpteFormat => (TimeDivision & 0x8000) != 0;

        /// <summary>
        /// 获取每四分音符的时钟数 (仅当非SMPTE格式时有效)
        /// </summary>
        public int TicksPerQuarterNote => IsSmpteFormat ? 0 : TimeDivision;

        /// <summary>
        /// 获取SMPTE帧率 (仅当SMPTE格式时有效)
        /// </summary>
        public int SmpteFrameRate => IsSmpteFormat ? -(TimeDivision >> 8) : 0;

        /// <summary>
        /// 获取每帧的时钟数 (仅当SMPTE格式时有效)
        /// </summary>
        public int TicksPerFrame => IsSmpteFormat ? (TimeDivision & 0xFF) : 0;
    }

    /// <summary>
    /// 高性能MIDI轨道，支持懒加载和流式访问
    /// </summary>
    public class MidiTrack
    {
        private readonly ReadOnlyMemory<byte> _trackData;
        private List<MidiEvent>? _cachedEvents;

        /// <summary>
        /// 轨道名称 (如果有的话)
        /// </summary>
        public string? Name { get; private set; }

        /// <summary>
        /// 轨道的原始二进制数据长度
        /// </summary>
        public int DataLength => _trackData.Length;

        public MidiTrack(ReadOnlyMemory<byte> trackData)
        {
            _trackData = trackData;
            ExtractTrackName();
        }

        /// <summary>
        /// 获取轨道中的所有事件 (懒加载)
        /// </summary>
        public IReadOnlyList<MidiEvent> Events
        {
            get
            {
                if (_cachedEvents == null)
                {
                    ParseEvents();
                }
                return _cachedEvents!;
            }
        }

        /// <summary>
        /// 创建事件枚举器，支持流式访问以节省内存
        /// </summary>
        public MidiEventEnumerator GetEventEnumerator()
        {
            return new MidiEventEnumerator(_trackData.Span);
        }

        private void ParseEvents()
        {
            var parser = new MidiEventParser(_trackData.Span);
            _cachedEvents = parser.ParseAllEvents();
        }

        private void ExtractTrackName()
        {
            // 尝试从前几个Meta事件中提取轨道名称
            var parser = new MidiEventParser(_trackData.Span);
            
            try
            {
                for (int i = 0; i < 10 && !parser.IsAtEnd; i++) // 只检查前10个事件
                {
                    var evt = parser.ParseNextEvent();
                    if (evt.IsMetaEvent && evt.MetaEventType == MetaEventType.TrackName)
                    {
                        Name = System.Text.Encoding.UTF8.GetString(evt.AdditionalData.Span);
                        break;
                    }
                }
            }
            catch
            {
                // 忽略解析错误，轨道名称不是必需的
            }
        }

        /// <summary>
        /// 获取轨道的统计信息而不完全解析所有事件
        /// </summary>
        public MidiTrackStatistics GetStatistics()
        {
            var parser = new MidiEventParser(_trackData.Span);
            int noteCount = 0;
            int eventCount = 0;
            uint totalTicks = 0;
            var channels = new HashSet<byte>();

            try
            {
                while (!parser.IsAtEnd)
                {
                    var evt = parser.ParseNextEvent();
                    eventCount++;
                    totalTicks += evt.DeltaTime;

                    if (evt.EventType == MidiEventType.NoteOn && evt.Data2 > 0)
                    {
                        noteCount++;
                        channels.Add(evt.Channel);
                    }
                }
            }
            catch
            {
                // 如果解析失败，返回部分统计信息
            }

            return new MidiTrackStatistics(noteCount, eventCount, totalTicks, channels.Count);
        }
    }

    /// <summary>
    /// 轨道统计信息
    /// </summary>
    public readonly struct MidiTrackStatistics
    {
        public readonly int NoteCount;
        public readonly int EventCount;
        public readonly uint TotalTicks;
        public readonly int UsedChannels;

        public MidiTrackStatistics(int noteCount, int eventCount, uint totalTicks, int usedChannels)
        {
            NoteCount = noteCount;
            EventCount = eventCount;
            TotalTicks = totalTicks;
            UsedChannels = usedChannels;
        }
    }

    /// <summary>
    /// 高性能事件枚举器，支持流式访问
    /// 改为常规struct以支持在迭代器方法中使用
    /// </summary>
    public struct MidiEventEnumerator
    {
        private readonly ReadOnlyMemory<byte> _data;
        private int _position;
        private byte _runningStatus;

        public MidiEventEnumerator(ReadOnlySpan<byte> data)
        {
            _data = data.ToArray(); // 转换为Memory以避免ref struct限制
            _position = 0;
            _runningStatus = 0;
            Current = default;
        }

        public MidiEvent Current { get; private set; }

        public bool MoveNext()
        {
            if (_position >= _data.Length)
                return false;

            try
            {
                var parser = new MidiEventParser(_data.Span[_position..]);
                Current = parser.ParseNextEvent();
                _position += parser.Position;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public MidiEventEnumerator GetEnumerator() => this;
    }
}