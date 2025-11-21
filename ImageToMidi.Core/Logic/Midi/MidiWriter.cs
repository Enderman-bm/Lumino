using System;
using System.IO;

namespace ImageToMidi.Logic.Midi
{
    public class MidiWriter
    {
        private readonly Stream _stream;
        private readonly BinaryWriter _writer;

        public MidiWriter(Stream stream)
        {
            _stream = stream;
            _writer = new BinaryWriter(stream);
        }

        public void Init()
        {
            // MThd
            _writer.Write(new char[] { 'M', 'T', 'h', 'd' });
            WriteUInt32BigEndian(6);
        }

        public void WriteFormat(ushort format)
        {
            WriteUInt16BigEndian(format);
        }

        public void WriteNtrks(ushort ntrks)
        {
            WriteUInt16BigEndian(ntrks);
        }

        public void WritePPQ(ushort ppq)
        {
            WriteUInt16BigEndian(ppq);
        }

        private long _trackLengthPosition;
        private long _trackStartPosition;

        public void InitTrack()
        {
            _writer.Write(new char[] { 'M', 'T', 'r', 'k' });
            _trackLengthPosition = _stream.Position;
            _writer.Write((uint)0); // Placeholder for length
            _trackStartPosition = _stream.Position;
        }

        public void Write(MIDIEvent evt)
        {
            WriteVariableLengthQuantity(evt.DeltaTime);
            byte[] data = evt.GetData();
            _writer.Write(data);
        }

        public void EndTrack()
        {
            // Write End of Track Meta Event
            WriteVariableLengthQuantity(0);
            _writer.Write((byte)0xFF);
            _writer.Write((byte)0x2F);
            _writer.Write((byte)0x00);

            long currentPos = _stream.Position;
            uint trackLength = (uint)(currentPos - _trackStartPosition);

            _stream.Seek(_trackLengthPosition, SeekOrigin.Begin);
            WriteUInt32BigEndian(trackLength);
            _stream.Seek(currentPos, SeekOrigin.Begin);
        }

        public void Close()
        {
            _writer.Flush();
            // Stream is owned by caller usually, but BinaryWriter might close it.
            // In ConversionProcess: using (var stream = ...) { MidiWriter writer ... writer.Close(); }
            // So we should probably not close the stream if we want to be safe, or let BinaryWriter do it.
            // BinaryWriter.Close() closes the stream.
            _writer.Close();
        }

        private void WriteUInt16BigEndian(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            _writer.Write(bytes);
        }

        private void WriteUInt32BigEndian(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            _writer.Write(bytes);
        }

        private void WriteVariableLengthQuantity(uint value)
        {
            uint buffer = value & 0x7F;
            while ((value >>= 7) > 0)
            {
                buffer <<= 8;
                buffer |= 0x80;
                buffer += (value & 0x7F);
            }

            while (true)
            {
                _writer.Write((byte)(buffer & 0xFF));
                if ((buffer & 0x80) != 0)
                    buffer >>= 8;
                else
                    break;
            }
        }
    }
}
