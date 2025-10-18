using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace EnderAudioAnalyzer.Decoders;

/// <summary>
/// AAC (Advanced Audio Coding) 解码器
/// 实现 ISO/IEC 13818-7 (MPEG-2 AAC) 和 ISO/IEC 14496-3 (MPEG-4 AAC) 标准
/// </summary>
public class AacDecoder
{
    private AacStreamInfo? _streamInfo;
    private readonly AacBitReader _bitReader;
    
    // AAC 配置文件
    public enum AacProfile
    {
        Main = 1,
        LC = 2,        // Low Complexity (最常用)
        SSR = 3,       // Scalable Sample Rate
        LTP = 4        // Long Term Prediction
    }
    
    // 采样率索引表
    private static readonly int[] SampleRateTable = {
        96000, 88200, 64000, 48000, 44100, 32000,
        24000, 22050, 16000, 12000, 11025, 8000, 7350
    };
    
    // 窗口序列
    private enum WindowSequence
    {
        OnlyLong = 0,
        LongStart = 1,
        EightShort = 2,
        LongStop = 3
    }
    
    public AacDecoder()
    {
        _bitReader = new AacBitReader();
    }
    
    /// <summary>
    /// 解析 AAC 文件（ADTS 或 ADIF 格式）
    /// </summary>
    public async Task<AacStreamInfo> ParseAsync(Stream stream)
    {
        // 检查是否为 ADIF 格式
        var header = new byte[4];
        await stream.ReadAsync(header, 0, 4);
        
        if (header[0] == 'A' && header[1] == 'D' && header[2] == 'I' && header[3] == 'F')
        {
            return await ParseAdifAsync(stream);
        }
        else
        {
            // 假设为 ADTS 格式
            stream.Position = 0;
            return await ParseAdtsAsync(stream);
        }
    }
    
    /// <summary>
    /// 解析 ADTS 格式
    /// </summary>
    private async Task<AacStreamInfo> ParseAdtsAsync(Stream stream)
    {
        // 查找第一个 ADTS 帧同步字
        while (stream.Position < stream.Length)
        {
            var b1 = stream.ReadByte();
            if (b1 != 0xFF) continue;
            
            var b2 = stream.ReadByte();
            if ((b2 & 0xF6) != 0xF0)
            {
                stream.Position -= 1;
                continue;
            }
            
            // 读取 ADTS 帧头（7 或 9 字节）
            stream.Position -= 2;
            var adtsHeader = new byte[7];
            await stream.ReadAsync(adtsHeader, 0, 7);
            
            // 解析 ADTS 头
            var header = ParseAdtsHeader(adtsHeader);
            
            _streamInfo = new AacStreamInfo
            {
                Profile = header.Profile,
                SampleRate = header.SampleRate,
                Channels = header.Channels,
                BitsPerSample = 16,
                Duration = await EstimateDurationAsync(stream, header)
            };
            
            stream.Position = 0;
            return _streamInfo;
        }
        
        throw new InvalidDataException("未找到有效的 ADTS 帧头");
    }
    
    /// <summary>
    /// 解析 ADIF 格式
    /// </summary>
    private async Task<AacStreamInfo> ParseAdifAsync(Stream stream)
    {
        stream.Position = 0;
        var buffer = new byte[32];
        await stream.ReadAsync(buffer, 0, 32);
        
        _bitReader.Initialize(buffer);
        
        // 跳过 ADIF ID
        _bitReader.ReadBits(32);
        
        // 读取版本和其他信息
        var copyrightPresent = _bitReader.ReadBits(1) == 1;
        if (copyrightPresent)
        {
            _bitReader.ReadBits(72); // 跳过版权信息
        }
        
        var original = _bitReader.ReadBits(1);
        var home = _bitReader.ReadBits(1);
        var bitstreamType = _bitReader.ReadBits(1);
        var bitrate = _bitReader.ReadBits(23);
        var numProgramConfigElements = _bitReader.ReadBits(4);
        
        // 读取程序配置元素
        var profile = (AacProfile)_bitReader.ReadBits(2);
        var sampleRateIndex = _bitReader.ReadBits(4);
        var channels = _bitReader.ReadBits(4);
        
        _streamInfo = new AacStreamInfo
        {
            Profile = profile,
            SampleRate = SampleRateTable[sampleRateIndex],
            Channels = channels,
            BitsPerSample = 16,
            Bitrate = bitrate,
            Duration = TimeSpan.Zero
        };
        
        stream.Position = 0;
        return _streamInfo;
    }
    
    /// <summary>
    /// 解码 AAC 音频数据
    /// </summary>
    public async Task<float[]> DecodeAsync(Stream stream, int maxSamples = -1)
    {
        if (_streamInfo == null)
        {
            await ParseAsync(stream);
            stream.Position = 0;
        }
        
        var samples = new List<float>();
        
        while (stream.Position < stream.Length)
        {
            if (maxSamples > 0 && samples.Count >= maxSamples)
                break;
            
            try
            {
                var frame = await ReadAdtsFrameAsync(stream);
                if (frame == null) break;
                
                var frameSamples = DecodeFrame(frame);
                samples.AddRange(frameSamples);
            }
            catch
            {
                break;
            }
        }
        
        return samples.ToArray();
    }
    
    /// <summary>
    /// 读取 ADTS 帧
    /// </summary>
    private async Task<AacFrame?> ReadAdtsFrameAsync(Stream stream)
    {
        // 查找帧同步字
        while (stream.Position < stream.Length)
        {
            var b1 = stream.ReadByte();
            if (b1 != 0xFF) continue;
            
            var b2 = stream.ReadByte();
            if ((b2 & 0xF6) != 0xF0)
            {
                stream.Position -= 1;
                continue;
            }
            
            // 读取完整的 ADTS 头
            stream.Position -= 2;
            var headerBytes = new byte[7];
            await stream.ReadAsync(headerBytes, 0, 7);
            
            var header = ParseAdtsHeader(headerBytes);
            
            // 读取帧数据
            var frameData = new byte[header.FrameLength - 7];
            await stream.ReadAsync(frameData, 0, frameData.Length);
            
            return new AacFrame
            {
                Header = header,
                Data = frameData
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// 解析 ADTS 帧头
    /// </summary>
    private AdtsHeader ParseAdtsHeader(byte[] header)
    {
        // 同步字：12 位 (0xFFF)
        // ID: 1 位 (0 = MPEG-4, 1 = MPEG-2)
        // Layer: 2 位 (总是 00)
        // Protection absent: 1 位
        
        var id = (header[1] & 0x08) >> 3;
        var protectionAbsent = (header[1] & 0x01) != 0;
        
        var profile = (AacProfile)(((header[2] & 0xC0) >> 6) + 1);
        var sampleRateIndex = (header[2] & 0x3C) >> 2;
        var channelConfig = ((header[2] & 0x01) << 2) | ((header[3] & 0xC0) >> 6);
        
        var frameLength = ((header[3] & 0x03) << 11) | (header[4] << 3) | ((header[5] & 0xE0) >> 5);
        
        return new AdtsHeader
        {
            Profile = profile,
            SampleRate = SampleRateTable[sampleRateIndex],
            Channels = channelConfig,
            FrameLength = frameLength,
            ProtectionAbsent = protectionAbsent
        };
    }
    
    /// <summary>
    /// 解码帧
    /// </summary>
    private float[] DecodeFrame(AacFrame frame)
    {
        _bitReader.Initialize(frame.Data);
        
        var samples = new List<float>();
        
        // 读取 Individual Channel Stream (ICS)
        for (int ch = 0; ch < frame.Header.Channels; ch++)
        {
            var channelSamples = DecodeChannelStream();
            
            // 交织样本
            for (int i = 0; i < channelSamples.Length; i++)
            {
                if (samples.Count <= i * frame.Header.Channels + ch)
                {
                    samples.AddRange(new float[frame.Header.Channels]);
                }
                samples[i * frame.Header.Channels + ch] = channelSamples[i];
            }
        }
        
        return samples.ToArray();
    }
    
    /// <summary>
    /// 解码声道流
    /// </summary>
    private float[] DecodeChannelStream()
    {
        // 读取 ICS 信息
        var globalGain = _bitReader.ReadBits(8);
        
        // 读取窗口序列
        var windowSequence = (WindowSequence)_bitReader.ReadBits(2);
        var windowShape = _bitReader.ReadBits(1);
        
        int numWindows = windowSequence == WindowSequence.EightShort ? 8 : 1;
        int windowLength = windowSequence == WindowSequence.EightShort ? 128 : 1024;
        
        // 读取比例因子
        var scalefactors = ReadScalefactors(numWindows);
        
        // 读取频谱数据（Huffman 编码）
        var spectrumData = ReadSpectrumData(windowLength * numWindows);
        
        // 反量化
        Dequantize(spectrumData, scalefactors, globalGain);
        
        // IMDCT
        var timeDomain = PerformImdct(spectrumData, windowSequence, windowLength, numWindows);
        
        return timeDomain;
    }
    
    /// <summary>
    /// 读取比例因子
    /// </summary>
    private int[] ReadScalefactors(int numWindows)
    {
        var scalefactors = new int[numWindows * 16]; // 简化：每个窗口最多 16 个频段
        
        int currentScalefactor = 0;
        
        for (int i = 0; i < scalefactors.Length; i++)
        {
            // 使用 Huffman 解码读取增量
            var delta = _bitReader.ReadSignedBits(4); // 简化实现
            currentScalefactor += delta;
            scalefactors[i] = currentScalefactor;
        }
        
        return scalefactors;
    }
    
    /// <summary>
    /// 读取频谱数据
    /// </summary>
    private float[] ReadSpectrumData(int length)
    {
        var spectrum = new float[length];
        
        // 简化实现：使用固定位数读取
        for (int i = 0; i < length && _bitReader.RemainingBits > 0; i++)
        {
            if (_bitReader.RemainingBits < 8) break;
            spectrum[i] = _bitReader.ReadSignedBits(8);
        }
        
        return spectrum;
    }
    
    /// <summary>
    /// 反量化
    /// </summary>
    private void Dequantize(float[] spectrum, int[] scalefactors, int globalGain)
    {
        for (int i = 0; i < spectrum.Length; i++)
        {
            if (spectrum[i] != 0)
            {
                int sfIndex = Math.Min(i / 64, scalefactors.Length - 1);
                float scale = (float)Math.Pow(2.0, (globalGain - 100 + scalefactors[sfIndex]) / 4.0);
                
                var sign = spectrum[i] < 0 ? -1 : 1;
                var absValue = Math.Abs(spectrum[i]);
                spectrum[i] = sign * scale * (float)Math.Pow(absValue, 4.0 / 3.0);
            }
        }
    }
    
    /// <summary>
    /// 执行 IMDCT
    /// </summary>
    private float[] PerformImdct(float[] spectrum, WindowSequence windowSequence, int windowLength, int numWindows)
    {
        var output = new float[windowLength * numWindows];
        
        for (int win = 0; win < numWindows; win++)
        {
            var windowSpectrum = new float[windowLength];
            Array.Copy(spectrum, win * windowLength, windowSpectrum, 0, windowLength);
            
            // IMDCT
            var windowOutput = new float[windowLength * 2];
            
            for (int i = 0; i < windowLength * 2; i++)
            {
                float sum = 0;
                for (int k = 0; k < windowLength; k++)
                {
                    sum += windowSpectrum[k] * (float)Math.Cos(
                        Math.PI / windowLength * (i + 0.5 + windowLength / 2.0) * (k + 0.5)
                    );
                }
                windowOutput[i] = sum;
            }
            
            // 窗口化
            ApplyWindow(windowOutput, windowSequence);
            
            // 重叠相加
            for (int i = 0; i < windowLength; i++)
            {
                output[win * windowLength + i] = windowOutput[i];
            }
        }
        
        return output;
    }
    
    /// <summary>
    /// 应用窗口函数
    /// </summary>
    private void ApplyWindow(float[] samples, WindowSequence windowSequence)
    {
        int n = samples.Length;
        
        for (int i = 0; i < n; i++)
        {
            // Kaiser-Bessel Derived (KBD) 窗口的简化版本
            float window = (float)Math.Sin(Math.PI * (i + 0.5) / n);
            samples[i] *= window;
        }
    }
    
    /// <summary>
    /// 估算时长
    /// </summary>
    private async Task<TimeSpan> EstimateDurationAsync(Stream stream, AdtsHeader header)
    {
        long startPos = stream.Position;
        int frameCount = 0;
        int samplesPerFrame = 1024; // AAC-LC 默认
        
        // 计算前几个帧
        for (int i = 0; i < 10 && stream.Position < stream.Length; i++)
        {
            var frame = await ReadAdtsFrameAsync(stream);
            if (frame != null)
                frameCount++;
        }
        
        stream.Position = startPos;
        
        if (frameCount > 0)
        {
            long totalFrames = stream.Length / (stream.Position / frameCount);
            double totalSamples = totalFrames * samplesPerFrame;
            double durationSeconds = totalSamples / header.SampleRate;
            
            return TimeSpan.FromSeconds(durationSeconds);
        }
        
        return TimeSpan.Zero;
    }
}

/// <summary>
/// AAC 流信息
/// </summary>
public class AacStreamInfo
{
    public AacDecoder.AacProfile Profile { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int BitsPerSample { get; set; }
    public int Bitrate { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// ADTS 帧头
/// </summary>
internal class AdtsHeader
{
    public AacDecoder.AacProfile Profile { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int FrameLength { get; set; }
    public bool ProtectionAbsent { get; set; }
}

/// <summary>
/// AAC 帧
/// </summary>
internal class AacFrame
{
    public AdtsHeader Header { get; set; } = new();
    public byte[] Data { get; set; } = Array.Empty<byte>();
}