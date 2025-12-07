using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnderDebugger;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 临时工程缓存服务实现
    /// 将MIDI导入过程中的音符数据缓存到硬盘而非内存
    /// 使用二进制格式存储以提高读写效率
    /// </summary>
    public class TempProjectCacheService : ITempProjectCacheService
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private readonly object _lock = new object();
        
        private string? _currentSessionId;
        private string? _currentCacheFilePath;
        private FileStream? _writeStream;
        private BinaryWriter? _binaryWriter;
        private long _noteCount;
        private bool _isWritingFinished;
        private bool _disposed;

        // 每个音符的二进制格式：
        // Pitch (int): 4 bytes
        // StartPosition (double): 8 bytes
        // Duration (double): 8 bytes
        // Velocity (int): 4 bytes
        // TrackIndex (int): 4 bytes
        // MidiChannel (int): 4 bytes
        // Total: 32 bytes per note
        private const int NOTE_BINARY_SIZE = 32;

        /// <summary>
        /// 临时缓存目录（程序根目录下的temp文件夹）
        /// </summary>
        public string TempCacheDirectory { get; }

        /// <summary>
        /// 当前缓存文件路径
        /// </summary>
        public string? CurrentCacheFilePath => _currentCacheFilePath;

        public TempProjectCacheService()
        {
            // 获取程序根目录
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            TempCacheDirectory = Path.Combine(appDirectory, "temp", "midi_cache");
            
            // 确保目录存在
            if (!Directory.Exists(TempCacheDirectory))
            {
                Directory.CreateDirectory(TempCacheDirectory);
            }

            _logger.Debug("TempProjectCacheService", $"临时缓存目录: {TempCacheDirectory}");
        }

        /// <summary>
        /// 开始新的缓存会话
        /// </summary>
        public string BeginCacheSession()
        {
            lock (_lock)
            {
                // 清理之前的会话
                EndCacheSessionInternal();

                _currentSessionId = Guid.NewGuid().ToString("N");
                _currentCacheFilePath = Path.Combine(TempCacheDirectory, $"cache_{_currentSessionId}.bin");
                _noteCount = 0;
                _isWritingFinished = false;

                // 如果目标文件已存在，先尝试删除
                if (File.Exists(_currentCacheFilePath))
                {
                    TryDeleteFileWithRetry(_currentCacheFilePath);
                }

                // 创建写入流（带重试机制）
                int retryCount = 0;
                const int maxRetries = 3;
                Exception? lastException = null;
                
                while (retryCount < maxRetries)
                {
                    try
                    {
                        _writeStream = new FileStream(
                            _currentCacheFilePath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            bufferSize: 65536,  // 64KB 缓冲区
                            FileOptions.SequentialScan | FileOptions.Asynchronous
                        );
                        _binaryWriter = new BinaryWriter(_writeStream);
                        break;
                    }
                    catch (IOException ex)
                    {
                        lastException = ex;
                        retryCount++;
                        _logger.Warn("TempProjectCacheService", $"创建缓存文件失败，重试 {retryCount}/{maxRetries}: {ex.Message}");
                        
                        // 强制GC并等待
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        Thread.Sleep(100 * retryCount);
                        
                        // 尝试删除可能残留的文件
                        TryDeleteFileWithRetry(_currentCacheFilePath);
                    }
                }
                
                if (_writeStream == null || _binaryWriter == null)
                {
                    throw new IOException($"无法创建缓存文件，文件可能被占用: {_currentCacheFilePath}", lastException);
                }

                // 写入文件头（预留空间用于存储音符数量）
                _binaryWriter.Write((long)0); // 占位符，后面会更新

                _logger.Info("TempProjectCacheService", $"开始缓存会话: {_currentSessionId}");
                return _currentSessionId;
            }
        }
        
        /// <summary>
        /// 尝试删除文件（带重试）
        /// </summary>
        private void TryDeleteFileWithRetry(string filePath, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                    return;
                }
                catch (IOException)
                {
                    if (i < maxRetries - 1)
                    {
                        Thread.Sleep(50 * (i + 1));
                    }
                }
            }
        }

        /// <summary>
        /// 写入单个音符
        /// </summary>
        public async Task WriteNoteAsync(Note note)
        {
            if (_binaryWriter == null || _isWritingFinished)
            {
                throw new InvalidOperationException("没有活动的缓存写入会话或写入已完成");
            }

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    WriteNoteBinary(note);
                    _noteCount++;
                }
            });
        }

        /// <summary>
        /// 批量写入音符
        /// </summary>
        public async Task WriteNotesAsync(IEnumerable<Note> notes)
        {
            if (_binaryWriter == null || _isWritingFinished)
            {
                throw new InvalidOperationException("没有活动的缓存写入会话或写入已完成");
            }

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    foreach (var note in notes)
                    {
                        WriteNoteBinary(note);
                        _noteCount++;
                    }
                }
            });
        }

        /// <summary>
        /// 将音符写入二进制格式
        /// </summary>
        private void WriteNoteBinary(Note note)
        {
            if (_binaryWriter == null) return;

            _binaryWriter.Write(note.Pitch);
            _binaryWriter.Write(note.StartPosition.ToDouble());
            _binaryWriter.Write(note.Duration.ToDouble());
            _binaryWriter.Write(note.Velocity);
            _binaryWriter.Write(note.TrackIndex);
            _binaryWriter.Write(note.MidiChannel);
        }

        /// <summary>
        /// 完成写入
        /// </summary>
        public async Task FinishWritingAsync()
        {
            if (_binaryWriter == null || _writeStream == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    // 刷新缓冲区
                    _binaryWriter.Flush();
                    _writeStream.Flush();

                    // 回到开头更新音符数量
                    _writeStream.Seek(0, SeekOrigin.Begin);
                    _binaryWriter.Write(_noteCount);
                    _binaryWriter.Flush();

                    // 关闭写入流
                    _binaryWriter.Dispose();
                    _writeStream.Dispose();
                    _binaryWriter = null;
                    _writeStream = null;
                    _isWritingFinished = true;

                    _logger.Info("TempProjectCacheService", $"写入完成，共 {_noteCount} 个音符");
                }
            });
        }

        /// <summary>
        /// 按批次读取音符
        /// </summary>
        public async IAsyncEnumerable<IList<Note>> ReadNotesInBatchesAsync(
            int batchSize,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_currentCacheFilePath) || !File.Exists(_currentCacheFilePath))
            {
                _logger.Warn("TempProjectCacheService", "缓存文件不存在，无法读取");
                yield break;
            }

            FileStream? readStream = null;
            BinaryReader? binaryReader = null;
            
            // 等待写入完成
            int waitCount = 0;
            while (!_isWritingFinished && waitCount < 50)
            {
                await Task.Delay(100);
                waitCount++;
            }
            
            // 带重试的文件打开
            int retryCount = 0;
            const int maxRetries = 5;
            Exception? lastException = null;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    readStream = new FileStream(
                        _currentCacheFilePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,  // 允许其他进程同时读写
                        bufferSize: 65536,
                        FileOptions.SequentialScan | FileOptions.Asynchronous
                    );
                    binaryReader = new BinaryReader(readStream);
                    break;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    retryCount++;
                    _logger.Warn("TempProjectCacheService", $"打开缓存文件失败，重试 {retryCount}/{maxRetries}: {ex.Message}");
                    
                    // 等待后重试
                    await Task.Delay(200 * retryCount);
                    
                    // 强制GC尝试释放句柄
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                catch (Exception ex)
                {
                    _logger.Error("TempProjectCacheService", $"打开缓存文件失败: {ex.Message}");
                    yield break;
                }
            }
            
            if (readStream == null || binaryReader == null)
            {
                _logger.Error("TempProjectCacheService", $"无法打开缓存文件，文件可能被占用: {lastException?.Message}");
                yield break;
            }

            long totalNotes = 0;
            try
            {
                // 读取音符数量
                totalNotes = binaryReader.ReadInt64();
                _logger.Debug("TempProjectCacheService", $"开始读取 {totalNotes} 个音符，批次大小: {batchSize}");
            }
            catch (Exception ex)
            {
                _logger.Error("TempProjectCacheService", $"读取缓存文件头失败: {ex.Message}");
                binaryReader?.Dispose();
                readStream?.Dispose();
                yield break;
            }

            var batch = new List<Note>(batchSize);
            long readCount = 0;
            bool hasError = false;

            while (readCount < totalNotes && !cancellationToken.IsCancellationRequested && !hasError)
            {
                Note? note = null;
                try
                {
                    note = ReadNoteBinary(binaryReader);
                }
                catch (EndOfStreamException)
                {
                    _logger.Warn("TempProjectCacheService", $"读取结束，已读取 {readCount} 个音符");
                    hasError = true;
                }
                catch (Exception ex)
                {
                    _logger.Error("TempProjectCacheService", $"读取音符失败: {ex.Message}");
                    hasError = true;
                }

                if (note != null && !hasError)
                {
                    batch.Add(note);
                    readCount++;

                    if (batch.Count >= batchSize)
                    {
                        yield return batch;
                        batch = new List<Note>(batchSize);
                        
                        // 让出控制权以避免阻塞
                        await Task.Yield();
                    }
                }
            }

            // 返回剩余的音符
            if (batch.Count > 0)
            {
                yield return batch;
            }

            // 清理资源
            binaryReader?.Dispose();
            await readStream.DisposeAsync();

            _logger.Debug("TempProjectCacheService", $"读取完成，共 {readCount} 个音符");
        }

        /// <summary>
        /// 从二进制格式读取音符
        /// </summary>
        private Note ReadNoteBinary(BinaryReader reader)
        {
            return new Note
            {
                Pitch = reader.ReadInt32(),
                StartPosition = MusicalFraction.FromDouble(reader.ReadDouble()),
                Duration = MusicalFraction.FromDouble(reader.ReadDouble()),
                Velocity = reader.ReadInt32(),
                TrackIndex = reader.ReadInt32(),
                MidiChannel = reader.ReadInt32()
            };
        }

        /// <summary>
        /// 获取缓存的音符数量
        /// </summary>
        public long GetCachedNoteCount()
        {
            return _noteCount;
        }

        /// <summary>
        /// 结束缓存会话
        /// </summary>
        public void EndCacheSession()
        {
            lock (_lock)
            {
                EndCacheSessionInternal();
            }
        }

        private void EndCacheSessionInternal()
        {
            // 先关闭BinaryWriter
            if (_binaryWriter != null)
            {
                try
                {
                    _binaryWriter.Flush();
                    _binaryWriter.Close();
                    _binaryWriter.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Warn("TempProjectCacheService", $"关闭BinaryWriter失败: {ex.Message}");
                }
                finally
                {
                    _binaryWriter = null;
                }
            }

            // 再关闭FileStream
            if (_writeStream != null)
            {
                try
                {
                    _writeStream.Flush();
                    _writeStream.Close();
                    _writeStream.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Warn("TempProjectCacheService", $"关闭WriteStream失败: {ex.Message}");
                }
                finally
                {
                    _writeStream = null;
                }
            }

            // 强制GC释放文件句柄
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 删除当前缓存文件（不删除，保留供后续读取）
            // 只有在需要清理时才删除
            var cacheFilePath = _currentCacheFilePath;
            
            _currentSessionId = null;
            _currentCacheFilePath = null;
            _noteCount = 0;
            _isWritingFinished = false;
            
            // 注意：不再自动删除缓存文件，因为可能还需要读取
            // 如果需要删除，调用CleanupAllCacheFiles
        }

        /// <summary>
        /// 清理所有临时缓存文件
        /// </summary>
        public void CleanupAllCacheFiles()
        {
            lock (_lock)
            {
                EndCacheSessionInternal();

                try
                {
                    if (Directory.Exists(TempCacheDirectory))
                    {
                        var files = Directory.GetFiles(TempCacheDirectory, "cache_*.bin");
                        foreach (var file in files)
                        {
                            try
                            {
                                File.Delete(file);
                                _logger.Debug("TempProjectCacheService", $"清理缓存文件: {file}");
                            }
                            catch (Exception ex)
                            {
                                _logger.Warn("TempProjectCacheService", $"清理缓存文件失败: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("TempProjectCacheService", $"清理缓存目录失败: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                EndCacheSessionInternal();
            }

            GC.SuppressFinalize(this);
        }

        ~TempProjectCacheService()
        {
            Dispose();
        }
    }
}
