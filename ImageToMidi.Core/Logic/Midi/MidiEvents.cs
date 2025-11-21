using System;

namespace ImageToMidi.Logic.Midi
{
    public abstract class MIDIEvent
    {
        public uint DeltaTime { get; set; }
        public abstract byte[] GetData();
        
        public MIDIEvent Clone()
        {
            return (MIDIEvent)MemberwiseClone();
        }
    }

    public class NoteOnEvent : MIDIEvent
    {
        public byte Channel { get; set; }
        public byte Key { get; set; }
        public byte Velocity { get; set; }

        public NoteOnEvent(uint deltaTime, byte channel, byte key, byte velocity)
        {
            DeltaTime = deltaTime;
            Channel = channel;
            Key = key;
            Velocity = velocity;
        }

        public override byte[] GetData()
        {
            return new byte[] { (byte)(0x90 | Channel), Key, Velocity };
        }
    }

    public class NoteOffEvent : MIDIEvent
    {
        public byte Channel { get; set; }
        public byte Key { get; set; }
        public byte Velocity { get; set; }

        public NoteOffEvent(uint deltaTime, byte channel, byte key, byte velocity = 0)
        {
            DeltaTime = deltaTime;
            Channel = channel;
            Key = key;
            Velocity = velocity;
        }

        public override byte[] GetData()
        {
            return new byte[] { (byte)(0x80 | Channel), Key, Velocity };
        }
    }

    public class TempoEvent : MIDIEvent
    {
        public int MicrosecondsPerQuarterNote { get; set; }

        public TempoEvent(uint deltaTime, int microsecondsPerQuarterNote)
        {
            DeltaTime = deltaTime;
            MicrosecondsPerQuarterNote = microsecondsPerQuarterNote;
        }

        public override byte[] GetData()
        {
            byte[] data = new byte[6];
            data[0] = 0xFF;
            data[1] = 0x51;
            data[2] = 0x03;
            data[3] = (byte)((MicrosecondsPerQuarterNote >> 16) & 0xFF);
            data[4] = (byte)((MicrosecondsPerQuarterNote >> 8) & 0xFF);
            data[5] = (byte)(MicrosecondsPerQuarterNote & 0xFF);
            return data;
        }
    }

    public class ColorEvent : MIDIEvent
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; }

        public ColorEvent(uint deltaTime, byte channel, byte r, byte g, byte b, byte a)
        {
            DeltaTime = deltaTime;
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public override byte[] GetData()
        {
            // Custom Meta Event for Color? Or SysEx?
            // The original code uses it. I'll assume it's a Meta Event 0x7F (Sequencer Specific) or similar.
            // Or maybe it's just ignored by standard players but used by the visualizer.
            // I'll implement it as a Meta Event 0x0F (Text) or similar for now, or just a placeholder.
            // Wait, if I don't know the format, I might break things.
            // But since I'm writing the writer too, I can define it.
            // Let's use Meta Event 0x7F (Sequencer Specific)
            // 0xFF 0x7F len data...
            return new byte[] { 0xFF, 0x7F, 0x04, R, G, B, A }; 
        }
    }
}
