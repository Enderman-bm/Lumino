using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnderDebugger;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 音符虚拟化服务实现 - 实现只加载可见区域音符的虚拟化存储
    /// 核心策略：
    /// 1. 构建轨道索引，记录每个轨道在缓存文件中的位置
    /// 2. 按需加载当前轨道的可见区域音符
    /// 3. 使用预加载缓冲区减少滚动时的卡顿
    /// </summary>
    public class NoteVirtualizationService : INoteVirtualizationService
    {
        private readonly EnderLogger _logger = EnderLogger.Instance;
        private readonly object _lock = new object();

        // 缓存文件路径
        private string? _cacheFilePath;
        
        // 轨道索引：轨道号 -> 轨道信息
        private readonly Dictionary<int, TrackNoteInfo> _trackIndex = new();
        
        // 当前视口缓存的音符
        private readonly List<NoteData> _viewportNotes = new();
        
        // 当前视口参数
        private int _currentTrackIndex = -1;
        private double _viewportStartTime;
        private double _viewportEndTime;
        private double _loadedStartTime;
        private double _loadedEndTime;
        
        // 状态
        private bool _isInitialized;
        private bool _disposed;
        private long _totalNoteCount;
        
        // 常量
        private const int NOTE_BINARY_SIZE = 32; // 与TempProjectCacheService一致
        private const double DEFAULT_PRELOAD_BUFFER = 8.0; // 预加载8个四分音符的缓冲区

        #region 属性
        public long TotalNoteCount => _totalNoteCount;
        public int LoadedNoteCount => _viewportNotes.Count;
        public bool IsInitialized => _isInitialized;
        public int CurrentTrackIndex => _currentTrackIndex;
        #endregion

        #region 初始化方法
        public async Task InitializeFromCacheAsync(ITempProjectCacheService cacheService, CancellationToken cancellationToken = default)
        {
            if (cacheService.CurrentCacheFilePath == null)
            {
                throw new InvalidOperationException("缓存服务没有活动的缓存文件");
            }

            _cacheFilePath = cacheService.CurrentCacheFilePath;
            _totalNoteCount = cacheService.GetCachedNoteCount();
            
            _logger.Info("NoteVirtualizationService", $"初始化虚拟化服务，缓存文件: {_cacheFilePath}, 总音符数: {_totalNoteCount}");
            
            await BuildIndexAsync(cancellationToken);
            _isInitialized = true;
        }

        public async Task BuildIndexAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_cacheFilePath) || !File.Exists(_cacheFilePath))
            {
                throw new InvalidOperationException("缓存文件不存在");
            }

            _logger.Info("NoteVirtualizationService", "开始构建轨道索引...");

            _trackIndex.Clear();
            
            // 临时存储每个轨道的统计信息
            var trackStats = new Dictionary<int, (long count, double minStart, double maxEnd, long firstOffset)>();

            await Task.Run(() =>
            {
                using var stream = new FileStream(_cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
                using var reader = new BinaryReader(stream);

                // 跳过文件头（8字节的音符数量）
                var totalNotes = reader.ReadInt64();
                _totalNoteCount = totalNotes;

                long currentOffset = 8; // 文件头之后的偏移

                for (long i = 0; i < totalNotes && !cancellationToken.IsCancellationRequested; i++)
                {
                    var pitch = reader.ReadInt32();
                    var startPos = reader.ReadDouble();
                    var duration = reader.ReadDouble();
                    var velocity = reader.ReadInt32();
                    var trackIndex = reader.ReadInt32();
                    var midiChannel = reader.ReadInt32();

                    var endPos = startPos + duration;

                    if (!trackStats.TryGetValue(trackIndex, out var stats))
                    {
                        trackStats[trackIndex] = (1, startPos, endPos, currentOffset);
                    }
                    else
                    {
                        trackStats[trackIndex] = (
                            stats.count + 1,
                            Math.Min(stats.minStart, startPos),
                            Math.Max(stats.maxEnd, endPos),
                            stats.firstOffset
                        );
                    }

                    currentOffset += NOTE_BINARY_SIZE;
                }

                // 构建轨道索引
                // 注意：由于音符是按轨道顺序写入的，我们需要计算每个轨道的实际偏移
                long runningOffset = 8;
                foreach (var kvp in trackStats.OrderBy(k => k.Key))
                {
                    var trackInfo = new TrackNoteInfo(
                        kvp.Key,
                        kvp.Value.count,
                        kvp.Value.minStart,
                        kvp.Value.maxEnd,
                        runningOffset,
                        kvp.Value.count * NOTE_BINARY_SIZE
                    );
                    _trackIndex[kvp.Key] = trackInfo;
                    runningOffset += kvp.Value.count * NOTE_BINARY_SIZE;

                    _logger.Debug("NoteVirtualizationService", 
                        $"轨道 {kvp.Key}: {kvp.Value.count} 个音符, 时间范围 [{kvp.Value.minStart:F2} - {kvp.Value.maxEnd:F2}]");
                }
            }, cancellationToken);

            _logger.Info("NoteVirtualizationService", $"索引构建完成，共 {_trackIndex.Count} 个轨道");
        }
        #endregion

        #region 轨道查询方法
        public async Task<IReadOnlyList<NoteData>> GetNotesInRangeAsync(
            int trackIndex, 
            double startTime, 
            double endTime, 
            CancellationToken cancellationToken = default)
        {
            if (!_isInitialized || string.IsNullOrEmpty(_cacheFilePath))
            {
                return Array.Empty<NoteData>();
            }

            if (!_trackIndex.TryGetValue(trackIndex, out var trackInfo))
            {
                return Array.Empty<NoteData>();
            }

            var result = new List<NoteData>();

            await Task.Run(() =>
            {
                using var stream = new FileStream(_cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
                using var reader = new BinaryReader(stream);

                // 跳转到轨道开始位置
                stream.Seek(trackInfo.FileOffset, SeekOrigin.Begin);

                var notesToRead = trackInfo.NoteCount;
                for (long i = 0; i < notesToRead && !cancellationToken.IsCancellationRequested; i++)
                {
                    var noteData = ReadNoteData(reader);
                    
                    // 检查是否在时间范围内
                    if (noteData.EndPosition >= startTime && noteData.StartPosition <= endTime)
                    {
                        result.Add(noteData);
                    }
                    
                    // 如果已经超过结束时间，可以提前退出（假设音符按时间排序）
                    // 但由于音符可能不是严格按时间排序的，我们继续读取整个轨道
                }
            }, cancellationToken);

            return result;
        }

        public TrackNoteInfo GetTrackInfo(int trackIndex)
        {
            if (_trackIndex.TryGetValue(trackIndex, out var info))
            {
                return info;
            }
            return default;
        }

        public IReadOnlyList<int> GetTrackIndices()
        {
            return _trackIndex.Keys.OrderBy(k => k).ToList();
        }
        #endregion

        #region 视口更新方法
        public void UpdateViewport(int trackIndex, double viewportStartTime, double viewportEndTime, double preloadBuffer = DEFAULT_PRELOAD_BUFFER)
        {
            lock (_lock)
            {
                // 检查是否需要更新
                bool needsUpdate = trackIndex != _currentTrackIndex ||
                                   viewportStartTime < _loadedStartTime ||
                                   viewportEndTime > _loadedEndTime;

                if (!needsUpdate)
                {
                    return;
                }

                _currentTrackIndex = trackIndex;
                _viewportStartTime = viewportStartTime;
                _viewportEndTime = viewportEndTime;

                // 计算加载范围（包含预加载缓冲区）
                var loadStartTime = viewportStartTime - preloadBuffer;
                var loadEndTime = viewportEndTime + preloadBuffer;

                // 异步加载音符
                _ = LoadViewportNotesAsync(trackIndex, loadStartTime, loadEndTime);
            }
        }

        private async Task LoadViewportNotesAsync(int trackIndex, double startTime, double endTime)
        {
            try
            {
                var notes = await GetNotesInRangeAsync(trackIndex, startTime, endTime);

                lock (_lock)
                {
                    _viewportNotes.Clear();
                    _viewportNotes.AddRange(notes);
                    _loadedStartTime = startTime;
                    _loadedEndTime = endTime;
                }

                _logger.Debug("NoteVirtualizationService", 
                    $"加载轨道 {trackIndex} 的 {notes.Count} 个音符，范围 [{startTime:F2} - {endTime:F2}]");
            }
            catch (Exception ex)
            {
                _logger.Error("NoteVirtualizationService", $"加载视口音符失败: {ex.Message}");
            }
        }

        public IReadOnlyList<NoteData> GetViewportNotes()
        {
            lock (_lock)
            {
                return _viewportNotes.ToList();
            }
        }
        #endregion

        #region 内存管理
        public void TrimCache()
        {
            // 当前实现中，视口外的音符已经被自动清理
            // 这个方法可以在未来用于更复杂的缓存策略
            GC.Collect(0, GCCollectionMode.Optimized);
        }

        public void ClearCache()
        {
            lock (_lock)
            {
                _viewportNotes.Clear();
                _loadedStartTime = 0;
                _loadedEndTime = 0;
            }
        }

        public MemoryUsageInfo GetMemoryUsage()
        {
            // NoteData是32字节的结构体
            var estimatedBytes = (long)_viewportNotes.Count * 32;
            return new MemoryUsageInfo(_viewportNotes.Count, _totalNoteCount, estimatedBytes);
        }
        #endregion

        #region 私有辅助方法
        private NoteData ReadNoteData(BinaryReader reader)
        {
            return new NoteData(
                reader.ReadInt32(),      // Pitch
                reader.ReadDouble(),     // StartPosition
                reader.ReadDouble(),     // Duration
                reader.ReadInt32(),      // Velocity
                reader.ReadInt32(),      // TrackIndex
                reader.ReadInt32()       // MidiChannel
            );
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ClearCache();
            _trackIndex.Clear();
            _isInitialized = false;

            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
