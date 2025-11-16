using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImageToMidi.MIDI
{
    /// <summary>
    /// MIDI事件接口
    /// </summary>
    public interface IMidiEvent
    {
        long DeltaTime { get; set; }
        byte[] ToBytes();
    }

    /// <summary>
    /// MIDI音符事件
    /// </summary>
    public interface IMidiNoteEvent : IMidiEvent
    {
        byte Channel { get; set; }
        byte Note { get; set; }
        byte Velocity { get; set; }
    }

    /// <summary>
    /// MIDI处理器接口
    /// </summary>
    public interface IMidiProcessor
    {
        /// <summary>
        /// 创建MIDI文件
        /// </summary>
        Task<byte[]> CreateMidiFileAsync(IEnumerable<IMidiEvent[]> tracks, int ticksPerQuarterNote = 480);

        /// <summary>
        /// 创建音符事件
        /// </summary>
        IMidiNoteEvent CreateNoteOnEvent(byte channel, byte note, byte velocity, long deltaTime = 0);

        /// <summary>
        /// 创建音符关闭事件
        /// </summary>
        IMidiNoteEvent CreateNoteOffEvent(byte channel, byte note, byte velocity, long deltaTime);

        /// <summary>
        /// 创建控制器事件
        /// </summary>
        IMidiEvent CreateControlChangeEvent(byte channel, byte controller, byte value, long deltaTime = 0);

        /// <summary>
        /// 创建音色变更事件
        /// </summary>
        IMidiEvent CreateProgramChangeEvent(byte channel, byte program, long deltaTime = 0);

        /// <summary>
        /// 创建节拍事件
        /// </summary>
        IMidiEvent CreateTempoEvent(int microsecondsPerQuarterNote, long deltaTime = 0);

        /// <summary>
        /// 创建时间签名事件
        /// </summary>
        IMidiEvent CreateTimeSignatureEvent(byte numerator, byte denominatorPower, long deltaTime = 0);
    }

    /// <summary>
    /// MIDI轨道构建器
    /// </summary>
    public interface IMidiTrackBuilder
    {
        /// <summary>
        /// 添加音符
        /// </summary>
        void AddNote(byte channel, byte note, byte velocity, long startTime, long duration);

        /// <summary>
        /// 添加控制器变更
        /// </summary>
        void AddControlChange(byte channel, byte controller, byte value, long time);

        /// <summary>
        /// 添加程序变更
        /// </summary>
        void AddProgramChange(byte channel, byte program, long time);

        /// <summary>
        /// 获取轨道事件
        /// </summary>
        IMidiEvent[] GetEvents();

        /// <summary>
        /// 清空轨道
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// 从图像生成MIDI音符的接口
    /// </summary>
    public interface IImageToMidiConverter
    {
        /// <summary>
        /// 将图像数据转换为MIDI轨道
        /// </summary>
        Task<IMidiEvent[][]> ConvertImageToMidiAsync(
            byte[] imageData,
            int width,
            int height,
            Models.PaletteColor[] palette,
            Contracts.ImageConvertOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 创建背景轨道
        /// </summary>
        Task<IMidiEvent[]> CreateBackgroundTrackAsync(int noteCount, int noteLength, int velocity);

        /// <summary>
        /// 创建鼓点轨道
        /// </summary>
        Task<IMidiEvent[]> CreateDrumTrackAsync(int drumType, int velocity, int interval);
    }
}
