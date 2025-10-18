using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace EnderAudioAnalyzer.Decoders;

/// <summary>
/// ALAC (Apple Lossless Audio Codec) 解码器
/// 实现 Apple Lossless 音频编解码器规范
/// </summary>
public class AlacDecoder
{
    private AlacStreamInfo? _streamInfo;
    private readonly AlacBitReader _bitReader;
    
    // ALAC 魔数
    private const uint ALAC_MAGIC = 0x616C6163; // "alac"
    
    // 帧类型
    private enum FrameType
    {
        Uncompressed = 0,
        Compressed = 1
    }
    
    // 预测器类型
    private const int MAX_PREDICTOR_ORDER = 31;
    
    public AlacDecoder()
    {
        _bitReader = new AlacBitReader();
    }
    
    /// <summary>
    /// 解析 ALAC 文件（M4A/MP4 容器）
    /// </summary>
    public async Task<AlacStreamInfo> ParseAsync(Stream stream)
    {
        // 查找 'alac' 原子
        var alacAtom = await FindAtomAsync(stream, "alac");
        if (alacAtom == null)
            throw new InvalidDataException("未找到 ALAC 配置");
        
        _bitReader.Initialize(alacAtom);
        
        // 跳过版本和标志
        _bitReader.Skip(32);
        
        // 读取 ALAC 特定配置
        var frameLength = _bitReader.ReadBits(32);
        var compatibleVersion = _bitReader.ReadBits(8);
        var bitDepth = _bitReader.ReadBits(8);
        var pb = _bitReader.ReadBits(8); // 参数 pb
        var mb = _bitReader.ReadBits(8); // 参数 mb
        var kb = _bitReader.ReadBits(8); // 参数 kb
        var channels = _bitReader.ReadBits(8);
        var maxRun = _bitReader.ReadBits(16);
        var maxFrameBytes = _bitReader.ReadBits(32);
        var avgBitRate = _bitReader.ReadBits(32);
        var sampleRate = _bitReader.ReadBits(32);
        
        _streamInfo = new AlacStreamInfo
        {
            FrameLength = (int)frameLength,
            BitDepth = bitDepth,
            Channels = channels,
            SampleRate = (int)sampleRate,
            AvgBitRate = (int)avgBitRate,
            MaxFrameBytes = (int)maxFrameBytes,
            Pb = pb,
            Mb = mb,
            Kb = kb,
            Duration = TimeSpan.Zero
        };
        
        stream.Position = 0;
        return _streamInfo;
    }
    
    /// <summary>
    /// 解码 ALAC 音频数据
    /// </summary>
    public async Task<float[]> DecodeAsync(Stream stream, int maxSamples = -1)
    {
        if (_streamInfo == null)
        {
            await ParseAsync(stream);
            stream.Position = 0;
        }
        
        var samples = new List<float>();
        
        // 查找 'mdat' 原子（媒体数据）
        var mdatAtom = await FindAtomAsync(stream, "mdat");
        if (mdatAtom == null)
            return samples.ToArray();
        
        var mdatStream = new MemoryStream(mdatAtom);
        
        while (mdatStream.Position < mdatStream.Length)
        {
            if (maxSamples > 0 && samples.Count >= maxSamples)
                break;
            
            try
            {
                var frameSamples = await DecodeFrameAsync(mdatStream);
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
    /// 查找 MP4 原子
    /// </summary>
    private async Task<byte[]?> FindAtomAsync(Stream stream, string atomType)
    {
        stream.Position = 0;
        
        while (stream.Position < stream.Length)
        {
            var sizeBytes = new byte[4];
            await stream.ReadAsync(sizeBytes, 0, 4);
            
            if (BitConverter.IsLittleEndian)
                Array.Reverse(sizeBytes);
            
            var size = BitConverter.ToUInt32(sizeBytes, 0);
            
            var typeBytes = new byte[4];
            await stream.ReadAsync(typeBytes, 0, 4);
            
            var type = System.Text.Encoding.ASCII.GetString(typeBytes);
            
            if (type == atomType)
            {
                var data = new byte[size - 8];
                await stream.ReadAsync(data, 0, data.Length);
                return data;
            }
            else if (type == "moov" || type == "trak" || type == "mdia" || type == "minf" || type == "stbl")
            {
                // 容器原子，递归搜索
                var containerData = new byte[size - 8];
                await stream.ReadAsync(containerData, 0, containerData.Length);
                
                var containerStream = new MemoryStream(containerData);
                var result = await FindAtomAsync(containerStream, atomType);
                if (result != null)
                    return result;
            }
            else
            {
                // 跳过此原子
                stream.Position += size - 8;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 解码单个帧
    /// </summary>
    private async Task<float[]> DecodeFrameAsync(Stream stream)
    {
        // 读取帧大小
        var frameSizeBytes = new byte[4];
        await stream.ReadAsync(frameSizeBytes, 0, 4);
        
        if (BitConverter.IsLittleEndian)
            Array.Reverse(frameSizeBytes);
        
        var frameSize = BitConverter.ToInt32(frameSizeBytes, 0);
        
        // 读取帧数据
        var frameData = new byte[frameSize];
        await stream.ReadAsync(frameData, 0, frameSize);
        
        _bitReader.Initialize(frameData);
        
        // 读取帧头
        var frameType = _bitReader.ReadBits(3);
        var unusedBits = _bitReader.ReadBits(4);
        var hasSize = _bitReader.ReadBits(1) == 1;
        
        int numSamples = _streamInfo!.FrameLength;
        
        if (hasSize)
        {
            numSamples = _bitReader.ReadBits(32);
        }
        
        // 解码声道
        var channelData = new int[_streamInfo.Channels][];
        
        for (int ch = 0; ch < _streamInfo.Channels; ch++)
        {
            channelData[ch] = DecodeChannel(numSamples);
        }
        
        // 如果是立体声，执行中侧解码
        if (_streamInfo.Channels == 2)
        {
            MidSideDecode(channelData[0], channelData[1]);
        }
        
        // 转换为浮点样本
        var samples = new List<float>();
        for (int i = 0; i < numSamples; i++)
        {
            for (int ch = 0; ch < _streamInfo.Channels; ch++)
            {
                // 归一化到 -1.0 到 1.0
                float sample = channelData[ch][i] / (float)(1 << (_streamInfo.BitDepth - 1));
                samples.Add(sample);
            }
        }
        
        return samples.ToArray();
    }
    
    /// <summary>
    /// 解码单个声道
    /// </summary>
    private int[] DecodeChannel(int numSamples)
    {
        var samples = new int[numSamples];
        
        // 读取预测器参数
        var predictionType = _bitReader.ReadBits(4);
        var predictionQuantization = _bitReader.ReadBits(4);
        var riceModifier = _bitReader.ReadBits(3);
        var predictionOrder = _bitReader.ReadBits(5);
        
        // 读取预测器系数
        var coefficients = new int[predictionOrder];
        for (int i = 0; i < predictionOrder; i++)
        {
            coefficients[i] = _bitReader.ReadSignedBits(16);
        }
        
        // 解码残差
        var residuals = DecodeResiduals(numSamples, riceModifier);
        
        // 应用预测器
        ApplyPredictor(samples, residuals, coefficients, predictionQuantization);
        
        return samples;
    }
    
    /// <summary>
    /// 解码残差（Rice 编码）
    /// </summary>
    private int[] DecodeResiduals(int count, int riceModifier)
    {
        var residuals = new int[count];
        
        for (int i = 0; i < count; i++)
        {
            // Rice 参数
            int k = riceModifier;
            
            // 读取商（一元编码）
            int quotient = 0;
            while (_bitReader.ReadBits(1) == 0 && _bitReader.RemainingBits > 0)
            {
                quotient++;
            }
            
            // 读取余数
            int remainder = 0;
            if (k > 0 && _bitReader.RemainingBits >= k)
            {
                remainder = _bitReader.ReadBits(k);
            }
            
            // 组合值
            int value = (quotient << k) | remainder;
            
            // 解码符号
            residuals[i] = (value & 1) != 0 ? -(value >> 1) - 1 : (value >> 1);
        }
        
        return residuals;
    }
    
    /// <summary>
    /// 应用线性预测器
    /// </summary>
    private void ApplyPredictor(int[] output, int[] residuals, int[] coefficients, int shift)
    {
        int order = coefficients.Length;
        
        // 前几个样本直接使用残差
        for (int i = 0; i < Math.Min(order, output.Length); i++)
        {
            output[i] = residuals[i];
        }
        
        // 应用预测
        for (int i = order; i < output.Length; i++)
        {
            long prediction = 0;
            
            for (int j = 0; j < order; j++)
            {
                prediction += (long)coefficients[j] * output[i - j - 1];
            }
            
            // 量化和舍入
            prediction = (prediction + (1 << (shift - 1))) >> shift;
            
            // 添加残差
            output[i] = (int)(prediction + residuals[i]);
        }
    }
    
    /// <summary>
    /// 中侧解码（立体声）
    /// </summary>
    private void MidSideDecode(int[] left, int[] right)
    {
        for (int i = 0; i < left.Length; i++)
        {
            int mid = left[i];
            int side = right[i];
            
            // 重建左右声道
            left[i] = (mid + side) / 2;
            right[i] = (mid - side) / 2;
        }
    }
}

/// <summary>
/// ALAC 流信息
/// </summary>
public class AlacStreamInfo
{
    public int FrameLength { get; set; }
    public int BitDepth { get; set; }
    public int Channels { get; set; }
    public int SampleRate { get; set; }
    public int AvgBitRate { get; set; }
    public int MaxFrameBytes { get; set; }
    public int Pb { get; set; }
    public int Mb { get; set; }
    public int Kb { get; set; }
    public TimeSpan Duration { get; set; }
}