using System;
using System.Runtime.InteropServices;
using System.Text;
using EnderDebugger;

namespace EnderWaveTableAccessingParty.Services
{
    /// <summary>
    /// WinMM.dll 的 P/Invoke 封装
    /// </summary>
    internal static class WinmmNative
    {
        private const string WINMM_DLL = "winmm.dll";

        // MIDI设备相关常量
        public const int MIDIERR_BASE = 64;
        public const int MIDIERR_NODEVICE = MIDIERR_BASE + 4;
        public const int MIDIERR_STILLPLAYING = MIDIERR_BASE + 1;
        public const int MAXPNAMELEN = 32;
        public const int MMSYSERR_NOERROR = 0;
        public const int MMSYSERR_BADDEVICEID = 2;
        public const int MMSYSERR_INVALHANDLE = 5;

        // MIDI消息类型常量
        public const int MIDI_NOTE_OFF = 0x80;
        public const int MIDI_NOTE_ON = 0x90;
        public const int MIDI_CONTROL_CHANGE = 0xB0;
        public const int MIDI_PROGRAM_CHANGE = 0xC0;

        // MIDI设备能力结构
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MIDIOUTCAPS
        {
            public short wMid;
            public short wPid;
            public int vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAXPNAMELEN)]
            public string szPname;
            public short wTechnology;
            public short wVoices;
            public short wNotes;
            public short wChannelMask;
            public int dwSupport;
        }

        // MIDI设备打开参数
        [StructLayout(LayoutKind.Sequential)]
        public struct MIDIHDR
        {
            public IntPtr lpData;
            public int dwBufferLength;
            public int dwBytesRecorded;
            public IntPtr dwUser;
            public int dwFlags;
            public IntPtr lpNext;
            public IntPtr reserved;
            public int dwOffset;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public IntPtr[] dwReserved;
        }

        // 获取MIDI输出设备数量
        [DllImport(WINMM_DLL)]
        public static extern int midiOutGetNumDevs();

        // 获取MIDI输出设备能力
        [DllImport(WINMM_DLL)]
        public static extern int midiOutGetDevCaps(int uDeviceID, ref MIDIOUTCAPS lpMidiOutCaps, int cbMidiOutCaps);

        // 打开MIDI输出设备
        [DllImport(WINMM_DLL)]
        public static extern int midiOutOpen(ref int lphMidiOut, int uDeviceID, IntPtr dwCallback, IntPtr dwInstance, int dwFlags);

        // 关闭MIDI输出设备
        [DllImport(WINMM_DLL)]
        public static extern int midiOutClose(int hMidiOut);

        // 发送短MIDI消息
        [DllImport(WINMM_DLL)]
        public static extern int midiOutShortMsg(int hMidiOut, int dwMsg);

        // 重置MIDI输出设备
        [DllImport(WINMM_DLL)]
        public static extern int midiOutReset(int hMidiOut);

        // 获取错误文本
        [DllImport(WINMM_DLL)]
        public static extern int midiOutGetErrorText(int mmrError, StringBuilder lpText, int cchText);

        // 准备MIDI头
        [DllImport(WINMM_DLL)]
        public static extern int midiOutPrepareHeader(int hMidiOut, ref MIDIHDR lpMidiOutHdr, int cbMidiOutHdr);

        // 取消准备MIDI头
        [DllImport(WINMM_DLL)]
        public static extern int midiOutUnprepareHeader(int hMidiOut, ref MIDIHDR lpMidiOutHdr, int cbMidiOutHdr);

        // 发送长MIDI消息
        [DllImport(WINMM_DLL)]
        public static extern int midiOutLongMsg(int hMidiOut, ref MIDIHDR lpMidiOutHdr, int cbMidiOutHdr);

        /// <summary>
        /// 获取MIDI错误文本
        /// </summary>
        public static string GetMidiErrorText(int errorCode)
        {
            var sb = new StringBuilder(256);
            int result = midiOutGetErrorText(errorCode, sb, sb.Capacity);
            string errorText = result == MMSYSERR_NOERROR ? sb.ToString() : $"Unknown MIDI error: {errorCode}";
            
            var logger = EnderLogger.Instance;
            logger.Debug("WinmmNative", $"获取MIDI错误文本 - 错误码: {errorCode}, 结果: {result}, 文本: {errorText}");
            
            return errorText;
        }

        /// <summary>
        /// 创建MIDI音符消息
        /// </summary>
        public static int CreateMidiMessage(int status, int data1, int data2, int channel = 0)
        {
            int message = (status | channel) | (data1 << 8) | (data2 << 16);
            var logger = EnderLogger.Instance;
            logger.Debug("WinmmNative", $"创建MIDI消息 - 状态: 0x{status:X}, 数据1: {data1}, 数据2: {data2}, 通道: {channel}, 结果: 0x{message:X8}");
            return message;
        }
        
        /// <summary>
        /// 检查MIDI设备句柄是否有效
        /// </summary>
        /// <param name="hMidiOut">MIDI输出设备句柄</param>
        /// <returns>如果句柄有效返回true，否则返回false</returns>
        public static bool IsMidiOutHandleValid(int hMidiOut)
        {
            // 句柄为-1表示未初始化
            if (hMidiOut == -1)
                return false;
                
            // 尝试发送一个空消息来验证句柄是否有效
            int result = midiOutShortMsg(hMidiOut, 0);
            return result == MMSYSERR_NOERROR || result != MMSYSERR_INVALHANDLE;
        }
    }
}