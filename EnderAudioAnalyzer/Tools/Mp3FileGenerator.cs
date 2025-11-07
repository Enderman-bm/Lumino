using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace EnderAudioAnalyzer.Tools
{
    /// <summary>
    /// 生成测试用的音频文件
    /// </summary>
    public class AudioFileGenerator
    {
        /// <summary>
        /// 生成一个测试用的音频文件
        /// </summary>
        /// <param name="filePath">输出文件路径</param>
        /// <param name="duration">音频时长（秒）</param>
        /// <param name="frequency">频率（Hz）</param>
        /// <param name="sampleRate">采样率</param>
        /// <param name="channels">声道数</param>
        public static async Task GenerateTestAudioFileAsync(string filePath, double duration = 5.0, double frequency = 440.0, int sampleRate = 44100, int channels = 2)
        {
            // 确保目录存在
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 生成WAV文件
            await GenerateWavFileAsync(filePath, duration, frequency, sampleRate, channels);
        }

        /// <summary>
        /// 生成WAV文件
        /// </summary>
        private static async Task GenerateWavFileAsync(string filePath, double duration, double frequency, int sampleRate, int channels)
        {
            var waveFormat = new WaveFormat(sampleRate, 16, channels);
            
            using (var writer = new WaveFileWriter(filePath, waveFormat))
            {
                // 计算总样本数
                int totalSamples = (int)(sampleRate * duration);
                double phase = 0;
                double phaseStep = 2 * Math.PI * frequency / sampleRate;
                
                for (int i = 0; i < totalSamples; i++)
                {
                    // 生成正弦波样本
                    float sampleValue = (float)Math.Sin(phase);
                    
                    // 转换为16位整数
                    short sample = (short)(sampleValue * short.MaxValue);
                    
                    // 写入所有声道
                    for (int channel = 0; channel < channels; channel++)
                    {
                        writer.WriteSample(sample / (float)short.MaxValue);
                    }
                    
                    // 更新相位
                    phase += phaseStep;
                    if (phase > 2 * Math.PI)
                    {
                        phase -= 2 * Math.PI;
                    }
                    
                    // 每1000个样本让出一次CPU
                    if (i % 1000 == 0)
                    {
                        await Task.Yield();
                    }
                }
                
                writer.Flush();
            }
        }
    }
}