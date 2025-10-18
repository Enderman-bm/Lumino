using System;
using System.IO;
using System.Text;
using EnderDebugger;

namespace EnderAudioAnalyzer.Decoders
{
    /// <summary>
    /// FLAC音频解码器
    /// 参考FLAC格式规范: https://xiph.org/flac/format.html
    /// </summary>
    public class FlacDecoder
    {
        private readonly EnderLogger _logger;
        private FlacStreamInfo _streamInfo;
        
        public FlacDecoder()
        {
            _logger = new EnderLogger("FlacDecoder");
        }

        /// <summary>
        /// 解码FLAC文件
        /// </summary>
        public FlacAudioData Decode(string filePath)
        {
            try
            {
                _logger.Info("FlacDecoder", $"开始解码FLAC文件: {filePath}");
                
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var reader = new FlacBitReader(fileStream);

                // 验证FLAC标识
                if (!ValidateFlacSignature(reader))
                {
                    throw new InvalidDataException("不是有效的FLAC文件");
                }

                // 读取元数据块
                ReadMetadataBlocks(reader);

                // 读取音频帧
                var samples = ReadAudioFrames(reader);

                _logger.Info("FlacDecoder", $"FLAC解码完成: {samples.Length} 个样本");

                return new FlacAudioData
                {
                    Samples = samples,
                    SampleRate = _streamInfo.SampleRate,
                    Channels = _streamInfo.Channels,
                    BitsPerSample = _streamInfo.BitsPerSample,
                    TotalSamples = _streamInfo.TotalSamples
                };
            }
            catch (Exception ex)
            {
                _logger.Error("FlacDecoder", $"FLAC解码失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 验证FLAC文件标识
        /// </summary>
        private bool ValidateFlacSignature(FlacBitReader reader)
        {
            try
            {
                // FLAC文件以"fLaC"开头(0x66 0x4C 0x61 0x43)
                byte[] signature = reader.ReadBytes(4);
                if (signature == null || signature.Length < 4)
                    return false;
                    
                return signature[0] == 0x66 &&
                       signature[1] == 0x4C &&
                       signature[2] == 0x61 &&
                       signature[3] == 0x43;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 读取所有元数据块
        /// </summary>
        private void ReadMetadataBlocks(FlacBitReader reader)
        {
            bool isLastBlock = false;
            
            while (!isLastBlock)
            {
                // 读取块头（1位last标志 + 7位块类型 + 24位块长度）
                isLastBlock = reader.ReadBit();
                int blockType = reader.ReadBits(7);
                int blockLength = reader.ReadBits(24);

                _logger.Debug("FlacDecoder", $"元数据块: 类型={blockType}, 长度={blockLength}, 最后={isLastBlock}");

                switch (blockType)
                {
                    case 0: // STREAMINFO
                        ReadStreamInfo(reader);
                        break;
                    case 1: // PADDING
                        reader.SkipBytes(blockLength);
                        break;
                    case 2: // APPLICATION
                        reader.SkipBytes(blockLength);
                        break;
                    case 3: // SEEKTABLE
                        reader.SkipBytes(blockLength);
                        break;
                    case 4: // VORBIS_COMMENT
                        reader.SkipBytes(blockLength);
                        break;
                    case 5: // CUESHEET
                        reader.SkipBytes(blockLength);
                        break;
                    case 6: // PICTURE
                        reader.SkipBytes(blockLength);
                        break;
                    default:
                        reader.SkipBytes(blockLength);
                        break;
                }
            }
        }

        /// <summary>
        /// 读取STREAMINFO元数据块
        /// </summary>
        private void ReadStreamInfo(FlacBitReader reader)
        {
            _streamInfo = new FlacStreamInfo
            {
                MinBlockSize = reader.ReadBits(16),
                MaxBlockSize = reader.ReadBits(16),
                MinFrameSize = reader.ReadBits(24),
                MaxFrameSize = reader.ReadBits(24),
                SampleRate = reader.ReadBits(20),
                Channels = reader.ReadBits(3) + 1,
                BitsPerSample = reader.ReadBits(5) + 1,
                TotalSamples = reader.ReadBitsLong(36)
            };

            // MD5签名（16字节）
            reader.SkipBytes(16);

            _logger.Info("FlacDecoder", 
                $"StreamInfo: 采样率={_streamInfo.SampleRate}Hz, " +
                $"通道={_streamInfo.Channels}, 位深={_streamInfo.BitsPerSample}, " +
                $"总样本={_streamInfo.TotalSamples}");
        }

        /// <summary>
        /// 读取所有音频帧
        /// </summary>
        private float[] ReadAudioFrames(FlacBitReader reader)
        {
            var samples = new float[_streamInfo.TotalSamples * _streamInfo.Channels];
            long samplesRead = 0;

            while (samplesRead < _streamInfo.TotalSamples)
            {
                try
                {
                    var frameSamples = ReadFrame(reader);
                    if (frameSamples == null || frameSamples.Length == 0)
                        break;

                    // 复制样本到输出数组
                    int samplesToCopy = (int)Math.Min(frameSamples.Length, 
                        samples.Length - samplesRead * _streamInfo.Channels);
                    Array.Copy(frameSamples, 0, samples, samplesRead * _streamInfo.Channels, samplesToCopy);
                    
                    samplesRead += frameSamples.Length / _streamInfo.Channels;
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }

            _logger.Debug("FlacDecoder", $"读取了 {samplesRead} 个样本帧");
            return samples;
        }

        /// <summary>
        /// 读取单个音频帧
        /// </summary>
        private float[] ReadFrame(FlacBitReader reader)
        {
            try
            {
                // 同步到帧边界
                if (!SyncToFrame(reader))
                    return null;

                // 读取帧头
                var header = ReadFrameHeader(reader);
                
                if (header == null || header.BlockSize <= 0 || header.Channels <= 0)
                    return null;
                
                // 读取子帧数据
                int[][] channelData = new int[header.Channels][];
                for (int ch = 0; ch < header.Channels; ch++)
                {
                    channelData[ch] = ReadSubframe(reader, header.BlockSize, header.BitsPerSample, header.ChannelAssignment, ch);
                }

                // 处理通道编码模式（左右、侧边、中间编码）
                DecodeChannelAssignment(channelData, header);

                // 跳过帧尾CRC
                reader.AlignToByte();
                reader.ReadBits(16); // CRC-16

                // 交织通道数据并转换为float
                return InterleaveAndConvert(channelData, header.BlockSize, header.Channels);
            }
            catch (Exception ex)
            {
                _logger.Warn("FlacDecoder", $"读取帧失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 同步到帧起始位置
        /// </summary>
        private bool SyncToFrame(FlacBitReader reader)
        {
            // FLAC帧以14位同步码开始: 11111111111110
            int maxAttempts = 8192; // 最多尝试8192位
            int attempts = 0;
            
            try
            {
                while (attempts < maxAttempts)
                {
                    if (reader.IsEndOfStream())
                        return false;

                    int sync = reader.PeekBits(14);
                    if (sync == 0x3FFE) // 11111111111110
                    {
                        return true;
                    }
                    
                    reader.ReadBit(); // 前进1位继续搜索
                    attempts++;
                }
            }
            catch
            {
                return false;
            }
            
            _logger.Warn("FlacDecoder", "帧同步失败：超过最大尝试次数");
            return false;
        }

        /// <summary>
        /// 读取帧头
        /// </summary>
        private FlacFrameHeader ReadFrameHeader(FlacBitReader reader)
        {
            var header = new FlacFrameHeader();

            // 同步码（14位）
            reader.ReadBits(14);

            // 保留位（1位）和阻塞策略（1位）
            reader.ReadBit();
            header.BlockingStrategy = reader.ReadBit();

            // 块大小（4位）
            int blockSizeCode = reader.ReadBits(4);
            header.BlockSize = DecodeBlockSize(blockSizeCode, reader);

            // 采样率（4位）
            int sampleRateCode = reader.ReadBits(4);
            header.SampleRate = DecodeSampleRate(sampleRateCode, reader);

            // 通道分配（4位）
            int channelCode = reader.ReadBits(4);
            header.ChannelAssignment = channelCode;
            header.Channels = DecodeChannels(channelCode);

            // 样本位深（3位）
            int bitsCode = reader.ReadBits(3);
            header.BitsPerSample = DecodeBitsPerSample(bitsCode);

            // 保留位（1位）
            reader.ReadBit();

            // 帧编号或样本编号（UTF-8编码）
            ReadUtf8Number(reader);

            // 读取扩展的块大小或采样率（如果需要）
            if (blockSizeCode == 6)
                header.BlockSize = reader.ReadBits(8) + 1;
            else if (blockSizeCode == 7)
                header.BlockSize = reader.ReadBits(16) + 1;

            if (sampleRateCode == 12)
                header.SampleRate = reader.ReadBits(8) * 1000;
            else if (sampleRateCode == 13)
                header.SampleRate = reader.ReadBits(16);
            else if (sampleRateCode == 14)
                header.SampleRate = reader.ReadBits(16) * 10;

            // 头部CRC-8
            reader.ReadBits(8);

            return header;
        }

        /// <summary>
        /// 读取子帧数据
        /// </summary>
        private int[] ReadSubframe(FlacBitReader reader, int blockSize, int bitsPerSample, int channelAssignment, int channelIndex)
        {
            // 子帧头（1位填充 + 6位类型 + 1位wasted bits标志）
            reader.ReadBit(); // 填充位
            int subframeType = reader.ReadBits(6);
            bool hasWastedBits = reader.ReadBit();

            // 根据通道编码调整位深度
            int effectiveBitsPerSample = bitsPerSample;
            if (channelAssignment == 8) // 左-侧
            {
                if (channelIndex == 1) effectiveBitsPerSample++; // 侧边通道多1位
            }
            else if (channelAssignment == 9) // 侧-右
            {
                if (channelIndex == 0) effectiveBitsPerSample++; // 侧边通道多1位
            }
            else if (channelAssignment == 10) // 中-侧
            {
                if (channelIndex == 1) effectiveBitsPerSample++; // 侧边通道多1位
            }

            int wastedBits = 0;
            if (hasWastedBits)
            {
                wastedBits = ReadUnaryValue(reader) + 1;
                effectiveBitsPerSample -= wastedBits;
            }
            
            bitsPerSample = effectiveBitsPerSample;

            int[] samples;

            if (subframeType == 0) // CONSTANT
            {
                samples = ReadConstantSubframe(reader, blockSize, bitsPerSample);
            }
            else if (subframeType == 1) // VERBATIM
            {
                samples = ReadVerbatimSubframe(reader, blockSize, bitsPerSample);
            }
            else if ((subframeType & 0x38) == 0x08) // FIXED
            {
                int order = subframeType & 0x07;
                samples = ReadFixedSubframe(reader, blockSize, bitsPerSample, order);
            }
            else if ((subframeType & 0x20) == 0x20) // LPC
            {
                int order = (subframeType & 0x1F) + 1;
                samples = ReadLpcSubframe(reader, blockSize, bitsPerSample, order);
            }
            else
            {
                throw new InvalidDataException($"未知的子帧类型: {subframeType}");
            }

            // 恢复wasted bits
            if (wastedBits > 0)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] <<= wastedBits;
                }
            }

            return samples;
        }

        /// <summary>
        /// 读取CONSTANT子帧
        /// </summary>
        private int[] ReadConstantSubframe(FlacBitReader reader, int blockSize, int bitsPerSample)
        {
            int value = reader.ReadSignedBits(bitsPerSample);
            int[] samples = new int[blockSize];
            Array.Fill(samples, value);
            return samples;
        }

        /// <summary>
        /// 读取VERBATIM子帧
        /// </summary>
        private int[] ReadVerbatimSubframe(FlacBitReader reader, int blockSize, int bitsPerSample)
        {
            int[] samples = new int[blockSize];
            for (int i = 0; i < blockSize; i++)
            {
                samples[i] = reader.ReadSignedBits(bitsPerSample);
            }
            return samples;
        }

        /// <summary>
        /// 读取FIXED子帧
        /// </summary>
        private int[] ReadFixedSubframe(FlacBitReader reader, int blockSize, int bitsPerSample, int order)
        {
            int[] samples = new int[blockSize];
            
            // 读取warm-up样本
            for (int i = 0; i < order; i++)
            {
                samples[i] = reader.ReadSignedBits(bitsPerSample);
            }

            // 读取残差
            int[] residuals = ReadResidual(reader, blockSize - order);

            // 应用固定预测器
            ApplyFixedPredictor(samples, residuals, order);

            return samples;
        }

        /// <summary>
        /// 读取LPC子帧
        /// </summary>
        private int[] ReadLpcSubframe(FlacBitReader reader, int blockSize, int bitsPerSample, int order)
        {
            int[] samples = new int[blockSize];
            
            // 读取warm-up样本
            for (int i = 0; i < order; i++)
            {
                samples[i] = reader.ReadSignedBits(bitsPerSample);
            }

            // 读取LPC系数精度
            int precision = reader.ReadBits(4) + 1;
            
            // 读取量化级别
            int shift = reader.ReadSignedBits(5);

            // 读取LPC系数
            int[] coefficients = new int[order];
            for (int i = 0; i < order; i++)
            {
                coefficients[i] = reader.ReadSignedBits(precision);
            }

            // 读取残差
            int[] residuals = ReadResidual(reader, blockSize - order);

            // 应用LPC预测器
            ApplyLpcPredictor(samples, coefficients, shift, residuals, order);

            return samples;
        }

        /// <summary>
        /// 读取残差数据
        /// </summary>
        private int[] ReadResidual(FlacBitReader reader, int count)
        {
            try
            {
                // 编码方法（2位）
                int codingMethod = reader.ReadBits(2);
                
                if (codingMethod > 1)
                    throw new InvalidDataException($"不支持的残差编码方法: {codingMethod}");

                // 分区阶数（4位）
                int partitionOrder = reader.ReadBits(4);
                int partitions = 1 << partitionOrder;

                int[] residuals = new int[count];
                int residualIndex = 0;

                for (int partition = 0; partition < partitions; partition++)
                {
                    int samplesInPartition;
                    if (partition == 0)
                        samplesInPartition = Math.Max(0, (count >> partitionOrder) - partitionOrder);
                    else
                        samplesInPartition = count >> partitionOrder;

                    if (samplesInPartition == 0)
                        continue;

                    // Rice参数
                    int riceParameter = reader.ReadBits(codingMethod == 0 ? 4 : 5);

                    if (riceParameter == (codingMethod == 0 ? 15 : 31))
                    {
                        // 未编码的残差
                        int bitsPerSample = reader.ReadBits(5);
                        for (int i = 0; i < samplesInPartition && residualIndex < residuals.Length; i++)
                        {
                            residuals[residualIndex++] = reader.ReadSignedBits(bitsPerSample);
                        }
                    }
                    else
                    {
                        // Rice编码的残差
                        for (int i = 0; i < samplesInPartition && residualIndex < residuals.Length; i++)
                        {
                            residuals[residualIndex++] = ReadRiceSignedValue(reader, riceParameter);
                        }
                    }
                }

                return residuals;
            }
            catch (Exception ex)
            {
                _logger.Warn("FlacDecoder", $"读取残差失败: {ex.Message}，返回零数组");
                // 返回零数组而不是抛出异常，避免整个解码失败
                return new int[count];
            }
        }

        /// <summary>
        /// 读取Rice编码的有符号值
        /// </summary>
        private int ReadRiceSignedValue(FlacBitReader reader, int parameter)
        {
            int unary = ReadUnaryValue(reader);
            int binary = reader.ReadBits(parameter);
            int value = (unary << parameter) | binary;
            
            // 转换为有符号值（zigzag编码）
            return (value & 1) == 0 ? (value >> 1) : -(value >> 1) - 1;
        }

        /// <summary>
        /// 读取一元编码值
        /// </summary>
        private int ReadUnaryValue(FlacBitReader reader)
        {
            int count = 0;
            while (!reader.ReadBit())
            {
                count++;
            }
            return count;
        }

        /// <summary>
        /// 读取UTF-8编码的数字
        /// </summary>
        private long ReadUtf8Number(FlacBitReader reader)
        {
            int firstByte = reader.ReadBits(8);
            
            if ((firstByte & 0x80) == 0)
                return firstByte;

            int numBytes = 0;
            int mask = 0x80;
            while ((firstByte & mask) != 0)
            {
                numBytes++;
                mask >>= 1;
            }

            long value = firstByte & (mask - 1);
            for (int i = 1; i < numBytes; i++)
            {
                int nextByte = reader.ReadBits(8);
                value = (value << 6) | (nextByte & 0x3F);
            }

            return value;
        }

        /// <summary>
        /// 应用固定预测器
        /// </summary>
        private void ApplyFixedPredictor(int[] samples, int[] residuals, int order)
        {
            for (int i = order; i < samples.Length; i++)
            {
                int prediction = 0;
                switch (order)
                {
                    case 0:
                        prediction = 0;
                        break;
                    case 1:
                        prediction = samples[i - 1];
                        break;
                    case 2:
                        prediction = 2 * samples[i - 1] - samples[i - 2];
                        break;
                    case 3:
                        prediction = 3 * samples[i - 1] - 3 * samples[i - 2] + samples[i - 3];
                        break;
                    case 4:
                        prediction = 4 * samples[i - 1] - 6 * samples[i - 2] + 
                                   4 * samples[i - 3] - samples[i - 4];
                        break;
                }
                samples[i] = prediction + residuals[i - order];
            }
        }

        /// <summary>
        /// 应用LPC预测器
        /// </summary>
        private void ApplyLpcPredictor(int[] samples, int[] coefficients, int shift, 
            int[] residuals, int order)
        {
            for (int i = order; i < samples.Length; i++)
            {
                long prediction = 0;
                for (int j = 0; j < order; j++)
                {
                    prediction += (long)coefficients[j] * samples[i - j - 1];
                }
                prediction >>= shift;
                samples[i] = (int)prediction + residuals[i - order];
            }
        }

        /// <summary>
        /// 交织通道数据并转换为浮点数
        /// </summary>
        private float[] InterleaveAndConvert(int[][] channelData, int blockSize, int channels)
        {
            float[] output = new float[blockSize * channels];
            int maxValue = 1 << (_streamInfo.BitsPerSample - 1);

            for (int i = 0; i < blockSize; i++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    output[i * channels + ch] = channelData[ch][i] / (float)maxValue;
                }
            }

            return output;
        }

        /// <summary>
        /// 解码通道分配模式
        /// </summary>
        private void DecodeChannelAssignment(int[][] channelData, FlacFrameHeader header)
        {
            if (header.ChannelAssignment >= 8 && header.ChannelAssignment <= 10)
            {
                // 处理特殊的通道编码
                int[] left = channelData[0];
                int[] right = channelData[1];

                switch (header.ChannelAssignment)
                {
                    case 8: // 左-侧编码
                        for (int i = 0; i < header.BlockSize; i++)
                        {
                            int side = right[i];
                            right[i] = left[i] - side;
                        }
                        break;
                    case 9: // 侧-右编码
                        for (int i = 0; i < header.BlockSize; i++)
                        {
                            int side = left[i];
                            left[i] = side + right[i];
                        }
                        break;
                    case 10: // 中-侧编码
                        for (int i = 0; i < header.BlockSize; i++)
                        {
                            int mid = left[i];
                            int side = right[i];
                            mid = (mid << 1) | (side & 1);
                            left[i] = (mid + side) >> 1;
                            right[i] = (mid - side) >> 1;
                        }
                        break;
                }
            }
        }

        // 辅助解码方法
        private int DecodeBlockSize(int code, FlacBitReader reader)
        {
            if (code == 1) return 192;
            if (code >= 2 && code <= 5) return 576 << (code - 2);
            if (code >= 8 && code <= 15) return 256 << (code - 8);
            return code; // 6和7需要额外读取
        }

        private int DecodeSampleRate(int code, FlacBitReader reader)
        {
            int[] rates = { 0, 88200, 176400, 192000, 8000, 16000, 22050, 24000,
                          32000, 44100, 48000, 96000 };
            if (code < 12) return rates[code];
            return code; // 12-14需要额外读取
        }

        private int DecodeChannels(int code)
        {
            if (code < 8) return code + 1;
            if (code <= 10) return 2; // 左右、侧边、中间编码
            throw new InvalidDataException($"无效的通道代码: {code}");
        }

        private int DecodeBitsPerSample(int code)
        {
            int[] bits = { _streamInfo.BitsPerSample, 8, 12, 0, 16, 20, 24, 0 };
            if (code == 3 || code == 7)
                throw new InvalidDataException("保留的位深度值");
            return bits[code];
        }
    }

    /// <summary>
    /// FLAC流信息
    /// </summary>
    internal class FlacStreamInfo
    {
        public int MinBlockSize { get; set; }
        public int MaxBlockSize { get; set; }
        public int MinFrameSize { get; set; }
        public int MaxFrameSize { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }
        public long TotalSamples { get; set; }
    }

    /// <summary>
    /// FLAC帧头
    /// </summary>
    internal class FlacFrameHeader
    {
        public bool BlockingStrategy { get; set; }
        public int BlockSize { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }
        public int ChannelAssignment { get; set; }
    }

    /// <summary>
    /// FLAC音频数据
    /// </summary>
    public class FlacAudioData
    {
        public float[] Samples { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }
        public long TotalSamples { get; set; }
    }
}