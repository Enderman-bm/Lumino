using System;
using System.Collections.Generic;

namespace MidiReader
{
    /// <summary>
    /// ������MIDI�¼�������
    /// </summary>
    public ref struct MidiEventParser
    {
        private MidiBinaryReader _reader;
        private byte _runningStatus;
        private readonly ReadOnlyMemory<byte> _memory;

        public MidiEventParser(ReadOnlyMemory<byte> memory)
        {
            _memory = memory;
            _reader = new MidiBinaryReader(memory.Span);
            _runningStatus = 0;
        }

        /// <summary>
        /// ������һ��MIDI�¼�
        /// </summary>
        public MidiEvent ParseNextEvent()
        {
            if (_reader.IsAtEnd)
                throw new InvalidOperationException("No more events to parse");

            // ��ȡdelta time
            uint deltaTime = _reader.ReadVariableLengthQuantity();

            // ��ȡ״̬�ֽ�
            byte statusByte = _reader.PeekByte();
            
            // Running Status handling
            if ((statusByte & 0x80) == 0)
            {
                // data byte encountered, use running status if available
                if (_runningStatus == 0)
                    throw new InvalidOperationException("Data byte without running status");
                statusByte = _runningStatus;
            }
            else
            {
                // status byte encountered, consume it
                _reader.ReadByte(); // consume status byte

                // If this is a system message (>= 0xF0) running status must be cleared
                if ((statusByte & 0xF0) == 0xF0)
                {
                    _runningStatus = 0;
                }
                else
                {
                    // update running status for channel voice messages
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
            int start = _reader.Position;
            var span = _reader.ReadBytes((int)length);

            // If last byte is F7 (end marker), exclude it from returned data
            int actualLength = (int)length;
            if (actualLength > 0 && span[actualLength - 1] == 0xF7)
            {
                actualLength--;
            }

            var mem = _memory.Slice(start, actualLength);
            return new MidiEvent(deltaTime, MidiEventType.SystemExclusive, 0, 0, 0, mem);
        }

        private MidiEvent ParseEndOfSystemExclusive(uint deltaTime)
        {
            uint length = _reader.ReadVariableLengthQuantity();
            int start = _reader.Position;
            var span = _reader.ReadBytes((int)length);
            var mem = _memory.Slice(start, (int)length);
            return new MidiEvent(deltaTime, MidiEventType.EndOfSystemExclusive, 0, 0, 0, mem);
        }

        private MidiEvent ParseMetaEvent(uint deltaTime)
        {
            byte metaType = _reader.ReadByte();
            uint length = _reader.ReadVariableLengthQuantity();

            ReadOnlyMemory<byte> data = default;
            if (length > 0)
            {
                int start = _reader.Position;
                var span = _reader.ReadBytes((int)length);
                data = _memory.Slice(start, (int)length);
            }

            return new MidiEvent(deltaTime, MidiEventType.MetaEvent, 0, metaType, 0, data);
        }

        /// <summary>
        /// ��������е������¼�
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
        /// ��ǰ����λ��
        /// </summary>
        public int Position => _reader.Position;

        /// <summary>
        /// �Ƿ��ѽ�������������
        /// </summary>
        public bool IsAtEnd => _reader.IsAtEnd;
    }
}