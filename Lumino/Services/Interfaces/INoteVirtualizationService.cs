using Lumino.Models.Music;
using Lumino.ViewModels.Editor;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lumino.Services.Interfaces
{
    /// <summary>
    /// 音符虚拟化服务接口 - 实现只加载可见区域音符的虚拟化存储
    /// 用于处理大规模音符集合(百万级别)的高效内存管理
    /// </summary>
    public interface INoteVirtualizationService : IDisposable
    {
        #region 属性
        /// <summary>
        /// 总音符数量
        /// </summary>
        long TotalNoteCount { get; }

        /// <summary>
        /// 当前已加载到内存的音符数量
        /// </summary>
        int LoadedNoteCount { get; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 当前加载的轨道索引（-1表示全部轨道）
        /// </summary>
        int CurrentTrackIndex { get; }
        #endregion

        #region 初始化方法
        /// <summary>
        /// 从临时缓存初始化虚拟化服务
        /// </summary>
        /// <param name="cacheService">临时缓存服务</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task InitializeFromCacheAsync(ITempProjectCacheService cacheService, CancellationToken cancellationToken = default);

        /// <summary>
        /// 构建音符索引（按轨道和时间位置）
        /// </summary>
        Task BuildIndexAsync(CancellationToken cancellationToken = default);
        #endregion

        #region 轨道查询方法
        /// <summary>
        /// 获取指定轨道在指定时间范围内的音符
        /// </summary>
        /// <param name="trackIndex">轨道索引</param>
        /// <param name="startTime">开始时间（四分音符单位）</param>
        /// <param name="endTime">结束时间（四分音符单位）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>该范围内的音符列表</returns>
        Task<IReadOnlyList<NoteData>> GetNotesInRangeAsync(
            int trackIndex,
            double startTime,
            double endTime,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取指定轨道的所有音符索引信息
        /// </summary>
        /// <param name="trackIndex">轨道索引</param>
        /// <returns>轨道音符统计信息</returns>
        TrackNoteInfo GetTrackInfo(int trackIndex);

        /// <summary>
        /// 获取所有轨道列表
        /// </summary>
        IReadOnlyList<int> GetTrackIndices();
        #endregion

        #region 视口更新方法
        /// <summary>
        /// 更新视口位置，触发音符预加载
        /// </summary>
        /// <param name="trackIndex">当前轨道索引</param>
        /// <param name="viewportStartTime">视口开始时间</param>
        /// <param name="viewportEndTime">视口结束时间</param>
        /// <param name="preloadBuffer">预加载缓冲区大小（时间单位）</param>
        void UpdateViewport(int trackIndex, double viewportStartTime, double viewportEndTime, double preloadBuffer = 4.0);

        /// <summary>
        /// 获取当前视口内的音符（同步方法，从缓存返回）
        /// </summary>
        IReadOnlyList<NoteData> GetViewportNotes();
        #endregion

        #region 内存管理
        /// <summary>
        /// 清理不在视口范围内的音符缓存
        /// </summary>
        void TrimCache();

        /// <summary>
        /// 完全清理缓存
        /// </summary>
        void ClearCache();

        /// <summary>
        /// 获取内存使用统计
        /// </summary>
        MemoryUsageInfo GetMemoryUsage();
        #endregion
    }

    /// <summary>
    /// 轻量级音符数据结构 - 仅包含必要字段，减少内存占用
    /// 32字节对齐，与缓存文件格式一致
    /// </summary>
    public readonly struct NoteData
    {
        public readonly int Pitch;
        public readonly double StartPosition;
        public readonly double Duration;
        public readonly int Velocity;
        public readonly int TrackIndex;
        public readonly int MidiChannel;

        public NoteData(int pitch, double startPosition, double duration, int velocity, int trackIndex, int midiChannel)
        {
            Pitch = pitch;
            StartPosition = startPosition;
            Duration = duration;
            Velocity = velocity;
            TrackIndex = trackIndex;
            MidiChannel = midiChannel;
        }

        /// <summary>
        /// 从Note模型创建
        /// </summary>
        public static NoteData FromNote(Note note)
        {
            return new NoteData(
                note.Pitch,
                note.StartPosition.ToDouble(),
                note.Duration.ToDouble(),
                note.Velocity,
                note.TrackIndex,
                note.MidiChannel
            );
        }

        /// <summary>
        /// 结束位置
        /// </summary>
        public double EndPosition => StartPosition + Duration;
    }

    /// <summary>
    /// 轨道音符信息
    /// </summary>
    public readonly struct TrackNoteInfo
    {
        public readonly int TrackIndex;
        public readonly long NoteCount;
        public readonly double MinStartTime;
        public readonly double MaxEndTime;
        public readonly long FileOffset;
        public readonly long ByteLength;

        public TrackNoteInfo(int trackIndex, long noteCount, double minStartTime, double maxEndTime, long fileOffset, long byteLength)
        {
            TrackIndex = trackIndex;
            NoteCount = noteCount;
            MinStartTime = minStartTime;
            MaxEndTime = maxEndTime;
            FileOffset = fileOffset;
            ByteLength = byteLength;
        }
    }

    /// <summary>
    /// 内存使用信息
    /// </summary>
    public readonly struct MemoryUsageInfo
    {
        public readonly long LoadedNotes;
        public readonly long TotalNotes;
        public readonly long EstimatedMemoryBytes;
        public readonly double LoadedPercentage;

        public MemoryUsageInfo(long loadedNotes, long totalNotes, long estimatedMemoryBytes)
        {
            LoadedNotes = loadedNotes;
            TotalNotes = totalNotes;
            EstimatedMemoryBytes = estimatedMemoryBytes;
            LoadedPercentage = totalNotes > 0 ? (double)loadedNotes / totalNotes * 100 : 0;
        }
    }
}
