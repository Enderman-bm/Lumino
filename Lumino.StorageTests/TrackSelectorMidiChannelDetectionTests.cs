using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lumino.ViewModels;
using MidiReader;
using Xunit;

namespace Lumino.StorageTests
{
    public class TrackSelectorMidiChannelDetectionTests
    {
        [Fact]
        public void LoadTracksFromMidi_Detects_Channel_From_NoteOnAndNoteOff()
        {
            // channel to encode (0-based)
            byte channel = 3; // expects MidiChannel == 3

            // build a single-track MIDI containing a NoteOn (vel>0) and NoteOff for that channel
            var trackBody = new MemoryStream();
            var bw = new BinaryWriter(trackBody, Encoding.UTF8, leaveOpen: true);

            // delta=0, NoteOn (0x90 | channel), note=60, vel=100
            WriteVariableLength(bw, 0);
            bw.Write((byte)(0x90 | channel));
            bw.Write((byte)60);
            bw.Write((byte)100);

            // delta=480, NoteOff (0x80 | channel), note=60, vel=0
            WriteVariableLength(bw, 480);
            bw.Write((byte)(0x80 | channel));
            bw.Write((byte)60);
            bw.Write((byte)0);

            // End of track meta event
            WriteVariableLength(bw, 0);
            bw.Write((byte)0xFF);
            bw.Write((byte)0x2F);
            WriteVariableLength(bw, 0);

            bw.Flush();
            var bodyBytes = trackBody.ToArray();

            var midiBytes = BuildMidiFile(new List<byte[]> { bodyBytes });

            using var midiFile = MidiFile.LoadFromBytes(midiBytes);

            var selector = new TrackSelectorViewModel();
            selector.LoadTracksFromMidi(midiFile);

            // first non-conductor track should have detected channel
            var regular = selector.Tracks.Where(t => !t.IsConductorTrack).ToList();
            Assert.NotEmpty(regular);
            Assert.Equal(channel, (byte)regular[0].MidiChannel);
        }

        [Fact]
        public void LoadTracksFromMidi_Detects_Channel_When_NoteOn_VelocityZero_And_ProgramChange()
        {
            // channel to encode (0-based)
            byte channel = 7;

            var trackBody = new MemoryStream();
            var bw = new BinaryWriter(trackBody, Encoding.UTF8, leaveOpen: true);

            // delta=0, NoteOn vel=0 (treated as note off by some writers)
            WriteVariableLength(bw, 0);
            bw.Write((byte)(0x90 | channel));
            bw.Write((byte)64);
            bw.Write((byte)0);

            // delta=0, ProgramChange (status 0xC0|ch) data=5
            WriteVariableLength(bw, 0);
            bw.Write((byte)(0xC0 | channel));
            bw.Write((byte)5);

            // delta=0 End of track
            WriteVariableLength(bw, 0);
            bw.Write((byte)0xFF);
            bw.Write((byte)0x2F);
            WriteVariableLength(bw, 0);

            bw.Flush();
            var bodyBytes = trackBody.ToArray();

            var midiBytes = BuildMidiFile(new List<byte[]> { bodyBytes });

            using var midiFile = MidiFile.LoadFromBytes(midiBytes);

            var selector = new TrackSelectorViewModel();
            selector.LoadTracksFromMidi(midiFile);

            var regular = selector.Tracks.Where(t => !t.IsConductorTrack).ToList();
            Assert.NotEmpty(regular);
            Assert.Equal(channel, (byte)regular[0].MidiChannel);
        }

        private static byte[] BuildMidiFile(List<byte[]> trackBodies)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            // Header 'MThd'
            bw.Write(Encoding.ASCII.GetBytes("MThd"));
            // header length 6
            bw.Write(ToBigEndian((uint)6));
            // format 1 (multiple track)
            bw.Write(ToBigEndian((ushort)1));
            // track count
            bw.Write(ToBigEndian((ushort)trackBodies.Count));
            // ticks per quarter note
            bw.Write(ToBigEndian((ushort)480));

            // each track
            foreach (var body in trackBodies)
            {
                bw.Write(Encoding.ASCII.GetBytes("MTrk"));
                bw.Write(ToBigEndian((uint)body.Length));
                bw.Write(body);
            }

            return ms.ToArray();
        }

        private static byte[] ToBigEndian(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes;
        }

        private static byte[] ToBigEndian(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes;
        }

        private static void WriteVariableLength(BinaryWriter writer, uint value)
        {
            if (value == 0)
            {
                writer.Write((byte)0);
                return;
            }

            var bytes = new List<byte>();
            uint val = value;
            while (val > 0)
            {
                byte b = (byte)(val & 0x7F);
                val >>= 7;
                if (bytes.Count > 0) b |= 0x80;
                bytes.Add(b);
            }
            bytes.Reverse();
            foreach (var b in bytes) writer.Write(b);
        }
    }
}
