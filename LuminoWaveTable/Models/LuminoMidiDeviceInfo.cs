using System;

namespace LuminoWaveTable.Models
{
    /// <summary>
    /// Lumino MIDI设备信息
    /// </summary>
    public class LuminoMidiDeviceInfo
    {
        public int DeviceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public uint Technology { get; set; }
        public uint Voices { get; set; }
        public uint Notes { get; set; }
        public uint ChannelMask { get; set; }
        public uint Support { get; set; }
        public bool IsAvailable { get; set; }
    }
}