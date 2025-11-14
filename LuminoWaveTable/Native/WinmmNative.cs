using System;
using System.Runtime.InteropServices;
using System.Text;
using EnderDebugger;

namespace LuminoWaveTable.Native
{
    /// <summary>
    /// WinMM.dll P/Invoke 封装
    /// </summary>
    internal static class WinmmNative
    {
        private const string WINMM_DLL = "winmm.dll";
        private static readonly EnderLogger _logger = EnderLogger.Instance;

        #region 常量定义

        // MIDI错误码
        public const int MMSYSERR_NOERROR = 0;
        public const int MMSYSERR_BADDEVICEID = 2;
        public const int MMSYSERR_INVALHANDLE = 5;
        public const int MMSYSERR_NOMEM = 7;
        public const int MMSYSERR_INVALPARAM = 11;
        public const int MMSYSERR_HANDLEBUSY = 12;
        public const int MMSYSERR_INVALIDALIAS = 13;
        public const int MMSYSERR_BADDB = 14;
        public const int MMSYSERR_KEYNOTFOUND = 15;
        public const int MMSYSERR_READERROR = 16;
        public const int MMSYSERR_WRITEERROR = 17;
        public const int MMSYSERR_DELETEERROR = 18;
        public const int MMSYSERR_VALNOTFOUND = 19;
        public const int MMSYSERR_NODRIVER = 20;
        public const int MMSYSERR_ALLOCATED = 21;

        // MIDI设备错误码
        public const int MIDIERR_BASE = 64;
        public const int MIDIERR_UNPREPARED = MIDIERR_BASE + 0;
        public const int MIDIERR_STILLPLAYING = MIDIERR_BASE + 1;
        public const int MIDIERR_NOMAP = MIDIERR_BASE + 2;
        public const int MIDIERR_NOTREADY = MIDIERR_BASE + 3;
        public const int MIDIERR_NODEVICE = MIDIERR_BASE + 4;
        public const int MIDIERR_INVALIDSETUP = MIDIERR_BASE + 5;
        public const int MIDIERR_BADOPENMODE = MIDIERR_BASE + 6;
        public const int MIDIERR_DONT_WAIT = MIDIERR_BASE + 7;
        public const int MIDIERR_INVALID_HANDLE = MIDIERR_BASE + 8;
        public const int MIDIERR_LAST_ERROR = MIDIERR_BASE + 8;

        // MIDI消息类型
        public const int MIDI_NOTE_OFF = 0x80;
        public const int MIDI_NOTE_ON = 0x90;
        public const int MIDI_KEY_PRESSURE = 0xA0;
        public const int MIDI_CONTROL_CHANGE = 0xB0;
        public const int MIDI_PROGRAM_CHANGE = 0xC0;
        public const int MIDI_CHANNEL_PRESSURE = 0xD0;
        public const int MIDI_PITCH_BEND = 0xE0;
        public const int MIDI_SYSTEM_EXCLUSIVE = 0xF0;

        // 控制器编号
        public const int MIDI_CONTROLLER_BANK_SELECT = 0x00;
        public const int MIDI_CONTROLLER_MODULATION = 0x01;
        public const int MIDI_CONTROLLER_VOLUME = 0x07;
        public const int MIDI_CONTROLLER_PAN = 0x0A;
        public const int MIDI_CONTROLLER_EXPRESSION = 0x0B;
        public const int MIDI_CONTROLLER_SUSTAIN = 0x40;
        public const int MIDI_CONTROLLER_PORTAMENTO = 0x41;
        public const int MIDI_CONTROLLER_SOSTENUTO = 0x42;
        public const int MIDI_CONTROLLER_SOFT_PEDAL = 0x43;
        public const int MIDI_CONTROLLER_LEGATO = 0x44;
        public const int MIDI_CONTROLLER_HOLD_2 = 0x45;
        public const int MIDI_CONTROLLER_SOUND_CONTROLLER_1 = 0x46;
        public const int MIDI_CONTROLLER_SOUND_CONTROLLER_2 = 0x47;
        public const int MIDI_CONTROLLER_SOUND_CONTROLLER_3 = 0x48;
        public const int MIDI_CONTROLLER_SOUND_CONTROLLER_4 = 0x49;
        public const int MIDI_CONTROLLER_SOUND_CONTROLLER_5 = 0x4A;
        public const int MIDI_CONTROLLER_SOUND_CONTROLLER_6 = 0x4B;
        public const int MIDI_CONTROLLER_SOUND_CONTROLLER_7 = 0x4C;
        public const int MIDI_CONTROLLER_SOUND_CONTROLLER_8 = 0x4D;
        public const int MIDI_CONTROLLER_SOUND_CONTROLLER_9 = 0x4E;
        public const int MIDI_CONTROLLER_SOUND_CONTROLLER_10 = 0x4F;
        public const int MIDI_CONTROLLER_ALL_SOUND_OFF = 0x78;
        public const int MIDI_CONTROLLER_RESET_ALL_CONTROLLERS = 0x79;
        public const int MIDI_CONTROLLER_LOCAL_CONTROL = 0x7A;
        public const int MIDI_CONTROLLER_ALL_NOTES_OFF = 0x7B;
        public const int MIDI_CONTROLLER_OMNI_MODE_OFF = 0x7C;
        public const int MIDI_CONTROLLER_OMNI_MODE_ON = 0x7D;
        public const int MIDI_CONTROLLER_MONO_MODE_ON = 0x7E;
        public const int MIDI_CONTROLLER_POLY_MODE_ON = 0x7F;

        // 其他常量
        public const int MAXPNAMELEN = 32;
        public const int MIDI_IO_STATUS = 0x00000020;
        public const int MIDI_IO_CONTROL = 0x00000010;
        public const int MIDI_IO_EXCLUSIVE = 0x00000008;
        public const int MIDI_IO_COMMON = 0x00000004;
        public const int MIDI_IO_REALTIME = 0x00000002;
        public const int MIDI_IO_COOKED = 0x00000002;
        public const int MIDI_IO_RAW = 0x00000000;

        #endregion

        #region 结构体定义

        /// <summary>
        /// MIDI输出设备能力结构
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MIDIOUTCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAXPNAMELEN)]
            public string szPname;
            public ushort wTechnology;
            public ushort wVoices;
            public ushort wNotes;
            public ushort wChannelMask;
            public uint dwSupport;
        }

        /// <summary>
        /// MIDI输入设备能力结构
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MIDIINCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAXPNAMELEN)]
            public string szPname;
            public uint dwSupport;
        }

        /// <summary>
        /// MIDI头结构
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MIDIHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public IntPtr lpNext;
            public IntPtr reserved;
            public uint dwOffset;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public IntPtr[] dwReserved;
        }

        #endregion

        #region WinMM API函数

        /// <summary>
        /// 获取MIDI输出设备数量
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiOutGetNumDevs();

        /// <summary>
        /// 获取MIDI输出设备能力
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiOutGetDevCaps(uint uDeviceID, ref MIDIOUTCAPS lpMidiOutCaps, uint cbMidiOutCaps);

        /// <summary>
        /// 打开MIDI输出设备
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiOutOpen(ref IntPtr lphMidiOut, uint uDeviceID, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);

        /// <summary>
        /// 关闭MIDI输出设备
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiOutClose(IntPtr hMidiOut);

        /// <summary>
        /// 发送短MIDI消息
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiOutShortMsg(IntPtr hMidiOut, uint dwMsg);

        /// <summary>
        /// 重置MIDI输出设备
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiOutReset(IntPtr hMidiOut);

        /// <summary>
        /// 获取MIDI错误文本
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiOutGetErrorText(uint mmrError, StringBuilder lpText, uint cchText);

        /// <summary>
        /// 准备MIDI头
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiOutPrepareHeader(IntPtr hMidiOut, ref MIDIHDR lpMidiOutHdr, uint cbMidiOutHdr);

        /// <summary>
        /// 取消准备MIDI头
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiOutUnprepareHeader(IntPtr hMidiOut, ref MIDIHDR lpMidiOutHdr, uint cbMidiOutHdr);

        /// <summary>
        /// 发送长MIDI消息
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiOutLongMsg(IntPtr hMidiOut, ref MIDIHDR lpMidiOutHdr, uint cbMidiOutHdr);

        /// <summary>
        /// 获取MIDI输入设备数量
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiInGetNumDevs();

        /// <summary>
        /// 获取MIDI输入设备能力
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiInGetDevCaps(uint uDeviceID, ref MIDIINCAPS lpMidiInCaps, uint cbMidiInCaps);

        /// <summary>
        /// 打开MIDI输入设备
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiInOpen(ref IntPtr lphMidiIn, uint uDeviceID, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);

        /// <summary>
        /// 关闭MIDI输入设备
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiInClose(IntPtr hMidiIn);

        /// <summary>
        /// 重置MIDI输入设备
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiInReset(IntPtr hMidiIn);

        /// <summary>
        /// 开始MIDI输入
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiInStart(IntPtr hMidiIn);

        /// <summary>
        /// 停止MIDI输入
        /// </summary>
        [DllImport(WINMM_DLL)]
        public static extern uint midiInStop(IntPtr hMidiIn);

        #endregion

        #region 辅助方法

        /// <summary>
        /// 创建MIDI消息
        /// </summary>
        public static uint CreateMidiMessage(byte status, byte data1, byte data2, byte channel = 0)
        {
            uint message = (uint)((status | channel) | (data1 << 8) | (data2 << 16));
            _logger.Debug("WinmmNative", $"创建MIDI消息 - 状态: 0x{status:X2}, 数据1: {data1}, 数据2: {data2}, 通道: {channel}, 结果: 0x{message:X8}");
            return message;
        }

        /// <summary>
        /// 获取MIDI错误文本
        /// </summary>
        public static string GetMidiErrorText(uint errorCode)
        {
            try
            {
                var sb = new StringBuilder(256);
                uint result = midiOutGetErrorText(errorCode, sb, (uint)sb.Capacity);
                string errorText = result == MMSYSERR_NOERROR ? sb.ToString() : $"Unknown MIDI error: {errorCode}";
                _logger.Debug("WinmmNative", $"获取MIDI错误文本 - 错误码: {errorCode}, 结果: {result}, 文本: {errorText}");
                return errorText;
            }
            catch (Exception ex)
            {
                _logger.Error("WinmmNative", $"获取MIDI错误文本失败: {ex.Message}");
                return $"Error retrieving MIDI error text: {ex.Message}";
            }
        }

        /// <summary>
        /// 检查MIDI设备句柄是否有效
        /// </summary>
        public static bool IsMidiOutHandleValid(IntPtr hMidiOut)
        {
            // 句柄为-1表示未初始化
            if (hMidiOut == IntPtr.Zero || hMidiOut == new IntPtr(-1))
                return false;

            // 尝试发送一个空消息来验证句柄是否有效
            try
            {
                uint result = midiOutShortMsg(hMidiOut, 0);
                return result == MMSYSERR_NOERROR || result != MMSYSERR_INVALHANDLE;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取所有可用的MIDI输出设备
        /// </summary>
        public static List<MIDIOUTCAPS> GetMidiOutDevices()
        {
            var devices = new List<MIDIOUTCAPS>();
            try
            {
                uint deviceCount = midiOutGetNumDevs();
                _logger.Info("WinmmNative", $"发现 {deviceCount} 个MIDI输出设备");

                for (uint i = 0; i < deviceCount; i++)
                {
                    var caps = new MIDIOUTCAPS();
                    uint result = midiOutGetDevCaps(i, ref caps, (uint)Marshal.SizeOf(typeof(MIDIOUTCAPS)));
                    if (result == MMSYSERR_NOERROR)
                    {
                        devices.Add(caps);
                        _logger.Debug("WinmmNative", $"设备 {i}: {caps.szPname}, 技术: {caps.wTechnology}, 复音: {caps.wVoices}");
                    }
                    else
                    {
                        _logger.Warn("WinmmNative", $"获取设备 {i} 能力失败: {GetMidiErrorText(result)}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("WinmmNative", $"获取MIDI输出设备列表失败: {ex.Message}");
            }
            return devices;
        }

        #endregion
    }
}