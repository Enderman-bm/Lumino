using System;
using Xunit;
using MidiReader;

namespace MidiReader.Tests
{
    public class MidiEventAdditionalDataTests
    {
        [Fact]
        public void MetaEvent_AdditionalData_IsSliceOfOriginalMemory()
        {
            // Construct bytes for single meta text event: delta=0, status=0xFF, type=0x01 (Text), length=3, data 'a','b','c'
            byte[] bytes = new byte[] { 0x00, 0xFF, 0x01, 0x03, (byte)'a', (byte)'b', (byte)'c' };
            var mem = new ReadOnlyMemory<byte>(bytes);

            var parser = new MidiEventParser(mem);
            var evt = parser.ParseNextEvent();

            Assert.True(evt.IsMetaEvent);
            Assert.Equal((byte)MetaEventType.TextEvent, evt.Data1);
            Assert.Equal(3, evt.AdditionalData.Length);
            var span = evt.AdditionalData.Span;
            Assert.Equal((byte)'a', span[0]);
            Assert.Equal((byte)'b', span[1]);
            Assert.Equal((byte)'c', span[2]);

            // Ensure it's a slice referencing original array (not a newly allocated equal array) by changing original and observing slice
            bytes[4] = (byte)'x';
            Assert.Equal((byte)'x', span[0]);
        }

        [Fact]
        public void SysExEvent_ExcludesTrailingF7_InAdditionalData()
        {
            // SysEx with trailing F7: delta=0, status=0xF0, length=3, data {1,2,0xF7}
            byte[] bytes = new byte[] { 0x00, 0xF0, 0x03, 0x01, 0x02, 0xF7 };
            var mem = new ReadOnlyMemory<byte>(bytes);

            var parser = new MidiEventParser(mem);
            var evt = parser.ParseNextEvent();

            Assert.Equal(MidiEventType.SystemExclusive, evt.EventType);
            // AdditionalData should exclude final 0xF7
            Assert.Equal(2, evt.AdditionalData.Length);
            var span = evt.AdditionalData.Span;
            Assert.Equal((byte)0x01, span[0]);
            Assert.Equal((byte)0x02, span[1]);

            // Mutate original to ensure slice points to original memory
            bytes[3] = 0x11;
            Assert.Equal((byte)0x11, span[0]);
        }
    }
}
