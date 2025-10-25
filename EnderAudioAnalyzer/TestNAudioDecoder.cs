using System;
using System.IO;
using System.Threading.Tasks;
using EnderAudioAnalyzer.Tests;
using EnderAudioAnalyzer.Tools;

namespace EnderAudioAnalyzer
{
    /// <summary>
    /// NAudio解码器测试程序
    /// </summary>
    class TestNAudioDecoder
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("NAudio音频解码器测试程序");
            Console.WriteLine("=========================");
            
            // 检查命令行参数
            string testFile = null;
            if (args.Length > 0)
            {
                testFile = args[0];
                if (File.Exists(testFile))
                {
                    Console.WriteLine($"使用命令行指定的测试文件: {testFile}");
                }
                else
                {
                    Console.WriteLine($"命令行指定的文件不存在: {testFile}");
                    testFile = null;
                }
            }
            
            // 如果没有有效的命令行参数，查找测试文件
            if (testFile == null)
            {
                testFile = FindTestAudioFile();
            }
            
            if (string.IsNullOrEmpty(testFile))
            {
                Console.WriteLine("未找到测试音频文件，正在生成测试WAV文件...");
                
                // 生成测试WAV文件
                string generatedFile = "test_audio.wav";
                try
                {
                    await AudioFileGenerator.GenerateTestAudioFileAsync(generatedFile);
                    if (File.Exists(generatedFile))
                    {
                        testFile = generatedFile;
                        Console.WriteLine($"已生成测试文件: {testFile}");
                    }
                    else
                    {
                        Console.WriteLine("无法生成测试文件，测试终止。");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"生成测试文件失败: {ex.Message}");
                    Console.WriteLine("测试终止。");
                    return;
                }
            }
            
            Console.WriteLine($"使用测试文件: {testFile}");
            
            // 创建测试实例并运行测试
            var test = new NAudioAudioDecoderTest();
            bool success = await test.RunFullTest(testFile);
            
            if (success)
            {
                Console.WriteLine("测试通过！");
            }
            else
            {
                Console.WriteLine("测试失败！");
            }
            
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
        
        /// <summary>
        /// 查找测试音频文件
        /// </summary>
        static string FindTestAudioFile()
        {
            // 1. 检查当前目录下的test.wav文件
            string currentDir = Directory.GetCurrentDirectory();
            string testWavPath = Path.Combine(currentDir, "test.wav");
            if (File.Exists(testWavPath))
            {
                Console.WriteLine($"找到测试文件: {testWavPath}");
                return testWavPath;
            }
            
            // 2. 检查当前目录下的test.mp3文件
            string testMp3Path = Path.Combine(currentDir, "test.mp3");
            if (File.Exists(testMp3Path))
            {
                Console.WriteLine($"找到测试文件: {testMp3Path}");
                return testMp3Path;
            }
            
            // 3. 检查AnalyzerOut目录下的音频文件
            string analyzerOutPath = Path.Combine(currentDir, "AnalyzerOut");
            if (Directory.Exists(analyzerOutPath))
            {
                var wavFiles = Directory.GetFiles(analyzerOutPath, "*.wav");
                if (wavFiles.Length > 0)
                {
                    Console.WriteLine($"找到测试文件: {wavFiles[0]}");
                    return wavFiles[0];
                }
                
                var mp3Files = Directory.GetFiles(analyzerOutPath, "*.mp3");
                if (mp3Files.Length > 0)
                {
                    Console.WriteLine($"找到测试文件: {mp3Files[0]}");
                    return mp3Files[0];
                }
            }
            
            // 4. 检查上级目录的AnalyzerOut目录下的音频文件
            string parentAnalyzerOutPath = Path.Combine(currentDir, "..", "AnalyzerOut");
            if (Directory.Exists(parentAnalyzerOutPath))
            {
                var wavFiles = Directory.GetFiles(parentAnalyzerOutPath, "*.wav");
                if (wavFiles.Length > 0)
                {
                    Console.WriteLine($"找到测试文件: {wavFiles[0]}");
                    return wavFiles[0];
                }
                
                var mp3Files = Directory.GetFiles(parentAnalyzerOutPath, "*.mp3");
                if (mp3Files.Length > 0)
                {
                    Console.WriteLine($"找到测试文件: {mp3Files[0]}");
                    return mp3Files[0];
                }
            }
            
            return null;
        }
    }
}