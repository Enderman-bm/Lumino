using System;
using System.Collections.Generic;
using System.Linq;
using DominoNext.ViewModels.Editor;
using DominoNext.Models.Music;

namespace DominoNext.Services.Implementation
{
    /// <summary>
    /// 高性能音符索引和查询服务
    /// 针对大量音符（10万+）的随机存取优化
    /// </summary>
    public class NoteIndexService
    {
        // 时间范围索引：按时间段分组存储音符
        private readonly Dictionary<int, List<NoteViewModel>> _timeRangeIndex = new();
        
        // 音高索引：按音高分组存储音符
        private readonly Dictionary<int, List<NoteViewModel>> _pitchIndex = new();
        
        // 二维空间索引：按时间和音高的组合索引
        private readonly Dictionary<long, List<NoteViewModel>> _spatialIndex = new();
        
        // 全局音符列表的快速引用
        private readonly HashSet<NoteViewModel> _allNotes = new();
        
        // 索引配置
        private const int TIME_BUCKET_SIZE = 1920; // 每个时间桶的tick大小（相当于20个四分音符）
        private const int PITCH_BUCKET_SIZE = 12; // 每个音高桶的大小（一个八度）
        
        // 脏标记
        private bool _isDirty = false;
        private DateTime _lastRebuildTime = DateTime.MinValue;
        
        /// <summary>
        /// 添加音符到索引
        /// </summary>
        public void AddNote(NoteViewModel note, int ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS)
        {
            if (_allNotes.Contains(note)) return;
            
            _allNotes.Add(note);
            AddToTimeIndex(note, ticksPerBeat);
            AddToPitchIndex(note);
            AddToSpatialIndex(note, ticksPerBeat);
            
            _isDirty = true;
        }
        
        /// <summary>
        /// 从索引中移除音符
        /// </summary>
        public void RemoveNote(NoteViewModel note, int ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS)
        {
            if (!_allNotes.Contains(note)) return;
            
            _allNotes.Remove(note);
            RemoveFromTimeIndex(note, ticksPerBeat);
            RemoveFromPitchIndex(note);
            RemoveFromSpatialIndex(note, ticksPerBeat);
            
            _isDirty = true;
        }
        
        /// <summary>
        /// 批量添加音符
        /// </summary>
        public void AddNotes(IEnumerable<NoteViewModel> notes, int ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS)
        {
            foreach (var note in notes)
            {
                if (_allNotes.Add(note))
                {
                    AddToTimeIndex(note, ticksPerBeat);
                    AddToPitchIndex(note);
                    AddToSpatialIndex(note, ticksPerBeat);
                }
            }
            _isDirty = true;
        }
        
        /// <summary>
        /// 清除所有索引
        /// </summary>
        public void Clear()
        {
            _timeRangeIndex.Clear();
            _pitchIndex.Clear();
            _spatialIndex.Clear();
            _allNotes.Clear();
            _isDirty = false;
        }
        
        /// <summary>
        /// 查找指定时间范围内的音符
        /// </summary>
        public IEnumerable<NoteViewModel> FindNotesInTimeRange(double startTicks, double endTicks, int ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS)
        {
            var startBucket = GetTimeBucket(startTicks);
            var endBucket = GetTimeBucket(endTicks);
            
            var result = new HashSet<NoteViewModel>();
            
            for (int bucket = startBucket; bucket <= endBucket; bucket++)
            {
                if (_timeRangeIndex.TryGetValue(bucket, out var notes))
                {
                    foreach (var note in notes)
                    {
                        var noteStart = note.StartPosition.ToTicks(ticksPerBeat);
                        var noteEnd = noteStart + note.Duration.ToTicks(ticksPerBeat);
                        
                        if (noteEnd >= startTicks && noteStart <= endTicks)
                        {
                            result.Add(note);
                        }
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 查找指定音高范围内的音符
        /// </summary>
        public IEnumerable<NoteViewModel> FindNotesInPitchRange(int minPitch, int maxPitch)
        {
            var result = new HashSet<NoteViewModel>();
            
            var startBucket = GetPitchBucket(minPitch);
            var endBucket = GetPitchBucket(maxPitch);
            
            for (int bucket = startBucket; bucket <= endBucket; bucket++)
            {
                if (_pitchIndex.TryGetValue(bucket, out var notes))
                {
                    foreach (var note in notes)
                    {
                        if (note.Pitch >= minPitch && note.Pitch <= maxPitch)
                        {
                            result.Add(note);
                        }
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 查找指定矩形区域内的音符（最常用的查询）
        /// </summary>
        public IEnumerable<NoteViewModel> FindNotesInRect(double startTicks, double endTicks, int minPitch, int maxPitch, int ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS)
        {
            var timeNotes = FindNotesInTimeRange(startTicks, endTicks, ticksPerBeat);
            var result = new List<NoteViewModel>();
            
            foreach (var note in timeNotes)
            {
                if (note.Pitch >= minPitch && note.Pitch <= maxPitch)
                {
                    result.Add(note);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 快速查找视口内的音符（渲染优化）
        /// </summary>
        public IEnumerable<NoteViewModel> FindNotesInViewport(
            double viewportStartTicks, 
            double viewportEndTicks, 
            int viewportMinPitch, 
            int viewportMaxPitch,
            int ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS)
        {
            // 使用空间索引进行快速查找
            var result = new HashSet<NoteViewModel>();
            
            var startTimeBucket = GetTimeBucket(viewportStartTicks);
            var endTimeBucket = GetTimeBucket(viewportEndTicks);
            var startPitchBucket = GetPitchBucket(viewportMinPitch);
            var endPitchBucket = GetPitchBucket(viewportMaxPitch);
            
            for (int timeBucket = startTimeBucket; timeBucket <= endTimeBucket; timeBucket++)
            {
                for (int pitchBucket = startPitchBucket; pitchBucket <= endPitchBucket; pitchBucket++)
                {
                    var spatialKey = ((long)timeBucket << 32) | (uint)pitchBucket;
                    
                    if (_spatialIndex.TryGetValue(spatialKey, out var notes))
                    {
                        foreach (var note in notes)
                        {
                            var noteStart = note.StartPosition.ToTicks(ticksPerBeat);
                            var noteEnd = noteStart + note.Duration.ToTicks(ticksPerBeat);
                            
                            if (noteEnd >= viewportStartTicks && noteStart <= viewportEndTicks &&
                                note.Pitch >= viewportMinPitch && note.Pitch <= viewportMaxPitch)
                            {
                                result.Add(note);
                            }
                        }
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 查找与指定音符重叠的音符
        /// </summary>
        public IEnumerable<NoteViewModel> FindOverlappingNotes(NoteViewModel targetNote, int ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS)
        {
            var startTicks = targetNote.StartPosition.ToTicks(ticksPerBeat);
            var endTicks = startTicks + targetNote.Duration.ToTicks(ticksPerBeat);
            
            return FindNotesInRect(startTicks, endTicks, targetNote.Pitch, targetNote.Pitch, ticksPerBeat)
                .Where(note => note != targetNote);
        }
        
        /// <summary>
        /// 获取索引统计信息
        /// </summary>
        public IndexStatistics GetStatistics()
        {
            return new IndexStatistics
            {
                TotalNotes = _allNotes.Count,
                TimeBuckets = _timeRangeIndex.Count,
                PitchBuckets = _pitchIndex.Count,
                SpatialBuckets = _spatialIndex.Count,
                IsDirty = _isDirty,
                LastRebuildTime = _lastRebuildTime
            };
        }
        
        /// <summary>
        /// 重建索引（在大量修改后调用）
        /// </summary>
        public void RebuildIndex(IEnumerable<NoteViewModel> allNotes, int ticksPerBeat = MusicalFraction.QUARTER_NOTE_TICKS)
        {
            Clear();
            AddNotes(allNotes, ticksPerBeat);
            _lastRebuildTime = DateTime.UtcNow;
            _isDirty = false;
        }
        
        // 私有辅助方法
        private void AddToTimeIndex(NoteViewModel note, int ticksPerBeat)
        {
            var timeBucket = GetTimeBucket(note.StartPosition.ToTicks(ticksPerBeat));
            if (!_timeRangeIndex.TryGetValue(timeBucket, out var notes))
            {
                notes = new List<NoteViewModel>();
                _timeRangeIndex[timeBucket] = notes;
            }
            notes.Add(note);
        }
        
        private void AddToPitchIndex(NoteViewModel note)
        {
            var pitchBucket = GetPitchBucket(note.Pitch);
            if (!_pitchIndex.TryGetValue(pitchBucket, out var notes))
            {
                notes = new List<NoteViewModel>();
                _pitchIndex[pitchBucket] = notes;
            }
            notes.Add(note);
        }
        
        private void AddToSpatialIndex(NoteViewModel note, int ticksPerBeat)
        {
            var timeBucket = GetTimeBucket(note.StartPosition.ToTicks(ticksPerBeat));
            var pitchBucket = GetPitchBucket(note.Pitch);
            var spatialKey = ((long)timeBucket << 32) | (uint)pitchBucket;
            
            if (!_spatialIndex.TryGetValue(spatialKey, out var notes))
            {
                notes = new List<NoteViewModel>();
                _spatialIndex[spatialKey] = notes;
            }
            notes.Add(note);
        }
        
        private void RemoveFromTimeIndex(NoteViewModel note, int ticksPerBeat)
        {
            var timeBucket = GetTimeBucket(note.StartPosition.ToTicks(ticksPerBeat));
            if (_timeRangeIndex.TryGetValue(timeBucket, out var notes))
            {
                notes.Remove(note);
                if (notes.Count == 0)
                    _timeRangeIndex.Remove(timeBucket);
            }
        }
        
        private void RemoveFromPitchIndex(NoteViewModel note)
        {
            var pitchBucket = GetPitchBucket(note.Pitch);
            if (_pitchIndex.TryGetValue(pitchBucket, out var notes))
            {
                notes.Remove(note);
                if (notes.Count == 0)
                    _pitchIndex.Remove(pitchBucket);
            }
        }
        
        private void RemoveFromSpatialIndex(NoteViewModel note, int ticksPerBeat)
        {
            var timeBucket = GetTimeBucket(note.StartPosition.ToTicks(ticksPerBeat));
            var pitchBucket = GetPitchBucket(note.Pitch);
            var spatialKey = ((long)timeBucket << 32) | (uint)pitchBucket;
            
            if (_spatialIndex.TryGetValue(spatialKey, out var notes))
            {
                notes.Remove(note);
                if (notes.Count == 0)
                    _spatialIndex.Remove(spatialKey);
            }
        }
        
        private int GetTimeBucket(double ticks) => (int)(ticks / TIME_BUCKET_SIZE);
        private int GetPitchBucket(int pitch) => pitch / PITCH_BUCKET_SIZE;
    }
    
    /// <summary>
    /// 索引统计信息
    /// </summary>
    public class IndexStatistics
    {
        public int TotalNotes { get; set; }
        public int TimeBuckets { get; set; }
        public int PitchBuckets { get; set; }
        public int SpatialBuckets { get; set; }
        public bool IsDirty { get; set; }
        public DateTime LastRebuildTime { get; set; }
    }
}