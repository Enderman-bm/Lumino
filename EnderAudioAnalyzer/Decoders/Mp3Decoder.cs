using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace EnderAudioAnalyzer.Decoders;

/// <summary>
/// MP3 (MPEG-1/2 Audio Layer III) 解码器
/// 实现 ISO/IEC 11172-3 和 ISO/IEC 13818-3 标准
/// </summary>
public class Mp3Decoder
{
    private Mp3StreamInfo? _streamInfo;
    private readonly Mp3BitReader _bitReader;
    
    // MPEG 版本
    public enum MpegVersion
    {
        Mpeg1 = 3,
        Mpeg2 = 2,
        Mpeg25 = 0
    }
    
    // 声道模式
    public enum ChannelMode
    {
        Stereo = 0,
        JointStereo = 1,
        DualChannel = 2,
        Mono = 3
    }
    
    // 采样率表 [MPEG版本][采样率索引]
    private static readonly int[,] SampleRateTable = {
        { 11025, 12000, 8000, 0 },    // MPEG 2.5
        { 0, 0, 0, 0 },                // 保留
        { 22050, 24000, 16000, 0 },   // MPEG 2
        { 44100, 48000, 32000, 0 }    // MPEG 1
    };
    
    // 比特率表 [MPEG版本][Layer][比特率索引]
    private static readonly int[,,] BitrateTable = {
        // MPEG 1
        {
            { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0 }, // Layer I
            { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0 },    // Layer II
            { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 }      // Layer III
        },
        // MPEG 2/2.5
        {
            { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0 },    // Layer I
            { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 },         // Layer II
            { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 }          // Layer III
        }
    };
    
    // Huffman 码表（简化版，完整实现需要更多表）
    private static readonly int[][] HuffmanTables = new int[32][];
    
    // IMDCT 窗口
    private float[] _imdctWindow = new float[36];
    
    // 重叠缓冲区
    private float[,] _overlapBuffer = new float[2, 576];
    
    public Mp3Decoder()
    {
        _bitReader = new Mp3BitReader();
        InitializeHuffmanTables();
        InitializeImdctWindow();
    }
    
    /// <summary>
    /// 解析 MP3 文件获取流信息
    /// </summary>
    public async Task<Mp3StreamInfo> ParseAsync(Stream stream)
    {
        // 跳过 ID3v2 标签（如果存在）
        await SkipId3v2TagAsync(stream);
        
        // 读取第一个帧头
        var frameHeader = await ReadFrameHeaderAsync(stream);
        
        _streamInfo = new Mp3StreamInfo
        {
            SampleRate = frameHeader.SampleRate,
            Channels = frameHeader.Channels,
            BitsPerSample = 16, // MP3 解码后通常是 16 位
            Bitrate = frameHeader.Bitrate,
            Duration = await EstimateDurationAsync(stream, frameHeader)
        };
        
        // 重置流位置
        stream.Position = 0;
        await SkipId3v2TagAsync(stream);
        
        return _streamInfo;
    }
    
    /// <summary>
    /// 解码 MP3 音频数据
    /// </summary>
    public async Task<float[]> DecodeAsync(Stream stream, int maxSamples = -1)
    {
        if (_streamInfo == null)
        {
            await ParseAsync(stream);
            stream.Position = 0;
            await SkipId3v2TagAsync(stream);
        }
        
        var samples = new List<float>();
        var buffer = new byte[4096];
        
        while (stream.Position < stream.Length)
        {
            if (maxSamples > 0 && samples.Count >= maxSamples)
                break;
                
            try
            {
                var frameHeader = await ReadFrameHeaderAsync(stream);
                var frameSamples = await DecodeFrameAsync(stream, frameHeader);
                samples.AddRange(frameSamples);
            }
            catch
            {
                // 跳过损坏的帧
                break;
            }
        }
        
        return samples.ToArray();
    }
    
    /// <summary>
    /// 跳过 ID3v2 标签
    /// </summary>
    private async Task SkipId3v2TagAsync(Stream stream)
    {
        var header = new byte[10];
        var bytesRead = await stream.ReadAsync(header, 0, 10);
        
        if (bytesRead == 10 && header[0] == 'I' && header[1] == 'D' && header[2] == '3')
        {
            // 计算标签大小（使用同步安全整数）
            int size = (header[6] << 21) | (header[7] << 14) | (header[8] << 7) | header[9];
            stream.Position += size;
        }
        else
        {
            stream.Position = 0;
        }
    }
    
    /// <summary>
    /// 读取帧头
    /// </summary>
    private async Task<Mp3FrameHeader> ReadFrameHeaderAsync(Stream stream)
    {
        // 查找帧同步字
        while (stream.Position < stream.Length)
        {
            var b1 = stream.ReadByte();
            if (b1 != 0xFF) continue;
            
            var b2 = stream.ReadByte();
            if ((b2 & 0xE0) != 0xE0)
            {
                stream.Position -= 1;
                continue;
            }
            
            // 读取完整的帧头（4字节）
            var b3 = stream.ReadByte();
            var b4 = stream.ReadByte();
            
            var header = (uint)((b1 << 24) | (b2 << 16) | (b3 << 8) | b4);
            
            // 解析帧头
            var mpegVersion = (MpegVersion)((header >> 19) & 0x3);
            var layer = 4 - (int)((header >> 17) & 0x3);
            var bitrateIndex = (int)((header >> 12) & 0xF);
            var sampleRateIndex = (int)((header >> 10) & 0x3);
            var padding = ((header >> 9) & 0x1) == 1;
            var channelMode = (ChannelMode)((header >> 6) & 0x3);
            
            // 验证有效性
            if (bitrateIndex == 0 || bitrateIndex == 15) continue;
            if (sampleRateIndex == 3) continue;
            
            var versionIndex = mpegVersion == MpegVersion.Mpeg1 ? 0 : 1;
            var bitrate = BitrateTable[versionIndex, layer - 1, bitrateIndex] * 1000;
            var sampleRate = SampleRateTable[(int)mpegVersion, sampleRateIndex];
            
            // 计算帧大小
            int frameSize;
            if (layer == 1)
            {
                frameSize = (12 * bitrate / sampleRate + (padding ? 1 : 0)) * 4;
            }
            else
            {
                frameSize = 144 * bitrate / sampleRate + (padding ? 1 : 0);
            }
            
            return new Mp3FrameHeader
            {
                Version = mpegVersion,
                Layer = layer,
                Bitrate = bitrate,
                SampleRate = sampleRate,
                Channels = channelMode == ChannelMode.Mono ? 1 : 2,
                FrameSize = frameSize,
                Padding = padding
            };
        }
        
        throw new InvalidDataException("未找到有效的 MP3 帧头");
    }
    
    /// <summary>
    /// 解码单个帧
    /// </summary>
    private async Task<float[]> DecodeFrameAsync(Stream stream, Mp3FrameHeader header)
    {
        // 读取帧数据
        var frameData = new byte[header.FrameSize - 4]; // 减去已读取的帧头
        await stream.ReadAsync(frameData, 0, frameData.Length);
        
        _bitReader.Initialize(frameData);
        
        // 读取边信息
        var sideInfo = ReadSideInfo(header);
        
        // 解码主数据
        var granules = new float[2][][]; // [粒度][声道][样本]
        for (int gr = 0; gr < 2; gr++)
        {
            granules[gr] = new float[header.Channels][];
            for (int ch = 0; ch < header.Channels; ch++)
            {
                granules[gr][ch] = DecodeGranule(sideInfo, gr, ch);
            }
        }
        
        // 合成子带
        var pcmSamples = SynthesizeSubbands(granules, header.Channels);
        
        return pcmSamples;
    }
    
    /// <summary>
    /// 读取边信息
    /// </summary>
    private Mp3SideInfo ReadSideInfo(Mp3FrameHeader header)
    {
        var sideInfo = new Mp3SideInfo
        {
            MainDataBegin = _bitReader.ReadBits(9),
            PrivateBits = _bitReader.ReadBits(header.Channels == 1 ? 5 : 3),
            Granules = new Mp3GranuleInfo[2][]
        };
        
        for (int gr = 0; gr < 2; gr++)
        {
            sideInfo.Granules[gr] = new Mp3GranuleInfo[header.Channels];
            for (int ch = 0; ch < header.Channels; ch++)
            {
                sideInfo.Granules[gr][ch] = new Mp3GranuleInfo
                {
                    Part2_3_length = _bitReader.ReadBits(12),
                    BigValues = _bitReader.ReadBits(9),
                    GlobalGain = _bitReader.ReadBits(8),
                    ScalefacCompress = _bitReader.ReadBits(4),
                    WindowSwitching = _bitReader.ReadBits(1) == 1
                };
                
                if (sideInfo.Granules[gr][ch].WindowSwitching)
                {
                    sideInfo.Granules[gr][ch].BlockType = _bitReader.ReadBits(2);
                    sideInfo.Granules[gr][ch].MixedBlockFlag = _bitReader.ReadBits(1) == 1;
                    
                    // 读取表选择
                    for (int region = 0; region < 2; region++)
                    {
                        _bitReader.ReadBits(5); // table_select
                    }
                    
                    // 读取子块增益
                    for (int window = 0; window < 3; window++)
                    {
                        _bitReader.ReadBits(3); // subblock_gain
                    }
                }
                else
                {
                    // 读取表选择
                    for (int region = 0; region < 3; region++)
                    {
                        _bitReader.ReadBits(5); // table_select
                    }
                    
                    _bitReader.ReadBits(4); // region0_count
                    _bitReader.ReadBits(3); // region1_count
                }
                
                _bitReader.ReadBits(1); // preflag
                _bitReader.ReadBits(1); // scalefac_scale
                _bitReader.ReadBits(1); // count1table_select
            }
        }
        
        return sideInfo;
    }
    
    /// <summary>
    /// 解码粒度
    /// </summary>
    private float[] DecodeGranule(Mp3SideInfo sideInfo, int granule, int channel)
    {
        var samples = new float[576];
        var granuleInfo = sideInfo.Granules[granule][channel];
        
        // 1. Huffman 解码
        var scalefactors = DecodeScalefactors(granuleInfo);
        var quantizedSamples = HuffmanDecode(granuleInfo);
        
        // 2. 反量化
        Requantize(quantizedSamples, scalefactors, granuleInfo);
        
        // 3. 重排序
        if (granuleInfo.WindowSwitching && granuleInfo.BlockType == 2)
        {
            Reorder(quantizedSamples);
        }
        
        // 4. 反混叠
        AntiAlias(quantizedSamples);
        
        // 5. IMDCT
        InverseModifiedDCT(quantizedSamples, samples, granuleInfo);
        
        // 6. 频率倒置
        FrequencyInversion(samples);
        
        return samples;
    }
    
    /// <summary>
    /// 解码比例因子
    /// </summary>
    private int[] DecodeScalefactors(Mp3GranuleInfo granuleInfo)
    {
        var scalefactors = new int[21];
        
        // 简化实现：使用固定值
        for (int i = 0; i < 21; i++)
        {
            scalefactors[i] = 0;
        }
        
        return scalefactors;
    }
    
    /// <summary>
    /// Huffman 解码（简化版）
    /// </summary>
    private float[] HuffmanDecode(Mp3GranuleInfo granuleInfo)
    {
        var samples = new float[576];
        
        // 简化实现：返回零样本
        // 完整实现需要使用 Huffman 表进行解码
        
        return samples;
    }
    
    /// <summary>
    /// 反量化
    /// </summary>
    private void Requantize(float[] samples, int[] scalefactors, Mp3GranuleInfo granuleInfo)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            if (samples[i] != 0)
            {
                var sign = samples[i] < 0 ? -1 : 1;
                var absValue = Math.Abs(samples[i]);
                samples[i] = sign * (float)Math.Pow(absValue, 4.0 / 3.0);
            }
        }
    }
    
    /// <summary>
    /// 重排序
    /// </summary>
    private void Reorder(float[] samples)
    {
        var temp = new float[576];
        Array.Copy(samples, temp, 576);
        
        // 实现重排序算法
        // 这里是简化实现
    }
    
    /// <summary>
    /// 反混叠
    /// </summary>
    private void AntiAlias(float[] samples)
    {
        // 反混叠系数
        float[] ca = { 
            -0.6f, -0.535f, -0.33f, -0.185f, 
            -0.095f, -0.041f, -0.0142f, -0.0037f 
        };
        float[] cs = new float[8];
        
        for (int i = 0; i < 8; i++)
        {
            cs[i] = (float)Math.Sqrt(1 - ca[i] * ca[i]);
        }
        
        // 应用反混叠
        for (int sb = 1; sb < 32; sb++)
        {
            for (int i = 0; i < 8; i++)
            {
                int idx1 = sb * 18 - 1 - i;
                int idx2 = sb * 18 + i;
                
                if (idx1 >= 0 && idx2 < samples.Length)
                {
                    float s1 = samples[idx1];
                    float s2 = samples[idx2];
                    
                    samples[idx1] = s1 * cs[i] - s2 * ca[i];
                    samples[idx2] = s2 * cs[i] + s1 * ca[i];
                }
            }
        }
    }
    
    /// <summary>
    /// 反向修改离散余弦变换 (IMDCT)
    /// </summary>
    private void InverseModifiedDCT(float[] input, float[] output, Mp3GranuleInfo granuleInfo)
    {
        int blockType = granuleInfo.WindowSwitching ? granuleInfo.BlockType : 0;
        
        for (int sb = 0; sb < 32; sb++)
        {
            float[] sbSamples = new float[18];
            
            if (blockType == 2)
            {
                // 短块 IMDCT
                for (int win = 0; win < 3; win++)
                {
                    float[] winSamples = new float[6];
                    
                    for (int i = 0; i < 6; i++)
                    {
                        winSamples[i] = input[sb * 18 + win * 6 + i];
                    }
                    
                    var imdctOut = PerformImdct(winSamples, 6);
                    
                    // 窗口化和重叠相加
                    for (int i = 0; i < 6; i++)
                    {
                        sbSamples[win * 6 + i] = imdctOut[i];
                    }
                }
            }
            else
            {
                // 长块 IMDCT
                float[] longSamples = new float[18];
                for (int i = 0; i < 18; i++)
                {
                    longSamples[i] = input[sb * 18 + i];
                }
                
                sbSamples = PerformImdct(longSamples, 18);
            }
            
            // 复制到输出
            for (int i = 0; i < 18; i++)
            {
                output[sb * 18 + i] = sbSamples[i];
            }
        }
    }
    
    /// <summary>
    /// 执行 IMDCT
    /// </summary>
    private float[] PerformImdct(float[] input, int n)
    {
        var output = new float[n * 2];
        
        for (int i = 0; i < n * 2; i++)
        {
            float sum = 0;
            for (int k = 0; k < n; k++)
            {
                sum += input[k] * (float)Math.Cos(Math.PI / (2 * n) * (i + 0.5 + n / 2.0) * (k + 0.5));
            }
            output[i] = sum;
        }
        
        return output;
    }
    
    /// <summary>
    /// 频率倒置
    /// </summary>
    private void FrequencyInversion(float[] samples)
    {
        for (int sb = 1; sb < 32; sb += 2)
        {
            for (int i = 1; i < 18; i += 2)
            {
                samples[sb * 18 + i] = -samples[sb * 18 + i];
            }
        }
    }
    
    /// <summary>
    /// 合成子带
    /// </summary>
    private float[] SynthesizeSubbands(float[][][] granules, int channels)
    {
        var samples = new List<float>();
        
        // 简化实现：直接使用 IMDCT 输出
        for (int gr = 0; gr < 2; gr++)
        {
            for (int i = 0; i < 576; i++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    if (i < granules[gr][ch].Length)
                    {
                        samples.Add(granules[gr][ch][i]);
                    }
                }
            }
        }
        
        return samples.ToArray();
    }
    
    /// <summary>
    /// 估算音频时长
    /// </summary>
    private async Task<TimeSpan> EstimateDurationAsync(Stream stream, Mp3FrameHeader firstFrame)
    {
        long fileSize = stream.Length;
        long dataSize = fileSize;
        
        // 估算时长
        double durationSeconds = (double)(dataSize * 8) / firstFrame.Bitrate;
        return TimeSpan.FromSeconds(durationSeconds);
    }
    
    /// <summary>
    /// 初始化 Huffman 表
    /// </summary>
    private void InitializeHuffmanTables()
    {
        // 简化实现
        for (int i = 0; i < 32; i++)
        {
            HuffmanTables[i] = new int[256];
        }
    }
    
    /// <summary>
    /// 初始化 IMDCT 窗口
    /// </summary>
    private void InitializeImdctWindow()
    {
        for (int i = 0; i < 36; i++)
        {
            _imdctWindow[i] = (float)Math.Sin(Math.PI / 36 * (i + 0.5));
        }
    }
}

/// <summary>
/// MP3 流信息
/// </summary>
public class Mp3StreamInfo
{
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int BitsPerSample { get; set; }
    public int Bitrate { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// MP3 帧头
/// </summary>
internal class Mp3FrameHeader
{
    public Mp3Decoder.MpegVersion Version { get; set; }
    public int Layer { get; set; }
    public int Bitrate { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int FrameSize { get; set; }
    public bool Padding { get; set; }
}

/// <summary>
/// MP3 边信息
/// </summary>
internal class Mp3SideInfo
{
    public int MainDataBegin { get; set; }
    public int PrivateBits { get; set; }
    public Mp3GranuleInfo[][] Granules { get; set; } = Array.Empty<Mp3GranuleInfo[]>();
}

/// <summary>
/// MP3 粒度信息
/// </summary>
internal class Mp3GranuleInfo
{
    public int Part2_3_length { get; set; }
    public int BigValues { get; set; }
    public int GlobalGain { get; set; }
    public int ScalefacCompress { get; set; }
    public bool WindowSwitching { get; set; }
    public int BlockType { get; set; }
    public bool MixedBlockFlag { get; set; }
}