using System;
using EnderAudioAnalyzer.Services;

class TestSpectrogram
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing SpectrogramService FFT implementation...");
        
        // 创建测试信号（正弦波）
        int sampleRate = 44100;
        double frequency = 440.0; // A4
        double duration = 1.0; // 1 second
        int numSamples = (int)(sampleRate * duration);
        
        float[] testSamples = new float[numSamples];
        for (int i = 0; i < numSamples; i++)
        {
            testSamples[i] = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
        }
        
        // 创建频谱服务
        var spectrogramService = new SpectrogramService();
        
        // 生成频谱图
        var spectrogramData = spectrogramService.GenerateSpectrogramAsync(testSamples, sampleRate, 1, 2048, 512).Result;
        
        Console.WriteLine($"Spectrogram dimensions: {spectrogramData.NumFrames} frames x {spectrogramData.NumBins} bins");
        
        // 检查前几帧的数据
        for (int frame = 0; frame < Math.Min(5, spectrogramData.NumFrames); frame++)
        {
            Console.WriteLine($"Frame {frame}:");
            for (int bin = 0; bin < Math.Min(10, spectrogramData.NumBins); bin++)
            {
                Console.WriteLine($"  Bin {bin}: {spectrogramData.Data[frame][bin]:F6}");
            }
        }
        
        // 检查是否有非零值
        bool allZero = true;
        for (int frame = 0; frame < spectrogramData.NumFrames; frame++)
        {
            for (int bin = 0; bin < spectrogramData.NumBins; bin++)
            {
                if (Math.Abs(spectrogramData.Data[frame][bin]) > 0.0001f)
                {
                    allZero = false;
                    break;
                }
            }
            if (!allZero) break;
        }
        
        Console.WriteLine($"All values are zero: {allZero}");
    }
}