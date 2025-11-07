using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnderAudioAnalyzer.Interfaces;
using EnderAudioAnalyzer.Models;
using EnderDebugger;

namespace EnderAudioAnalyzer.Services
{
    /// <summary>
    /// 频谱分析服务 - 负责音频频谱分析和音高检测
    /// </summary>
    public class SpectrumAnalysisService : ISpectrumAnalysisService
    {
        private readonly EnderLogger _logger;
        private readonly FFTService _fftService;

        public SpectrumAnalysisService()
        {
            _logger = new EnderLogger("SpectrumAnalysisService");
            _fftService = new FFTService();
        }

        /// <summary>
        /// 执行频谱分析
        /// </summary>
        public async Task<SpectrumData> AnalyzeSpectrumAsync(float[] audioSamples, int sampleRate, SpectrumAnalysisOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Debug("SpectrumAnalysisService", $"开始频谱分析: 样本数={audioSamples.Length}, 采样率={sampleRate}Hz");

                    // 检查输入样本数据
                    if (audioSamples == null || audioSamples.Length == 0)
                    {
                        _logger.Error("SpectrumAnalysisService", "音频样本为空");
                        throw new ArgumentException("音频样本为空");
                    }

                    // 检查样本数据是否全为零
                    bool allZero = true;
                    for (int i = 0; i < Math.Min(10, audioSamples.Length); i++)
                    {
                        if (Math.Abs(audioSamples[i]) > 1e-10f)
                        {
                            allZero = false;
                            break;
                        }
                    }
                    
                    if (allZero)
                    {
                        _logger.Warn("SpectrumAnalysisService", "警告：输入音频样本全为零");
                    }
                    else
                    {
                        _logger.Debug("SpectrumAnalysisService", $"音频样本范围: [{audioSamples.Min()}, {audioSamples.Max()}]");
                    }

                    var spectrumData = new SpectrumData
                    {
                        SampleRate = sampleRate,
                        FrequencyResolution = (double)sampleRate / options.WindowSize,
                        Timestamp = DateTime.Now
                    };

                    // 应用窗函数
                    var windowedSamples = ApplyWindowFunction(audioSamples, options.WindowType, options.WindowSize);

                    // 执行FFT
                    var fftResult = _fftService.ComputeFFT(windowedSamples);

                    // 计算幅度谱
                    var magnitudeSpectrum = ComputeMagnitudeSpectrum(fftResult, sampleRate, options.WindowSize);

                    // 寻找频谱峰值
                    var peaks = FindSpectralPeaks(magnitudeSpectrum, options.MinPeakHeight, options.PeakThreshold);

                    spectrumData.Frequencies = magnitudeSpectrum.Select(m => m.Frequency).ToArray();
                    spectrumData.Magnitudes = magnitudeSpectrum.Select(m => m.Magnitude).ToArray();
                    spectrumData.Peaks = peaks;

                    _logger.Info("SpectrumAnalysisService", $"频谱分析完成: 找到 {peaks.Count} 个峰值");
                    return spectrumData;
                }
                catch (Exception ex)
                {
                    _logger.Error("SpectrumAnalysisService", $"频谱分析失败: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// 生成频谱图（STFT - 短时傅里叶变换）
        /// </summary>
        /// <param name="audioSamples">音频样本</param>
        /// <param name="sampleRate">采样率</param>
        /// <param name="options">频谱图选项</param>
        /// <returns>2D数组 [时间帧, 频率bin]，值为dB幅度</returns>
        public async Task<double[,]> GenerateSpectrogramAsync(float[] audioSamples, int sampleRate, SpectrogramOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Debug("SpectrumAnalysisService", $"开始生成频谱图: 样本数={audioSamples.Length}, 窗口大小={options.WindowSize}, 跳跃大小={options.HopSize}");

                    int numFrames = (audioSamples.Length - options.WindowSize) / options.HopSize + 1;
                    int numFreqBins = options.WindowSize / 2 + 1; // 正频率部分

                    var spectrogram = new double[numFrames, numFreqBins];

                    for (int frame = 0; frame < numFrames; frame++)
                    {
                        // 提取当前帧
                        int startIdx = frame * options.HopSize;
                        var frameSamples = new float[options.WindowSize];
                        Array.Copy(audioSamples, startIdx, frameSamples, 0, Math.Min(options.WindowSize, audioSamples.Length - startIdx));

                        // 应用窗函数
                        var windowedSamples = ApplyWindowFunction(frameSamples, options.WindowType, options.WindowSize);

                        // 执行FFT
                        var fftResult = _fftService.ComputeFFT(windowedSamples);

                        // 计算幅度谱并转换为dB
                        for (int bin = 0; bin < numFreqBins; bin++)
                        {
                            double frequency = (double)bin * sampleRate / options.WindowSize;
                            double magnitude = Math.Sqrt(fftResult[bin].Real * fftResult[bin].Real + fftResult[bin].Imaginary * fftResult[bin].Imaginary);
                            spectrogram[frame, bin] = 20 * Math.Log10(magnitude + double.Epsilon);
                        }

                        // 报告进度（可选）
                        if (frame % 10 == 0)
                        {
                            _logger.Debug("SpectrumAnalysisService", $"频谱图进度: {frame}/{numFrames} 帧");
                        }
                    }

                    _logger.Info("SpectrumAnalysisService", $"频谱图生成完成: {numFrames} x {numFreqBins} 帧");
                    return spectrogram;
                }
                catch (Exception ex)
                {
                    _logger.Error("SpectrumAnalysisService", $"频谱图生成失败: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// 应用窗函数
        /// </summary>
        private float[] ApplyWindowFunction(float[] samples, WindowType windowType, int windowSize)
        {
            var windowed = new float[windowSize];
            var window = CreateWindow(windowType, windowSize);

            for (int i = 0; i < windowSize && i < samples.Length; i++)
            {
                windowed[i] = samples[i] * window[i];
            }

            return windowed;
        }

        /// <summary>
        /// 创建窗函数
        /// </summary>
        private float[] CreateWindow(WindowType windowType, int size)
        {
            var window = new float[size];

            switch (windowType)
            {
                case WindowType.Hann:
                    for (int i = 0; i < size; i++)
                    {
                        window[i] = 0.5f * (1 - (float)Math.Cos(2 * Math.PI * i / (size - 1)));
                    }
                    break;

                case WindowType.Hamming:
                    for (int i = 0; i < size; i++)
                    {
                        window[i] = 0.54f - 0.46f * (float)Math.Cos(2 * Math.PI * i / (size - 1));
                    }
                    break;

                case WindowType.Blackman:
                    for (int i = 0; i < size; i++)
                    {
                        window[i] = 0.42f - 0.5f * (float)Math.Cos(2 * Math.PI * i / (size - 1)) +
                                    0.08f * (float)Math.Cos(4 * Math.PI * i / (size - 1));
                    }
                    break;

                case WindowType.Rectangular:
                default:
                    for (int i = 0; i < size; i++)
                    {
                        window[i] = 1.0f;
                    }
                    break;
            }

            return window;
        }

        /// <summary>
        /// 计算幅度谱
        /// </summary>
        private List<Models.FrequencyMagnitude> ComputeMagnitudeSpectrum(ComplexNumber[] fftResult, int sampleRate, int windowSize)
        {
            var magnitudeSpectrum = new List<FrequencyMagnitude>();
            int n = fftResult.Length;

            // 只计算正频率部分（奈奎斯特频率以下）
            for (int i = 0; i < n / 2; i++)
            {
                double frequency = (double)i * sampleRate / n;
                double magnitude = Math.Sqrt(fftResult[i].Real * fftResult[i].Real + 
                                           fftResult[i].Imaginary * fftResult[i].Imaginary);
                
                // 转换为dB
                double magnitudeDb = 20 * Math.Log10(magnitude + double.Epsilon);

                magnitudeSpectrum.Add(new FrequencyMagnitude
                {
                    Frequency = frequency,
                    Magnitude = magnitudeDb
                });
            }

            return magnitudeSpectrum;
        }

        /// <summary>
        /// 寻找频谱峰值
        /// </summary>
        private List<Models.SpectralPeak> FindSpectralPeaks(List<Models.FrequencyMagnitude> magnitudeSpectrum, double minPeakHeight, double peakThreshold)
        {
            var peaks = new List<SpectralPeak>();

            for (int i = 1; i < magnitudeSpectrum.Count - 1; i++)
            {
                double current = magnitudeSpectrum[i].Magnitude;
                double prev = magnitudeSpectrum[i - 1].Magnitude;
                double next = magnitudeSpectrum[i + 1].Magnitude;

                // 检查是否为局部最大值且高于阈值
                if (current > prev && current > next && 
                    current > minPeakHeight && 
                    current - prev > peakThreshold && 
                    current - next > peakThreshold)
                {
                    peaks.Add(new SpectralPeak
                    {
                        Frequency = magnitudeSpectrum[i].Frequency,
                        Magnitude = current,
                        BinIndex = i
                    });
                }
            }

            return peaks;
        }

        /// <summary>
        /// 检测音高（使用YIN算法）
        /// </summary>
        public async Task<double?> DetectPitchAsync(float[] audioSamples, int sampleRate, PitchDetectionOptions options)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Debug("SpectrumAnalysisService", $"开始音高检测: 样本数={audioSamples.Length}, 采样率={sampleRate}Hz");

                    // 使用YIN算法检测音高
                    var pitch = YinPitchDetection(audioSamples, sampleRate, options);

                    if (pitch.HasValue)
                    {
                        _logger.Info("SpectrumAnalysisService", $"音高检测完成: {pitch.Value:F2}Hz");
                    }
                    else
                    {
                        _logger.Warn("SpectrumAnalysisService", "未检测到有效音高");
                    }

                    return pitch;
                }
                catch (Exception ex)
                {
                    _logger.Error("SpectrumAnalysisService", $"音高检测失败: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// YIN音高检测算法实现
        /// </summary>
        private double? YinPitchDetection(float[] samples, int sampleRate, PitchDetectionOptions options)
        {
            int tauMax = Math.Min(samples.Length / 2, (int)(sampleRate / options.MinFrequency));
            int tauMin = Math.Max(2, (int)(sampleRate / options.MaxFrequency));

            // 计算差分函数
            var differenceFunction = ComputeDifferenceFunction(samples, tauMax);

            // 计算累积均值归一化差分函数
            var cmndf = ComputeCumulativeMeanNormalizedDifference(differenceFunction, tauMax);

            // 寻找第一个谷值
            int tau = FindFirstTrough(cmndf, tauMin, tauMax, options.Threshold);

            if (tau == -1)
            {
                return null; // 未找到有效音高
            }

            // 抛物线插值提高精度
            if (tau > 1 && tau < tauMax - 1)
            {
                double betterTau = ParabolicInterpolation(cmndf, tau);
                return sampleRate / betterTau;
            }

            return sampleRate / tau;
        }

        /// <summary>
        /// 计算差分函数
        /// </summary>
        private double[] ComputeDifferenceFunction(float[] samples, int tauMax)
        {
            var df = new double[tauMax + 1];

            for (int tau = 0; tau <= tauMax; tau++)
            {
                double sum = 0.0;
                for (int j = 0; j < samples.Length - tau; j++)
                {
                    double diff = samples[j] - samples[j + tau];
                    sum += diff * diff;
                }
                df[tau] = sum;
            }

            return df;
        }

        /// <summary>
        /// 计算累积均值归一化差分函数
        /// </summary>
        private double[] ComputeCumulativeMeanNormalizedDifference(double[] df, int tauMax)
        {
            var cmndf = new double[tauMax + 1];
            cmndf[0] = 1.0;

            double runningSum = 0.0;
            for (int tau = 1; tau <= tauMax; tau++)
            {
                runningSum += df[tau];
                cmndf[tau] = df[tau] * tau / runningSum;
            }

            return cmndf;
        }

        /// <summary>
        /// 寻找第一个谷值
        /// </summary>
        private int FindFirstTrough(double[] cmndf, int tauMin, int tauMax, double threshold)
        {
            for (int tau = tauMin; tau <= tauMax; tau++)
            {
                if (cmndf[tau] < threshold)
                {
                    // 找到低于阈值的点，寻找局部最小值
                    while (tau + 1 < tauMax && cmndf[tau + 1] < cmndf[tau])
                    {
                        tau++;
                    }
                    return tau;
                }
            }

            // 如果没找到低于阈值的点，寻找全局最小值
            double minValue = double.MaxValue;
            int minTau = -1;

            for (int tau = tauMin; tau <= tauMax; tau++)
            {
                if (cmndf[tau] < minValue)
                {
                    minValue = cmndf[tau];
                    minTau = tau;
                }
            }

            return minTau;
        }

        /// <summary>
        /// 抛物线插值
        /// </summary>
        private double ParabolicInterpolation(double[] values, int index)
        {
            double alpha = values[index - 1];
            double beta = values[index];
            double gamma = values[index + 1];

            return index + (alpha - gamma) / (2 * (alpha - 2 * beta + gamma));
        }

        /// <summary>
        /// 分析谐波结构
        /// </summary>
        public async Task<Models.HarmonicAnalysis> AnalyzeHarmonicsAsync(SpectrumData spectrumData, double fundamentalFrequency)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Debug("SpectrumAnalysisService", $"开始谐波分析: 基频={fundamentalFrequency:F2}Hz");

                    var harmonicAnalysis = new HarmonicAnalysis
                    {
                        FundamentalFrequency = fundamentalFrequency,
                        Harmonics = new List<Harmonic>()
                    };

                    if (fundamentalFrequency <= 0)
                    {
                        _logger.Warn("SpectrumAnalysisService", "无效的基频，跳过谐波分析");
                        return harmonicAnalysis;
                    }

                    // 分析前10个谐波
                    for (int harmonic = 1; harmonic <= 10; harmonic++)
                    {
                        double targetFrequency = fundamentalFrequency * harmonic;
                        double tolerance = fundamentalFrequency * 0.05; // 5%容差

                        // 在频谱中寻找最近的峰值
                        var nearestPeak = spectrumData.Peaks
                            .Where(p => Math.Abs(p.Frequency - targetFrequency) <= tolerance)
                            .OrderBy(p => Math.Abs(p.Frequency - targetFrequency))
                            .FirstOrDefault();

                        if (nearestPeak != null)
                        {
                            harmonicAnalysis.Harmonics.Add(new Harmonic
                            {
                                Order = harmonic,
                                Frequency = nearestPeak.Frequency,
                                Magnitude = nearestPeak.Magnitude,
                                Deviation = Math.Abs(nearestPeak.Frequency - targetFrequency)
                            });
                        }
                    }

                    harmonicAnalysis.Harmonicity = CalculateHarmonicity(harmonicAnalysis);
                    _logger.Info("SpectrumAnalysisService", $"谐波分析完成: 找到 {harmonicAnalysis.Harmonics.Count} 个谐波");

                    return harmonicAnalysis;
                }
                catch (Exception ex)
                {
                    _logger.Error("SpectrumAnalysisService", $"谐波分析失败: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// 计算谐波性（谐波能量占总能量的比例）
        /// </summary>
        private double CalculateHarmonicity(Models.HarmonicAnalysis harmonicAnalysis)
        {
            if (harmonicAnalysis.Harmonics.Count == 0)
                return 0.0;

            double harmonicEnergy = harmonicAnalysis.Harmonics.Sum(h => Math.Pow(10, h.Magnitude / 20));
            double totalEnergy = Math.Pow(10, harmonicAnalysis.Harmonics.Max(h => h.Magnitude) / 20) * 2; // 近似估计

            return harmonicEnergy / totalEnergy;
        }
    }

    /// <summary>
    /// FFT服务 - 实现快速傅里叶变换
    /// </summary>
    public class FFTService
    {
        /// <summary>
        /// 计算FFT（Cooley-Tukey算法）
        /// </summary>
        public ComplexNumber[] ComputeFFT(float[] samples)
        {
            int n = samples.Length;
            
            // 确保n是2的幂
            if (!IsPowerOfTwo(n))
            {
                n = NextPowerOfTwo(n);
                Array.Resize(ref samples, n);
            }

            var complexSamples = samples.Select(s => new ComplexNumber(s, 0)).ToArray();
            return FFT(complexSamples, false);
        }

        /// <summary>
        /// 递归FFT实现
        /// </summary>
        private ComplexNumber[] FFT(ComplexNumber[] samples, bool inverse)
        {
            int n = samples.Length;

            if (n == 1)
                return new[] { samples[0] };

            // 分离偶数和奇数索引的样本
            var even = new ComplexNumber[n / 2];
            var odd = new ComplexNumber[n / 2];

            for (int i = 0; i < n / 2; i++)
            {
                even[i] = samples[2 * i];
                odd[i] = samples[2 * i + 1];
            }

            // 递归计算
            var evenFFT = FFT(even, inverse);
            var oddFFT = FFT(odd, inverse);

            // 合并结果
            var result = new ComplexNumber[n];
            double sign = inverse ? 1.0 : -1.0;

            for (int k = 0; k < n / 2; k++)
            {
                double angle = sign * 2.0 * Math.PI * k / n;
                ComplexNumber twiddle = new ComplexNumber(Math.Cos(angle), Math.Sin(angle));
                
                ComplexNumber t = oddFFT[k] * twiddle;
                result[k] = evenFFT[k] + t;
                result[k + n / 2] = evenFFT[k] - t;

                if (inverse)
                {
                    result[k] = result[k] / 2.0;
                    result[k + n / 2] = result[k + n / 2] / 2.0;
                }
            }

            return result;
        }

        private bool IsPowerOfTwo(int n)
        {
            return (n & (n - 1)) == 0;
        }

        private int NextPowerOfTwo(int n)
        {
            int power = 1;
            while (power < n)
                power <<= 1;
            return power;
        }
    }

    /// <summary>
    /// 复数结构
    /// </summary>
    public struct ComplexNumber
    {
        public double Real { get; set; }
        public double Imaginary { get; set; }

        public ComplexNumber(double real, double imaginary)
        {
            Real = real;
            Imaginary = imaginary;
        }

        public static ComplexNumber operator +(ComplexNumber a, ComplexNumber b)
        {
            return new ComplexNumber(a.Real + b.Real, a.Imaginary + b.Imaginary);
        }

        public static ComplexNumber operator -(ComplexNumber a, ComplexNumber b)
        {
            return new ComplexNumber(a.Real - b.Real, a.Imaginary - b.Imaginary);
        }

        public static ComplexNumber operator *(ComplexNumber a, ComplexNumber b)
        {
            return new ComplexNumber(
                a.Real * b.Real - a.Imaginary * b.Imaginary,
                a.Real * b.Imaginary + a.Imaginary * b.Real
            );
        }

        public static ComplexNumber operator /(ComplexNumber a, double divisor)
        {
            return new ComplexNumber(a.Real / divisor, a.Imaginary / divisor);
        }
    }
}