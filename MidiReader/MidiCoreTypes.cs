using System;

namespace MidiReader
{
    /// <summary>
    /// MIDI事件类型枚举
    /// </summary>
    public enum MidiEventType : byte
    {
        // Channel Events (0x80-0xEF)
        NoteOff = 0x80,
        NoteOn = 0x90,
        PolyphonicKeyPressure = 0xA0,
        ControlChange = 0xB0,
        ProgramChange = 0xC0,
        ChannelPressure = 0xD0,
        PitchBendChange = 0xE0,

        // System Events (0xF0-0xFF)
        SystemExclusive = 0xF0,
        MidiTimeCodeQuarterFrame = 0xF1,
        SongPositionPointer = 0xF2,
        SongSelect = 0xF3,
        TuneRequest = 0xF6,
        EndOfSystemExclusive = 0xF7,
        TimingClock = 0xF8,
        Start = 0xFA,
        Continue = 0xFB,
        Stop = 0xFC,
        ActiveSensing = 0xFE,
        
        // Meta Events (只在MIDI文件中出现)
        MetaEvent = 0xFF
    }

    /// <summary>
    /// Meta事件类型
    /// </summary>
    public enum MetaEventType : byte
    {
        SequenceNumber = 0x00,
        TextEvent = 0x01,
        CopyrightNotice = 0x02,
        TrackName = 0x03,
        InstrumentName = 0x04,
        Lyric = 0x05,
        Marker = 0x06,
        CuePoint = 0x07,
        ChannelPrefix = 0x20,
        EndOfTrack = 0x2F,
        SetTempo = 0x51,
        SmpteOffset = 0x54,
        TimeSignature = 0x58,
        KeySignature = 0x59,
        SequencerSpecific = 0x7F
    }

    /// <summary>
    /// MIDI文件格式类型
    /// </summary>
    public enum MidiFileFormat : ushort
    {
        SingleTrack = 0,
        MultipleTracksParallel = 1,
        MultipleTracksSequential = 2
    }

    /// <summary>
    /// 高性能MIDI事件结构体，使用最小内存布局
    /// </summary>
    public readonly struct MidiEvent
    {
        public readonly uint DeltaTime;
        public readonly MidiEventType EventType;
        public readonly byte Channel;
        public readonly byte Data1;
        public readonly byte Data2;
        public readonly ReadOnlyMemory<byte> AdditionalData; // 用于SysEx和Meta事件的额外数据

        public MidiEvent(uint deltaTime, MidiEventType eventType, byte channel = 0, byte data1 = 0, byte data2 = 0, ReadOnlyMemory<byte> additionalData = default)
        {
            DeltaTime = deltaTime;
            EventType = eventType;
            Channel = channel;
            Data1 = data1;
            Data2 = data2;
            AdditionalData = additionalData;
        }

        /// <summary>
        /// 检查是否为通道事件
        /// </summary>
        public bool IsChannelEvent => (byte)EventType >= 0x80 && (byte)EventType <= 0xEF;

        /// <summary>
        /// 检查是否为系统事件
        /// </summary>
        public bool IsSystemEvent => (byte)EventType >= 0xF0 && (byte)EventType <= 0xFE;

        /// <summary>
        /// 检查是否为Meta事件
        /// </summary>
        public bool IsMetaEvent => EventType == MidiEventType.MetaEvent;

        /// <summary>
        /// 获取Meta事件类型（仅当IsMetaEvent为true时有效）
        /// </summary>
        public MetaEventType MetaEventType => (MetaEventType)Data1;
    }
}
