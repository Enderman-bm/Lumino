using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace EnderAudioAnalyzer.Decoders;

/// <summary>
/// OPUS 解码器
/// 实现 RFC 6716 (OPUS Audio Codec) 和 RFC 7845 (Ogg Encapsulation)
/// </summary>
public class OpusDecoder
{
    private OpusStreamInfo? _streamInfo;
    private readonly OpusBitReader _bitReader;
    
    // OPUS 模式
    private enum OpusMode
    {
        Silk = 0,      // 语音编码
        Hybrid = 1,    // 混合模式
        Celt = 2       // 音频编码
    }
    
    // 带宽
    private enum OpusBandwidth
    {
        Narrowband = 0,     // 4 kHz
        Mediumband = 1,     // 6 kHz
        Wideband = 2,       // 8 kHz
        Superwideband = 3,  // 12 kHz
        Fullband = 4        // 20 kHz
    }
    
    // 帧大小（样本数）
    private static readonly int[] FrameSizes = { 
        120, 240, 480, 960, 1920, 2880 
    };
    
    // CELT 模式配置
    private int[] _celtModes = Array.Empty<int>();
    
    public OpusDecoder()
    {
        _bitReader = new OpusBitReader();
    }
    
    /// <summary>
    /// 解析 OGG OPUS 文件
    /// </summary>
    public async Task<OpusStreamInfo> ParseAsync(Stream stream)
    {
        // 读取第一个 OGG 页（OpusHead）
        var headPage = await ReadOggPageAsync(stream);
        if (headPage == null)
            throw new InvalidDataException("无效的 OGG 文件");
        
        _bitReader.Initialize(headPage.Data);
        
        // 读取 "OpusHead" 标识
        var magic = new byte[8];
        for (int i = 0; i < 8; i++)
            magic[i] = (byte)_bitReader.ReadBits(8);
        
        if (Encoding.ASCII.GetString(magic) != "OpusHead")
            throw new InvalidDataException("无效的 OPUS 头");
        
        // 读取版本
        var version = _bitReader.ReadBits(8);
        
        // 读取声道数
        var channels = _bitReader.ReadBits(8);
        
        // 读取预跳过样本数
        var preSkip = _bitReader.ReadBits(16);
        
        // 读取输入采样率
        var inputSampleRate = _bitReader.ReadBits(32);
        
        // 读取输出增益
        var outputGain = _bitReader.ReadSignedBits(16);
        
        // 读取声道映射族
        var channelMappingFamily = _bitReader.ReadBits(8);
        
        _streamInfo = new OpusStreamInfo
        {
            Version = version,
            Channels = channels,
            PreSkip = preSkip,
            InputSampleRate = inputSampleRate,
            OutputGain = outputGain,
            ChannelMappingFamily = channelMappingFamily,
            SampleRate = 48000, // OPUS 固定使用 48kHz
            BitsPerSample = 16,
            Duration = TimeSpan.Zero
        };
        
        // 读取 OpusTags 页
        var tagsPage = await ReadOggPageAsync(stream);
        
        stream.Position = 0;
        return _streamInfo;
    }
    
    /// <summary>
    /// 解码 OPUS 音频数据
    /// </summary>
    public async Task<float[]> DecodeAsync(Stream stream, int maxSamples = -1)
    {
        if (_streamInfo == null)
        {
            await ParseAsync(stream);
            stream.Position = 0;
        }
        
        var samples = new List<float>();
        
        // 跳过头部页面
        await ReadOggPageAsync(stream); // OpusHead
        await ReadOggPageAsync(stream); // OpusTags
        
        // 解码音频页面
        while (stream.Position < stream.Length)
        {
            if (maxSamples > 0 && samples.Count >= maxSamples)
                break;
            
            var page = await ReadOggPageAsync(stream);
            if (page == null) break;
            
            var pageSamples = DecodeOpusPacket(page.Data);
            samples.AddRange(pageSamples);
        }
        
        return samples.ToArray();
    }
    
    /// <summary>
    /// 读取 OGG 页
    /// </summary>
    private async Task<OggPage?> ReadOggPageAsync(Stream stream)
    {
        // 查找 OGG 页同步模式 "OggS"
        while (stream.Position < stream.Length)
        {
            var b1 = stream.ReadByte();
            if (b1 != 'O') continue;
            
            var b2 = stream.ReadByte();
            if (b2 != 'g') { stream.Position -= 1; continue; }
            
            var b3 = stream.ReadByte();
            if (b3 != 'g') { stream.Position -= 2; continue; }
            
            var b4 = stream.ReadByte();
            if (b4 != 'S') { stream.Position -= 3; continue; }
            
            // 读取页头
            var version = stream.ReadByte();
            var headerType = stream.ReadByte();
            
            var granulePosition = new byte[8];
            await stream.ReadAsync(granulePosition, 0, 8);
            
            var serialNumber = new byte[4];
            await stream.ReadAsync(serialNumber, 0, 4);
            
            var pageSequence = new byte[4];
            await stream.ReadAsync(pageSequence, 0, 4);
            
            var checksum = new byte[4];
            await stream.ReadAsync(checksum, 0, 4);
            
            var segmentCount = stream.ReadByte();
            var segmentTable = new byte[segmentCount];
            await stream.ReadAsync(segmentTable, 0, segmentCount);
            
            // 计算数据大小
            int dataSize = 0;
            for (int i = 0; i < segmentCount; i++)
                dataSize += segmentTable[i];
            
            // 读取数据
            var data = new byte[dataSize];
            await stream.ReadAsync(data, 0, dataSize);
            
            return new OggPage
            {
                Version = version,
                HeaderType = headerType,
                Data = data
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// 解码 OPUS 包
    /// </summary>
    private float[] DecodeOpusPacket(byte[] data)
    {
        if (data.Length == 0)
            return Array.Empty<float>();
        
        _bitReader.Initialize(data);
        
        // 解析 TOC (Table of Contents)
        var toc = _bitReader.ReadBits(8);
        
        // 提取配置信息
        var config = (toc >> 3) & 0x1F;
        var stereo = (toc & 0x04) != 0;
        var frameCount = toc & 0x03;
        
        // 确定帧数
        int numFrames = 1;
        if (frameCount == 3)
        {
            numFrames = _bitReader.ReadBits(8) & 0x3F;
        }
        else if (frameCount > 0)
        {
            numFrames = 2;
        }
        
        // 解码所有帧
        var allSamples = new List<float>();
        
        for (int i = 0; i < numFrames; i++)
        {
            var frameSamples = DecodeOpusFrame(config, stereo);
            allSamples.AddRange(frameSamples);
        }
        
        return allSamples.ToArray();
    }
    
    /// <summary>
    /// 解码 OPUS 帧
    /// </summary>
    private float[] DecodeOpusFrame(int config, bool stereo)
    {
        // 确定模式和带宽
        var mode = GetOpusMode(config);
        var bandwidth = GetOpusBandwidth(config);
        var frameSize = GetFrameSize(config);
        
        int channels = stereo ? 2 : 1;
        var samples = new float[frameSize * channels];
        
        // 根据模式选择解码器
        switch (mode)
        {
            case OpusMode.Silk:
                DecodeSilkFrame(samples, frameSize, channels);
                break;
            
            case OpusMode.Celt:
                DecodeCeltFrame(samples, frameSize, channels, bandwidth);
                break;
            
            case OpusMode.Hybrid:
                // 混合模式：SILK 低频 + CELT 高频
                DecodeSilkFrame(samples, frameSize, channels);
                DecodeCeltFrame(samples, frameSize, channels, bandwidth);
                break;
        }
        
        return samples;
    }
    
    /// <summary>
    /// 解码 SILK 帧（语音编码）
    /// </summary>
    private void DecodeSilkFrame(float[] output, int frameSize, int channels)
    {
        // SILK 解码的简化实现
        // 实际实现需要完整的 SILK 解码器
        
        // 生成静音或简单的正弦波用于测试
        for (int i = 0; i < frameSize; i++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                output[i * channels + ch] = 0;
            }
        }
    }
    
    /// <summary>
    /// 解码 CELT 帧（音频编码）
    /// </summary>
    private void DecodeCeltFrame(float[] output, int frameSize, int channels, OpusBandwidth bandwidth)
    {
        // CELT 解码的简化实现
        // 实际实现需要完整的 CELT 解码器，包括 MDCT、PVQ 等
        
        int bands = GetCeltBands(bandwidth);
        
        // 读取能量包络
        var energies = new float[bands * channels];
        for (int i = 0; i < energies.Length; i++)
        {
            if (_bitReader.RemainingBits >= 8)
                energies[i] = _bitReader.ReadBits(8) / 255.0f;
        }
        
        // 读取频谱数据（简化）
        var spectrum = new float[frameSize * channels];
        for (int i = 0; i < Math.Min(spectrum.Length, _bitReader.RemainingBits / 4); i++)
        {
            if (_bitReader.RemainingBits >= 4)
                spectrum[i] = _bitReader.ReadSignedBits(4) / 8.0f;
        }
        
        // IMDCT（简化）
        PerformImdct(spectrum, output, frameSize, channels);
    }
    
    /// <summary>
    /// 执行 IMDCT
    /// </summary>
    private void PerformImdct(float[] spectrum, float[] output, int frameSize, int channels)
    {
        for (int ch = 0; ch < channels; ch++)
        {
            for (int i = 0; i < frameSize; i++)
            {
                float sum = 0;
                for (int k = 0; k < frameSize / 2; k++)
                {
                    int specIndex = ch * frameSize / 2 + k;
                    if (specIndex < spectrum.Length)
                    {
                        sum += spectrum[specIndex] * (float)Math.Cos(
                            Math.PI / frameSize * (i + 0.5 + frameSize / 4.0) * (k + 0.5)
                        );
                    }
                }
                output[i * channels + ch] = sum;
            }
        }
    }
    
    /// <summary>
    /// 获取 OPUS 模式
    /// </summary>
    private OpusMode GetOpusMode(int config)
    {
        if (config < 12)
            return OpusMode.Silk;
        else if (config < 16)
            return OpusMode.Hybrid;
        else
            return OpusMode.Celt;
    }
    
    /// <summary>
    /// 获取带宽
    /// </summary>
    private OpusBandwidth GetOpusBandwidth(int config)
    {
        if (config < 12)
        {
            // SILK 模式
            return config < 4 ? OpusBandwidth.Narrowband :
                   config < 8 ? OpusBandwidth.Mediumband :
                   OpusBandwidth.Wideband;
        }
        else if (config < 16)
        {
            // Hybrid 模式
            return config < 14 ? OpusBandwidth.Superwideband : OpusBandwidth.Fullband;
        }
        else
        {
            // CELT 模式
            return config < 20 ? OpusBandwidth.Narrowband :
                   config < 24 ? OpusBandwidth.Wideband :
                   config < 28 ? OpusBandwidth.Superwideband :
                   OpusBandwidth.Fullband;
        }
    }
    
    /// <summary>
    /// 获取帧大小
    /// </summary>
    private int GetFrameSize(int config)
    {
        // 简化：返回常用的 960 样本（20ms @ 48kHz）
        return 960;
    }
    
    /// <summary>
    /// 获取 CELT 频段数
    /// </summary>
    private int GetCeltBands(OpusBandwidth bandwidth)
    {
        return bandwidth switch
        {
            OpusBandwidth.Narrowband => 13,
            OpusBandwidth.Mediumband => 15,
            OpusBandwidth.Wideband => 17,
            OpusBandwidth.Superwideband => 19,
            OpusBandwidth.Fullband => 21,
            _ => 21
        };
    }
}

/// <summary>
/// OPUS 流信息
/// </summary>
public class OpusStreamInfo
{
    public int Version { get; set; }
    public int Channels { get; set; }
    public int PreSkip { get; set; }
    public int InputSampleRate { get; set; }
    public int OutputGain { get; set; }
    public int ChannelMappingFamily { get; set; }
    public int SampleRate { get; set; }
    public int BitsPerSample { get; set; }
    public TimeSpan Duration { get; set; }
}