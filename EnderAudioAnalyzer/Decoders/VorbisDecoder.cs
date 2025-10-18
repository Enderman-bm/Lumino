using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace EnderAudioAnalyzer.Decoders;

/// <summary>
/// OGG Vorbis 解码器
/// 实现 Xiph.Org Vorbis I 规范
/// </summary>
public class VorbisDecoder
{
    private VorbisStreamInfo? _streamInfo;
    private readonly VorbisBitReader _bitReader;
    
    // Vorbis 包类型
    private enum PacketType
    {
        Identification = 1,
        Comment = 3,
        Setup = 5,
        Audio = 0
    }
    
    // 窗口类型
    private enum WindowType
    {
        Short = 0,
        Long = 1
    }
    
    // MDCT 窗口
    private float[] _windowLong = Array.Empty<float>();
    private float[] _windowShort = Array.Empty<float>();
    
    // 码本
    private VorbisCodebook[] _codebooks = Array.Empty<VorbisCodebook>();
    
    // Floor 配置
    private VorbisFloor[] _floors = Array.Empty<VorbisFloor>();
    
    // Residue 配置
    private VorbisResidue[] _residues = Array.Empty<VorbisResidue>();
    
    // Mapping 配置
    private VorbisMapping[] _mappings = Array.Empty<VorbisMapping>();
    
    // Mode 配置
    private VorbisMode[] _modes = Array.Empty<VorbisMode>();
    
    public VorbisDecoder()
    {
        _bitReader = new VorbisBitReader();
    }
    
    /// <summary>
    /// 解析 OGG Vorbis 文件
    /// </summary>
    public async Task<VorbisStreamInfo> ParseAsync(Stream stream)
    {
        // 读取 OGG 页头
        var firstPage = await ReadOggPageAsync(stream);
        if (firstPage == null)
            throw new InvalidDataException("无效的 OGG 文件");
        
        // 解析 Vorbis Identification 头
        _bitReader.Initialize(firstPage.Data);
        
        var packetType = _bitReader.ReadBits(8);
        if (packetType != (int)PacketType.Identification)
            throw new InvalidDataException("无效的 Vorbis 包类型");
        
        // 读取 "vorbis" 标识
        var vorbisString = new byte[6];
        for (int i = 0; i < 6; i++)
            vorbisString[i] = (byte)_bitReader.ReadBits(8);
        
        if (Encoding.ASCII.GetString(vorbisString) != "vorbis")
            throw new InvalidDataException("无效的 Vorbis 标识");
        
        // 读取版本
        var version = _bitReader.ReadBits(32);
        var channels = _bitReader.ReadBits(8);
        var sampleRate = _bitReader.ReadBits(32);
        var bitrateMaximum = _bitReader.ReadBits(32);
        var bitrateNominal = _bitReader.ReadBits(32);
        var bitrateMinimum = _bitReader.ReadBits(32);
        
        var blocksize0 = 1 << _bitReader.ReadBits(4);
        var blocksize1 = 1 << _bitReader.ReadBits(4);
        
        _streamInfo = new VorbisStreamInfo
        {
            Version = version,
            Channels = channels,
            SampleRate = sampleRate,
            BitsPerSample = 16,
            BitrateNominal = bitrateNominal,
            BlockSize0 = blocksize0,
            BlockSize1 = blocksize1,
            Duration = TimeSpan.Zero
        };
        
        // 初始化窗口
        InitializeWindows(blocksize0, blocksize1);
        
        // 读取 Comment 头
        var commentPage = await ReadOggPageAsync(stream);
        
        // 读取 Setup 头
        var setupPage = await ReadOggPageAsync(stream);
        if (setupPage != null)
        {
            ParseSetupHeader(setupPage.Data);
        }
        
        stream.Position = 0;
        return _streamInfo;
    }
    
    /// <summary>
    /// 解码 Vorbis 音频数据
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
        await ReadOggPageAsync(stream); // Identification
        await ReadOggPageAsync(stream); // Comment
        await ReadOggPageAsync(stream); // Setup
        
        // 解码音频页面
        while (stream.Position < stream.Length)
        {
            if (maxSamples > 0 && samples.Count >= maxSamples)
                break;
            
            var page = await ReadOggPageAsync(stream);
            if (page == null) break;
            
            var pageSamples = DecodeAudioPacket(page.Data);
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
    /// 解析 Setup 头
    /// </summary>
    private void ParseSetupHeader(byte[] data)
    {
        _bitReader.Initialize(data);
        
        // 跳过包类型和标识
        _bitReader.ReadBits(8);
        for (int i = 0; i < 6; i++)
            _bitReader.ReadBits(8);
        
        // 读取码本数量
        var codebookCount = _bitReader.ReadBits(8) + 1;
        _codebooks = new VorbisCodebook[codebookCount];
        
        for (int i = 0; i < codebookCount; i++)
        {
            _codebooks[i] = ReadCodebook();
        }
        
        // 读取 Floor、Residue、Mapping、Mode 配置
        // 简化实现：跳过详细解析
    }
    
    /// <summary>
    /// 读取码本
    /// </summary>
    private VorbisCodebook ReadCodebook()
    {
        var codebook = new VorbisCodebook();
        
        // 读取同步模式
        var sync = _bitReader.ReadBits(24);
        if (sync != 0x564342) // "BCV"
            throw new InvalidDataException("无效的码本同步模式");
        
        codebook.Dimensions = _bitReader.ReadBits(16);
        codebook.Entries = _bitReader.ReadBits(24);
        
        var ordered = _bitReader.ReadBits(1) == 1;
        
        // 读取码字长度
        codebook.Lengths = new int[codebook.Entries];
        
        if (ordered)
        {
            var currentEntry = 0;
            var currentLength = _bitReader.ReadBits(5) + 1;
            
            while (currentEntry < codebook.Entries)
            {
                var number = _bitReader.ReadBits((int)Math.Floor(Math.Log2(codebook.Entries - currentEntry)) + 1);
                
                for (int i = 0; i < number && currentEntry < codebook.Entries; i++)
                {
                    codebook.Lengths[currentEntry++] = currentLength;
                }
                
                currentLength++;
            }
        }
        else
        {
            var sparse = _bitReader.ReadBits(1) == 1;
            
            for (int i = 0; i < codebook.Entries; i++)
            {
                if (sparse)
                {
                    var flag = _bitReader.ReadBits(1) == 1;
                    if (flag)
                        codebook.Lengths[i] = _bitReader.ReadBits(5) + 1;
                }
                else
                {
                    codebook.Lengths[i] = _bitReader.ReadBits(5) + 1;
                }
            }
        }
        
        return codebook;
    }
    
    /// <summary>
    /// 解码音频包
    /// </summary>
    private float[] DecodeAudioPacket(byte[] data)
    {
        _bitReader.Initialize(data);
        
        // 检查包类型（音频包的第一位应该是 0）
        var packetType = _bitReader.ReadBits(1);
        if (packetType != 0)
            return Array.Empty<float>();
        
        // 读取模式号
        var modeNumber = _bitReader.ReadBits(GetModeBits());
        if (modeNumber >= _modes.Length)
            return Array.Empty<float>();
        
        var mode = _modes[modeNumber];
        var blockSize = mode.BlockFlag ? _streamInfo!.BlockSize1 : _streamInfo!.BlockSize0;
        
        // 解码频谱数据
        var spectrum = new float[_streamInfo.Channels][];
        for (int ch = 0; ch < _streamInfo.Channels; ch++)
        {
            spectrum[ch] = new float[blockSize / 2];
        }
        
        // Floor 解码（简化）
        DecodeFloor(spectrum, mode);
        
        // Residue 解码（简化）
        DecodeResidue(spectrum, mode);
        
        // IMDCT
        var timeDomain = PerformImdct(spectrum, blockSize);
        
        // 窗口化
        ApplyWindow(timeDomain, blockSize);
        
        // 交织声道
        var output = new List<float>();
        for (int i = 0; i < blockSize; i++)
        {
            for (int ch = 0; ch < _streamInfo.Channels; ch++)
            {
                if (i < timeDomain[ch].Length)
                    output.Add(timeDomain[ch][i]);
            }
        }
        
        return output.ToArray();
    }
    
    /// <summary>
    /// 解码 Floor
    /// </summary>
    private void DecodeFloor(float[][] spectrum, VorbisMode mode)
    {
        // 简化实现：使用单位增益
        for (int ch = 0; ch < spectrum.Length; ch++)
        {
            for (int i = 0; i < spectrum[ch].Length; i++)
            {
                spectrum[ch][i] = 1.0f;
            }
        }
    }
    
    /// <summary>
    /// 解码 Residue
    /// </summary>
    private void DecodeResidue(float[][] spectrum, VorbisMode mode)
    {
        // 简化实现：生成静音
        // 完整实现需要使用码本进行 VQ 解码
    }
    
    /// <summary>
    /// 执行 IMDCT
    /// </summary>
    private float[][] PerformImdct(float[][] spectrum, int blockSize)
    {
        var output = new float[spectrum.Length][];
        
        for (int ch = 0; ch < spectrum.Length; ch++)
        {
            output[ch] = new float[blockSize];
            
            for (int i = 0; i < blockSize; i++)
            {
                float sum = 0;
                for (int k = 0; k < blockSize / 2; k++)
                {
                    sum += spectrum[ch][k] * (float)Math.Cos(
                        Math.PI / blockSize * (i + 0.5 + blockSize / 4.0) * (k + 0.5)
                    );
                }
                output[ch][i] = sum;
            }
        }
        
        return output;
    }
    
    /// <summary>
    /// 应用窗口函数
    /// </summary>
    private void ApplyWindow(float[][] samples, int blockSize)
    {
        var window = blockSize == _streamInfo!.BlockSize0 ? _windowShort : _windowLong;
        
        for (int ch = 0; ch < samples.Length; ch++)
        {
            for (int i = 0; i < Math.Min(blockSize, window.Length); i++)
            {
                samples[ch][i] *= window[i];
            }
        }
    }
    
    /// <summary>
    /// 初始化窗口
    /// </summary>
    private void InitializeWindows(int shortSize, int longSize)
    {
        _windowShort = new float[shortSize];
        _windowLong = new float[longSize];
        
        // Vorbis 使用的窗口函数
        for (int i = 0; i < shortSize; i++)
        {
            _windowShort[i] = (float)Math.Sin(0.5 * Math.PI * 
                Math.Pow(Math.Sin((i + 0.5) / shortSize * 0.5 * Math.PI), 2));
        }
        
        for (int i = 0; i < longSize; i++)
        {
            _windowLong[i] = (float)Math.Sin(0.5 * Math.PI * 
                Math.Pow(Math.Sin((i + 0.5) / longSize * 0.5 * Math.PI), 2));
        }
    }
    
    /// <summary>
    /// 获取模式位数
    /// </summary>
    private int GetModeBits()
    {
        if (_modes.Length <= 1) return 1;
        return (int)Math.Ceiling(Math.Log2(_modes.Length));
    }
}

/// <summary>
/// Vorbis 流信息
/// </summary>
public class VorbisStreamInfo
{
    public int Version { get; set; }
    public int Channels { get; set; }
    public int SampleRate { get; set; }
    public int BitsPerSample { get; set; }
    public int BitrateNominal { get; set; }
    public int BlockSize0 { get; set; }
    public int BlockSize1 { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// OGG 页
/// </summary>
internal class OggPage
{
    public int Version { get; set; }
    public int HeaderType { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Vorbis 码本
/// </summary>
internal class VorbisCodebook
{
    public int Dimensions { get; set; }
    public int Entries { get; set; }
    public int[] Lengths { get; set; } = Array.Empty<int>();
}

/// <summary>
/// Vorbis Floor
/// </summary>
internal class VorbisFloor
{
    public int Type { get; set; }
}

/// <summary>
/// Vorbis Residue
/// </summary>
internal class VorbisResidue
{
    public int Type { get; set; }
}

/// <summary>
/// Vorbis Mapping
/// </summary>
internal class VorbisMapping
{
    public int Type { get; set; }
}

/// <summary>
/// Vorbis Mode
/// </summary>
internal class VorbisMode
{
    public bool BlockFlag { get; set; }
    public int Mapping { get; set; }
}