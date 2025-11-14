using System;
using System.Collections.Generic;
using System.Text;

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
        /// 时间基准 (ticks per quarter note 或 SMPTE)
        /// </summary>
        public readonly ushort TimeDivision;

        public MidiFileHeader(MidiFileFormat format, ushort trackCount, ushort timeDivision)
        {
            Format = format;
            TrackCount = trackCount;
            TimeDivision = timeDivision;
        }

        /// <summary>
        /// 检查时间基准是否为SMPTE格式
        /// </summary>
        public bool IsSmpteFormat => (TimeDivision & 0x8000) != 0;

        /// <summary>
        /// 获取每四分音符的tick数 (仅在非SMPTE格式时有效)
        /// </summary>
        public int TicksPerQuarterNote => IsSmpteFormat ? 0 : TimeDivision;

        /// <summary>
        /// 获取SMPTE帧率 (仅在SMPTE格式时有效)
        /// </summary>
        public int SmpteFrameRate => IsSmpteFormat ? -(TimeDivision >> 8) : 0;

        /// <summary>
        /// 获取每帧的tick数 (仅在SMPTE格式时有效)
        /// </summary>
        public int TicksPerFrame => IsSmpteFormat ? (TimeDivision & 0xFF) : 0;
    }

    /// <summary>
    /// ������MIDI�����֧�������غ���ʽ����
    /// </summary>
    public class MidiTrack
    {
        private static readonly Encoding UTF8Encoding = Encoding.UTF8;
        private readonly ReadOnlyMemory<byte> _trackData;

        /// <summary>
        /// ������� (����еĻ�)
        /// </summary>
        public string? Name { get; private set; }

        /// <summary>
        /// �����ԭʼ���������ݳ���
        /// </summary>
        public int DataLength => _trackData.Length;

        public MidiTrack(ReadOnlyMemory<byte> trackData)
        {
            _trackData = trackData;
            ExtractTrackName();
        }

        /// <summary>
        /// 获取轨道中的所有事件 (延迟加载)
        /// </summary>
        public IReadOnlyList<MidiEvent> Events => new LazyMidiEventList(_trackData);

        /// <summary>
        /// �����¼�ö������֧����ʽ�����Խ�ʡ�ڴ�
        /// </summary>
        public MidiEventEnumerator GetEventEnumerator()
        {
            // Pass ReadOnlyMemory directly to avoid intermediate ToArray copy
            return new MidiEventEnumerator(_trackData);
        }

        private void ExtractTrackName()
        {
            // 从当前轨道Meta事件中提取音轨名称
            var parser = new MidiEventParser(_trackData);
            
            // 检查数据有效性
            if (_trackData.Length < 4)
                return;
            
            for (int i = 0; i < 10 && !parser.IsAtEnd; i++) // 只查前10个事件
            {
                try
                {
                    var evt = parser.ParseNextEvent();
                    if (evt.IsMetaEvent && evt.MetaEventType == MetaEventType.TrackName)
                    {
                        if (evt.AdditionalData.Length > 0)
                        {
                            Name = UTF8Encoding.GetString(evt.AdditionalData.Span);
                        }
                        return;
                    }
                }
                catch
                {
                    // 如果解析失败，停止查找
                    break;
                }
            }
        }

        /// <summary>
        /// ��ȡ�����ͳ����Ϣ������ȫ���������¼�
        /// </summary>
        public MidiTrackStatistics GetStatistics()
        {
            var parser = new MidiEventParser(_trackData);
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
                // �������ʧ�ܣ����ز���ͳ����Ϣ
            }

            return new MidiTrackStatistics(noteCount, eventCount, totalTicks, channels.Count);
        }

        /// <summary>
        /// 延迟加载的MIDI事件列表，实现内存优化
        /// </summary>
        private class LazyMidiEventList : IReadOnlyList<MidiEvent>
        {
            private readonly ReadOnlyMemory<byte> _data;
            private List<MidiEvent>? _events;

            public LazyMidiEventList(ReadOnlyMemory<byte> data) => _data = data;

            private List<MidiEvent> GetEvents() => new MidiEventParser(_data).ParseAllEvents();

            public int Count
            {
                get
                {
                    if (_events == null)
                    {
                        _events = GetEvents();
                    }
                    return _events.Count;
                }
            }

            public MidiEvent this[int index]
            {
                get
                {
                    if (_events == null)
                    {
                        _events = GetEvents();
                    }
                    return _events[index];
                }
            }

            public IEnumerator<MidiEvent> GetEnumerator()
            {
                if (_events != null)
                {
                    return _events.GetEnumerator();
                }
                _events = GetEvents();
                return _events.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

    /// <summary>
    /// ���ͳ����Ϣ
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
    /// �������¼�ö������֧����ʽ����
    /// ��Ϊ����struct��֧���ڵ�����������ʹ��
    /// </summary>
    /// <summary>
    /// 为遍历事件提供高性能的枚举器，支持流式解析
    /// 作为值类型struct，支持在栈上直接使用
    /// 优化: 避免重复构造 MidiEventParser，复用内部状态
    /// </summary>
    public struct MidiEventEnumerator
    {
        private readonly ReadOnlyMemory<byte> _data;
        private int _position;

        public MidiEventEnumerator(ReadOnlyMemory<byte> data)
        {
            // Keep the original memory reference to avoid copying
            _data = data;
            _position = 0;
            Current = default;
        }

        public MidiEvent Current { get; private set; }

        public bool MoveNext()
        {
            if (_position >= _data.Length)
                return false;

            try
            {
                var parser = new MidiEventParser(_data.Slice(_position));
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