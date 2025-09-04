using System;
using System.Collections.Generic;

namespace MidiReader
{
    /// <summary>
    /// 高性能MIDI事件解析器
    /// </summary>
    public ref struct MidiEventParser
    {
        private MidiBinaryReader _reader;
        private byte _runningStatus;

        public MidiEventParser(ReadOnlySpan<byte> data)
        {
            _reader = new MidiBinaryReader(data);
            _runningStatus = 0;
        }

        /// <summary>
        /// 解析下一个MIDI事件
        /// </summary>
        public MidiEvent ParseNextEvent()
        {
            if (_reader.IsAtEnd)
                throw new InvalidOperationException("No more events to parse");

            // 读取delta time
            uint deltaTime = _reader.ReadVariableLengthQuantity();

            // 读取状态字节
            byte statusByte = _reader.PeekByte();
            
            // 处理Running Status
            if ((statusByte & 0x80) == 0)
            {
                // 这是一个数据字节，使用running status
                if (_runningStatus == 0)
                    throw new InvalidOperationException("Data byte without running status");
                statusByte = _runningStatus;
            }
            else
            {
                // 这是一个新的状态字节
                _reader.ReadByte(); // 消费状态字节
                if (statusByte != 0xF0 && statusByte != 0xF7 && statusByte != 0xFF)
                {
                    _runningStatus = statusByte;
                }
            }

            return ParseEventByStatus(deltaTime, statusByte);
        }

        private MidiEvent ParseEventByStatus(uint deltaTime, byte statusByte)
        {
            var eventType = (MidiEventType)(statusByte & 0xF0);
            byte channel = (byte)(statusByte & 0x0F);

            return eventType switch
            {
                MidiEventType.NoteOff => ParseChannelEvent(deltaTime, eventType, channel, 2),
                MidiEventType.NoteOn => ParseChannelEvent(deltaTime, eventType, channel, 2),
                MidiEventType.PolyphonicKeyPressure => ParseChannelEvent(deltaTime, eventType, channel, 2),
                MidiEventType.ControlChange => ParseChannelEvent(deltaTime, eventType, channel, 2),
                MidiEventType.ProgramChange => ParseChannelEvent(deltaTime, eventType, channel, 1),
                MidiEventType.ChannelPressure => ParseChannelEvent(deltaTime, eventType, channel, 1),
                MidiEventType.PitchBendChange => ParseChannelEvent(deltaTime, eventType, channel, 2),
                _ => ParseSystemEvent(deltaTime, statusByte)
            };
        }

        private MidiEvent ParseChannelEvent(uint deltaTime, MidiEventType eventType, byte channel, int dataByteCount)
        {
            byte data1 = dataByteCount > 0 ? _reader.ReadByte() : (byte)0;
            byte data2 = dataByteCount > 1 ? _reader.ReadByte() : (byte)0;

            return new MidiEvent(deltaTime, eventType, channel, data1, data2);
        }

        private MidiEvent ParseSystemEvent(uint deltaTime, byte statusByte)
        {
            return (MidiEventType)statusByte switch
            {
                MidiEventType.SystemExclusive => ParseSystemExclusive(deltaTime),
                MidiEventType.MidiTimeCodeQuarterFrame => new MidiEvent(deltaTime, MidiEventType.MidiTimeCodeQuarterFrame, 0, _reader.ReadByte()),
                MidiEventType.SongPositionPointer => new MidiEvent(deltaTime, MidiEventType.SongPositionPointer, 0, _reader.ReadByte(), _reader.ReadByte()),
                MidiEventType.SongSelect => new MidiEvent(deltaTime, MidiEventType.SongSelect, 0, _reader.ReadByte()),
                MidiEventType.TuneRequest => new MidiEvent(deltaTime, MidiEventType.TuneRequest),
                MidiEventType.EndOfSystemExclusive => ParseEndOfSystemExclusive(deltaTime),
                MidiEventType.TimingClock => new MidiEvent(deltaTime, MidiEventType.TimingClock),
                MidiEventType.Start => new MidiEvent(deltaTime, MidiEventType.Start),
                MidiEventType.Continue => new MidiEvent(deltaTime, MidiEventType.Continue),
                MidiEventType.Stop => new MidiEvent(deltaTime, MidiEventType.Stop),
                MidiEventType.ActiveSensing => new MidiEvent(deltaTime, MidiEventType.ActiveSensing),
                MidiEventType.MetaEvent => ParseMetaEvent(deltaTime),
                _ => throw new InvalidOperationException($"Unknown system event: 0x{statusByte:X2}")
            };
        }

        private MidiEvent ParseSystemExclusive(uint deltaTime)
        {
            uint length = _reader.ReadVariableLengthQuantity();
            var data = _reader.ReadBytes((int)length);
            
            // 检查是否以F7结尾
            if (data.Length > 0 && data[^1] == 0xF7)
            {
                // 移除结尾的F7
                data = data[..^1];
            }

            return new MidiEvent(deltaTime, MidiEventType.SystemExclusive, 0, 0, 0, data.ToArray());
        }

        private MidiEvent ParseEndOfSystemExclusive(uint deltaTime)
        {
            uint length = _reader.ReadVariableLengthQuantity();
            var data = _reader.ReadBytes((int)length);
            
            return new MidiEvent(deltaTime, MidiEventType.EndOfSystemExclusive, 0, 0, 0, data.ToArray());
        }

        private MidiEvent ParseMetaEvent(uint deltaTime)
        {
            byte metaType = _reader.ReadByte();
            uint length = _reader.ReadVariableLengthQuantity();
            
            ReadOnlyMemory<byte> data = default;
            if (length > 0)
            {
                data = _reader.ReadBytes((int)length).ToArray();
            }

            return new MidiEvent(deltaTime, MidiEventType.MetaEvent, 0, metaType, 0, data);
        }

        /// <summary>
        /// 解析轨道中的所有事件
        /// </summary>
        public List<MidiEvent> ParseAllEvents()
        {
            var events = new List<MidiEvent>();
            
            while (!_reader.IsAtEnd)
            {
                events.Add(ParseNextEvent());
            }
            
            return events;
        }

        /// <summary>
        /// 当前解析位置
        /// </summary>
        public int Position => _reader.Position;

        /// <summary>
        /// 是否已解析完所有数据
        /// </summary>
        public bool IsAtEnd => _reader.IsAtEnd;
    }
}