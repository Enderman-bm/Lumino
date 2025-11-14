using System;

namespace LuminoWaveTable.Models
{
    /// <summary>
    /// MIDI设备信息
    /// </summary>
    public class MidiDeviceInfo
    {
        public int DeviceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsInputDevice { get; set; }
        public bool IsOutputDevice { get; set; }
        public bool IsDefault { get; set; }
        public string Technology { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }
}