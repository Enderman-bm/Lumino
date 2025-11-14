using System;
using System.Collections.Generic;
using System.Linq;
using EnderDebugger;
using LuminoWaveTable.Native;

namespace LuminoWaveTable.Core
{
    /// <summary>
    /// MIDI消息处理器
    /// </summary>
    public class MidiMessageProcessor
    {
        private readonly EnderLogger _logger;
        private readonly object _lockObject = new object();
        private readonly Dictionary<int, byte> _currentPrograms; // 通道 -> 程序号
        private readonly Dictionary<int, byte> _currentControllers; // 控制器状态
        private readonly HashSet<int> _activeNotes; // 活跃的音符

        public MidiMessageProcessor()
        {
            _logger = EnderLogger.Instance;
            _currentPrograms = new Dictionary<int, byte>();
            _currentControllers = new Dictionary<int, byte>();
            _activeNotes = new HashSet<int>();
            
            InitializeDefaultState();
        }

        /// <summary>
        /// 初始化默认状态
        /// </summary>
        private void InitializeDefaultState()
        {
            lock (_lockObject)
            {
                // 初始化所有通道的默认程序（钢琴）
                for (int channel = 0; channel < 16; channel++)
                {
                    _currentPrograms[channel] = 0; // Acoustic Grand Piano
                }
                
                _logger.Debug("MidiMessageProcessor", "MIDI消息处理器初始化完成");
            }
        }

        /// <summary>
        /// 创建Note On消息
        /// </summary>
        public uint CreateNoteOn(int note, int velocity, int channel = 0)
        {
            if (note < 0 || note > 127)
                throw new ArgumentOutOfRangeException(nameof(note), "音符编号必须在0-127范围内");
            
            if (velocity < 0 || velocity > 127)
                throw new ArgumentOutOfRangeException(nameof(velocity), "力度必须在0-127范围内");
            
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须在0-15范围内");

            lock (_lockObject)
            {
                int noteKey = (channel << 8) | note;
                _activeNotes.Add(noteKey);
                
                uint message = (uint)((WinmmNative.MIDI_NOTE_ON | channel) | (note << 8) | (velocity << 16));
                _logger.Debug("MidiMessageProcessor", $"创建Note On消息 - 音符: {note}, 力度: {velocity}, 通道: {channel}, 消息: 0x{message:X8}");
                return message;
            }
        }

        /// <summary>
        /// 创建Note Off消息
        /// </summary>
        public uint CreateNoteOff(int note, int channel = 0)
        {
            if (note < 0 || note > 127)
                throw new ArgumentOutOfRangeException(nameof(note), "音符编号必须在0-127范围内");
            
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须在0-15范围内");

            lock (_lockObject)
            {
                int noteKey = (channel << 8) | note;
                _activeNotes.Remove(noteKey);
                
                uint message = (uint)((WinmmNative.MIDI_NOTE_OFF | channel) | (note << 8));
                _logger.Debug("MidiMessageProcessor", $"创建Note Off消息 - 音符: {note}, 通道: {channel}, 消息: 0x{message:X8}");
                return message;
            }
        }

        /// <summary>
        /// 创建程序变更消息
        /// </summary>
        public uint CreateProgramChange(int program, int channel = 0)
        {
            if (program < 0 || program > 127)
                throw new ArgumentOutOfRangeException(nameof(program), "程序号必须在0-127范围内");
            
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须在0-15范围内");

            lock (_lockObject)
            {
                _currentPrograms[channel] = (byte)program;
                
                uint message = (uint)((WinmmNative.MIDI_PROGRAM_CHANGE | channel) | (program << 8));
                _logger.Debug("MidiMessageProcessor", $"创建Program Change消息 - 程序: {program}, 通道: {channel}, 消息: 0x{message:X8}");
                return message;
            }
        }

        /// <summary>
        /// 创建控制器变更消息
        /// </summary>
        public uint CreateControlChange(int controller, int value, int channel = 0)
        {
            if (controller < 0 || controller > 127)
                throw new ArgumentOutOfRangeException(nameof(controller), "控制器号必须在0-127范围内");
            
            if (value < 0 || value > 127)
                throw new ArgumentOutOfRangeException(nameof(value), "控制器值必须在0-127范围内");
            
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须在0-15范围内");

            lock (_lockObject)
            {
                int controllerKey = (channel << 8) | controller;
                _currentControllers[controllerKey] = (byte)value;
                
                uint message = (uint)((WinmmNative.MIDI_CONTROL_CHANGE | channel) | (controller << 8) | (value << 16));
                _logger.Debug("MidiMessageProcessor", $"创建Control Change消息 - 控制器: {controller}, 值: {value}, 通道: {channel}, 消息: 0x{message:X8}");
                return message;
            }
        }

        /// <summary>
        /// 创建音量控制消息
        /// </summary>
        public uint CreateVolumeChange(int volume, int channel = 0)
        {
            return CreateControlChange(WinmmNative.MIDI_CONTROLLER_VOLUME, volume, channel);
        }

        /// <summary>
        /// 创建声像控制消息
        /// </summary>
        public uint CreatePanChange(int pan, int channel = 0)
        {
            return CreateControlChange(WinmmNative.MIDI_CONTROLLER_PAN, pan, channel);
        }

        /// <summary>
        /// 创建表情控制消息
        /// </summary>
        public uint CreateExpressionChange(int expression, int channel = 0)
        {
            return CreateControlChange(WinmmNative.MIDI_CONTROLLER_EXPRESSION, expression, channel);
        }

        /// <summary>
        /// 创建延音踏板消息
        /// </summary>
        public uint CreateSustainChange(bool sustain, int channel = 0)
        {
            return CreateControlChange(WinmmNative.MIDI_CONTROLLER_SUSTAIN, sustain ? 127 : 0, channel);
        }

        /// <summary>
        /// 创建所有音符关闭消息
        /// </summary>
        public uint CreateAllNotesOff(int channel = 0)
        {
            return CreateControlChange(WinmmNative.MIDI_CONTROLLER_ALL_NOTES_OFF, 0, channel);
        }

        /// <summary>
        /// 创建所有控制器重置消息
        /// </summary>
        public uint CreateResetAllControllers(int channel = 0)
        {
            return CreateControlChange(WinmmNative.MIDI_CONTROLLER_RESET_ALL_CONTROLLERS, 0, channel);
        }

        /// <summary>
        /// 创建弯音轮消息
        /// </summary>
        public uint CreatePitchBend(int value, int channel = 0)
        {
            if (value < -8192 || value > 8191)
                throw new ArgumentOutOfRangeException(nameof(value), "弯音值必须在-8192到8191范围内");
            
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须在0-15范围内");

            lock (_lockObject)
            {
                // 弯音值转换为14位数据（0-16383）
                int bendValue = value + 8192;
                byte lsb = (byte)(bendValue & 0x7F);
                byte msb = (byte)((bendValue >> 7) & 0x7F);
                
                uint message = (uint)((WinmmNative.MIDI_PITCH_BEND | channel) | (lsb << 8) | (msb << 16));
                _logger.Debug("MidiMessageProcessor", $"创建Pitch Bend消息 - 值: {value}, 通道: {channel}, 消息: 0x{message:X8}");
                return message;
            }
        }

        /// <summary>
        /// 创建通道压力消息
        /// </summary>
        public uint CreateChannelPressure(int pressure, int channel = 0)
        {
            if (pressure < 0 || pressure > 127)
                throw new ArgumentOutOfRangeException(nameof(pressure), "通道压力必须在0-127范围内");
            
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须在0-15范围内");

            lock (_lockObject)
            {
                uint message = (uint)((WinmmNative.MIDI_CHANNEL_PRESSURE | channel) | (pressure << 8));
                _logger.Debug("MidiMessageProcessor", $"创建Channel Pressure消息 - 压力: {pressure}, 通道: {channel}, 消息: 0x{message:X8}");
                return message;
            }
        }

        /// <summary>
        /// 获取指定通道的当前程序
        /// </summary>
        public int GetCurrentProgram(int channel)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须在0-15范围内");

            lock (_lockObject)
            {
                return _currentPrograms.TryGetValue(channel, out byte program) ? program : 0;
            }
        }

        /// <summary>
        /// 获取指定通道和控制器的当前值
        /// </summary>
        public int GetControllerValue(int channel, int controller)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须在0-15范围内");
            
            if (controller < 0 || controller > 127)
                throw new ArgumentOutOfRangeException(nameof(controller), "控制器号必须在0-127范围内");

            lock (_lockObject)
            {
                int controllerKey = (channel << 8) | controller;
                return _currentControllers.TryGetValue(controllerKey, out byte value) ? value : 0;
            }
        }

        /// <summary>
        /// 检查指定音符是否在播放
        /// </summary>
        public bool IsNoteActive(int note, int channel = 0)
        {
            if (note < 0 || note > 127)
                throw new ArgumentOutOfRangeException(nameof(note), "音符编号必须在0-127范围内");
            
            if (channel < 0 || channel > 15)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须在0-15范围内");

            lock (_lockObject)
            {
                int noteKey = (channel << 8) | note;
                return _activeNotes.Contains(noteKey);
            }
        }

        /// <summary>
        /// 获取所有活跃音符
        /// </summary>
        public List<(int Note, int Channel)> GetActiveNotes()
        {
            lock (_lockObject)
            {
                var result = new List<(int Note, int Channel)>();
                foreach (int noteKey in _activeNotes)
                {
                    int channel = noteKey >> 8;
                    int note = noteKey & 0xFF;
                    result.Add((note, channel));
                }
                return result;
            }
        }

        /// <summary>
        /// 创建所有音符关闭消息（所有通道）
        /// </summary>
        public List<uint> CreateAllNotesOffAllChannels()
        {
            lock (_lockObject)
            {
                var messages = new List<uint>();
                for (int channel = 0; channel < 16; channel++)
                {
                    messages.Add(CreateAllNotesOff(channel));
                }
                _activeNotes.Clear();
                return messages;
            }
        }

        /// <summary>
        /// 创建GM系统专用消息
        /// </summary>
        public byte[] CreateGmSystemOn()
        {
            // GM系统开消息
            return new byte[] { 0xF0, 0x7E, 0x7F, 0x09, 0x01, 0xF7 };
        }

        /// <summary>
        /// 创建GS系统专用消息
        /// </summary>
        public byte[] CreateGsReset()
        {
            // GS系统重置消息
            return new byte[] { 0xF0, 0x41, 0x10, 0x42, 0x12, 0x40, 0x00, 0x7F, 0x00, 0x41, 0xF7 };
        }

        /// <summary>
        /// 创建XG系统专用消息
        /// </summary>
        public byte[] CreateXgSystemOn()
        {
            // XG系统开消息
            return new byte[] { 0xF0, 0x43, 0x10, 0x4C, 0x00, 0x00, 0x7E, 0x00, 0xF7 };
        }

        /// <summary>
        /// 重置处理器状态
        /// </summary>
        public void Reset()
        {
            lock (_lockObject)
            {
                _currentPrograms.Clear();
                _currentControllers.Clear();
                _activeNotes.Clear();
                
                InitializeDefaultState();
                _logger.Debug("MidiMessageProcessor", "MIDI消息处理器状态已重置");
            }
        }

        /// <summary>
        /// 解析MIDI消息
        /// </summary>
        public (byte Status, byte Data1, byte Data2, byte Channel) ParseMidiMessage(uint message)
        {
            byte status = (byte)(message & 0xFF);
            byte data1 = (byte)((message >> 8) & 0xFF);
            byte data2 = (byte)((message >> 16) & 0xFF);
            byte channel = (byte)(status & 0x0F);
            byte messageType = (byte)(status & 0xF0);

            return (messageType, data1, data2, channel);
        }

        /// <summary>
        /// 验证MIDI消息
        /// </summary>
        public bool ValidateMidiMessage(uint message)
        {
            try
            {
                var (status, data1, data2, channel) = ParseMidiMessage(message);
                
                // 验证通道
                if (channel > 15) return false;
                
                // 验证数据字节
                if (data1 > 127 || data2 > 127) return false;
                
                // 验证状态字节
                byte messageType = (byte)(status & 0xF0);
                switch (messageType)
                {
                    case WinmmNative.MIDI_NOTE_OFF:
                    case WinmmNative.MIDI_NOTE_ON:
                    case WinmmNative.MIDI_KEY_PRESSURE:
                    case WinmmNative.MIDI_CONTROL_CHANGE:
                    case WinmmNative.MIDI_PITCH_BEND:
                        // 这些消息类型需要两个数据字节
                        return true;
                        
                    case WinmmNative.MIDI_PROGRAM_CHANGE:
                    case WinmmNative.MIDI_CHANNEL_PRESSURE:
                        // 这些消息类型只需要一个数据字节
                        return true;
                        
                    default:
                        // 其他消息类型视为无效
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}