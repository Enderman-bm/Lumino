using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnderDebugger;

namespace EnderAudioAnalyzer.Services
{
    /// <summary>
    /// 频谱图生成服务
    /// </summary>
    public class SpectrogramService
    {
        private readonly EnderLogger _logger;

        public SpectrogramService()
        {
            _logger = new EnderLogger("SpectrogramService");
        }

        /// <summary>
        /// 生成音频频谱图
        /// </summary>
        /// <param name="audioSamples">音频样本数据（交织格式）</param>
        /// <param name="sampleRate">采样率</param>
        /// <param name="channels">通道数</param>
        /// <param name="fftSize">FFT窗口大小（必须是2的幂）</param>
        /// <param name="hopSize">跳跃大小（帧之间的样本数）</param>
        /// <returns>频谱图数据 [时间帧][频率箱]</returns>
        public async Task<SpectrogramData> GenerateSpectrogramAsync(
            float[] audioSamples, 
            int sampleRate, 
            int channels,
            int fftSize = 2048,
            int hopSize = 512)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Info("SpectrogramService", 
                        $"开始生成频谱图: 采样率={sampleRate}Hz, 通道={channels}, FFT大小={fftSize}, 跳跃={hopSize}");

                    // 转换为单声道（如果需要）
                    float[] monoSamples = ConvertToMono(audioSamples, channels);

                    // 计算帧数
                    int numFrames = (monoSamples.Length - fftSize) / hopSize + 1;
                    int numBins = fftSize / 2 + 1; // FFT输出频率箱数

                    // 创建频谱数据数组
                    float[][] spectrogram = new float[numFrames][];
                    
                    // 预计算汉宁窗
                    float[] window = CreateHannWindow(fftSize);

                    // 对每一帧进行FFT
                    for (int frame = 0; frame < numFrames; frame++)
                    {
                        int startSample = frame * hopSize;
                        
                        // 提取当前帧的样本
                        float[] frameSamples = new float[fftSize];
                        Array.Copy(monoSamples, startSample, frameSamples, 0, 
                            Math.Min(fftSize, monoSamples.Length - startSample));

                        // 应用窗函数
                        for (int i = 0; i < frameSamples.Length; i++)
                        {
                            frameSamples[i] *= window[i];
                        }

                        // 执行FFT并计算幅度谱
                        spectrogram[frame] = ComputeMagnitudeSpectrum(frameSamples, fftSize);
                    }

                    // 转换为对数刻度（dB）
                    ConvertToLogScale(spectrogram);

                    _logger.Info("SpectrogramService", 
                        $"频谱图生成完成: {numFrames} 帧 × {numBins} 频率箱");

                    return new SpectrogramData
                    {
                        Data = spectrogram,
                        SampleRate = sampleRate,
                        FftSize = fftSize,
                        HopSize = hopSize,
                        NumFrames = numFrames,
                        NumBins = numBins,
                        TimeResolution = (double)hopSize / sampleRate,
                        FrequencyResolution = (double)sampleRate / fftSize
                    };
                }
                catch (Exception ex)
                {
                    _logger.Error("SpectrogramService", $"生成频谱图失败: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// 将频率映射到MIDI音高
        /// </summary>
        /// <param name="frequency">频率（Hz）</param>
        /// <returns>MIDI音高（0-127）</returns>
        public int FrequencyToMidiNote(double frequency)
        {
            if (frequency <= 0) return 0;
            
            // MIDI音高 = 69 + 12 * log2(f / 440)
            // 其中 69 是 A4 (440Hz) 的MIDI音高
            double midiNote = 69 + 12 * Math.Log2(frequency / 440.0);
            return Math.Clamp((int)Math.Round(midiNote), 0, 127);
        }

        /// <summary>
        /// 将MIDI音高映射到频率
        /// </summary>
        /// <param name="midiNote">MIDI音高（0-127）</param>
        /// <returns>频率（Hz）</returns>
        public double MidiNoteToFrequency(int midiNote)
        {
            // f = 440 * 2^((n - 69) / 12)
            return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
        }

        /// <summary>
        /// 获取指定MIDI音高范围的频谱数据
        /// </summary>
        public float[][] GetSpectrogramForMidiRange(
            SpectrogramData spectrogram,
            int minMidiNote,
            int maxMidiNote)
        {
            try
            {
                int numFrames = spectrogram.NumFrames;
                int numMidiNotes = maxMidiNote - minMidiNote + 1;
                
                // 创建MIDI音高对齐的频谱数据 [时间帧][MIDI音高]
                float[][] midiSpectrogram = new float[numFrames][];
                
                for (int frame = 0; frame < numFrames; frame++)
                {
                    midiSpectrogram[frame] = new float[numMidiNotes];
                    
                    for (int midiNote = minMidiNote; midiNote <= maxMidiNote; midiNote++)
                    {
                        // 计算该MIDI音高对应的频率范围
                        double centerFreq = MidiNoteToFrequency(midiNote);
                        double lowerFreq = 440.0 * Math.Pow(2.0, (midiNote - 0.5 - 69) / 12.0);
                        double upperFreq = 440.0 * Math.Pow(2.0, (midiNote + 0.5 - 69) / 12.0);
                        
                        // 找到对应的频率箱范围
                        int lowerBin = (int)Math.Max(0, lowerFreq / spectrogram.FrequencyResolution);
                        int upperBin = (int)Math.Min(spectrogram.NumBins - 1,
                            upperFreq / spectrogram.FrequencyResolution);
                        
                        // 对该范围内的能量求和或取最大值
                        float maxEnergy = 0;
                        for (int bin = lowerBin; bin <= upperBin; bin++)
                        {
                            maxEnergy = Math.Max(maxEnergy, spectrogram.Data[frame][bin]);
                        }
                        
                        midiSpectrogram[frame][midiNote - minMidiNote] = maxEnergy;
                    }
                }
                
                _logger.Info("SpectrogramService", 
                    $"生成MIDI对齐频谱图: {numFrames} 帧 × {numMidiNotes} 音高 (MIDI {minMidiNote}-{maxMidiNote})");
                
                return midiSpectrogram;
            }
            catch (Exception ex)
            {
                _logger.Error("SpectrogramService", $"生成MIDI对齐频谱图失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 转换为单声道
        /// </summary>
        private float[] ConvertToMono(float[] audioSamples, int channels)
        {
            if (channels == 1)
                return audioSamples;

            int monoLength = audioSamples.Length / channels;
            float[] monoSamples = new float[monoLength];

            for (int i = 0; i < monoLength; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    sum += audioSamples[i * channels + ch];
                }
                monoSamples[i] = sum / channels;
            }

            return monoSamples;
        }

        /// <summary>
        /// 创建汉宁窗
        /// </summary>
        private float[] CreateHannWindow(int size)
        {
            float[] window = new float[size];
            for (int i = 0; i < size; i++)
            {
                window[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1))));
            }
            return window;
        }

        /// <summary>
        /// 计算幅度谱
        /// </summary>
        private float[] ComputeMagnitudeSpectrum(float[] signal, int fftSize)
        {
            // 执行FFT
            Complex[] fftResult = FFT(signal, fftSize);
            
            // 计算幅度
            int numBins = fftSize / 2 + 1;
            float[] magnitude = new float[numBins];
            
            for (int i = 0; i < numBins; i++)
            {
                magnitude[i] = fftResult[i].Magnitude();
            }
            
            // 调试输出：检查前几个bin的值
            _logger.Debug("SpectrogramService",
                $"FFT结果: 信号长度={signal.Length}, FFT大小={fftSize}, 前5个bin: " +
                $"[0]={magnitude[0]:F6}, [1]={magnitude[1]:F6}, [2]={magnitude[2]:F6}, " +
                $"[3]={magnitude[3]:F6}, [4]={magnitude[4]:F6}");
            
            return magnitude;
        }

        /// <summary>
        /// 快速傅里叶变换（Cooley-Tukey算法）
        /// </summary>
        private Complex[] FFT(float[] signal, int n)
        {
            // 确保n是2的幂
            if ((n & (n - 1)) != 0)
            {
                throw new ArgumentException("FFT size must be a power of 2");
            }

            // 转换为复数
            Complex[] x = new Complex[n];
            for (int i = 0; i < signal.Length && i < n; i++)
            {
                x[i] = new Complex(signal[i], 0);
            }
            for (int i = signal.Length; i < n; i++)
            {
                x[i] = new Complex(0, 0);
            }

            // 调试：检查输入信号
            _logger.Debug("SpectrogramService",
                $"FFT输入信号: 长度={signal.Length}, 前5个样本: " +
                $"[0]={signal[0]:F6}, [1]={signal[1]:F6}, [2]={signal[2]:F6}, " +
                $"[3]={signal[3]:F6}, [4]={signal[4]:F6}");

            // Cooley-Tukey FFT
            var result = FFTRecursive(x);
            
            // 调试：检查FFT结果
            _logger.Debug("SpectrogramService",
                $"FFT结果: 前5个复数: " +
                $"[0]=({result[0].Real:F6}, {result[0].Imaginary:F6}), " +
                $"[1]=({result[1].Real:F6}, {result[1].Imaginary:F6}), " +
                $"[2]=({result[2].Real:F6}, {result[2].Imaginary:F6}), " +
                $"[3]=({result[3].Real:F6}, {result[3].Imaginary:F6}), " +
                $"[4]=({result[4].Real:F6}, {result[4].Imaginary:F6})");
            
            return result;
        }

        /// <summary>
        /// 递归FFT实现
        /// </summary>
        private Complex[] FFTRecursive(Complex[] x)
        {
            int n = x.Length;
            if (n == 1) return x;

            // 分离偶数和奇数索引
            Complex[] even = new Complex[n / 2];
            Complex[] odd = new Complex[n / 2];
            for (int i = 0; i < n / 2; i++)
            {
                even[i] = x[2 * i];
                odd[i] = x[2 * i + 1];
            }

            // 递归计算
            Complex[] fftEven = FFTRecursive(even);
            Complex[] fftOdd = FFTRecursive(odd);

            // 合并结果
            Complex[] result = new Complex[n];
            for (int k = 0; k < n / 2; k++)
            {
                double angle = -2 * Math.PI * k / n;
                Complex w = new Complex(Math.Cos(angle), Math.Sin(angle));
                Complex t = w * fftOdd[k];
                
                result[k] = fftEven[k] + t;
                result[k + n / 2] = fftEven[k] - t;
            }

            return result;
        }

        /// <summary>
        /// 转换为对数刻度（dB）
        /// </summary>
        private void ConvertToLogScale(float[][] spectrogram)
        {
            const float epsilon = 1e-10f; // 避免log(0)
            
            for (int frame = 0; frame < spectrogram.Length; frame++)
            {
                for (int bin = 0; bin < spectrogram[frame].Length; bin++)
                {
                    // 转换为dB: 20 * log10(magnitude)
                    float magnitude = spectrogram[frame][bin];
                    spectrogram[frame][bin] =
                        20.0f * (float)Math.Log10(magnitude + epsilon);
                }
            }
            
            // 调试输出：检查对数转换后的值
            if (spectrogram.Length > 0)
            {
                _logger.Debug("SpectrogramService",
                    $"对数转换后前5个bin: [0]={spectrogram[0][0]:F6}, [1]={spectrogram[0][1]:F6}, " +
                    $"[2]={spectrogram[0][2]:F6}, [3]={spectrogram[0][3]:F6}, [4]={spectrogram[0][4]:F6}");
            }
        }
    }

    /// <summary>
    /// 复数结构
    /// </summary>
    public struct Complex
    {
        public double Real { get; set; }
        public double Imaginary { get; set; }

        public Complex(double real, double imaginary)
        {
            Real = real;
            Imaginary = imaginary;
        }

        public float Magnitude()
        {
            return (float)Math.Sqrt(Real * Real + Imaginary * Imaginary);
        }

        public static Complex operator +(Complex a, Complex b)
        {
            return new Complex(a.Real + b.Real, a.Imaginary + b.Imaginary);
        }

        public static Complex operator -(Complex a, Complex b)
        {
            return new Complex(a.Real - b.Real, a.Imaginary - b.Imaginary);
        }

        public static Complex operator *(Complex a, Complex b)
        {
            return new Complex(
                a.Real * b.Real - a.Imaginary * b.Imaginary,
                a.Real * b.Imaginary + a.Imaginary * b.Real
            );
        }
    }

    /// <summary>
    /// 频谱图数据
    /// </summary>
    public class SpectrogramData
    {
        /// <summary>
        /// 频谱数据 [时间帧][频率箱]
        /// </summary>
        public float[][] Data { get; set; }

        /// <summary>
        /// 采样率
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// FFT窗口大小
        /// </summary>
        public int FftSize { get; set; }

        /// <summary>
        /// 跳跃大小
        /// </summary>
        public int HopSize { get; set; }

        /// <summary>
        /// 时间帧数
        /// </summary>
        public int NumFrames { get; set; }

        /// <summary>
        /// 频率箱数
        /// </summary>
        public int NumBins { get; set; }

        /// <summary>
        /// 时间分辨率（秒/帧）
        /// </summary>
        public double TimeResolution { get; set; }

        /// <summary>
        /// 频率分辨率（Hz/箱）
        /// </summary>
        public double FrequencyResolution { get; set; }
    }
}