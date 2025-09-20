using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的音轨管理功能
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 音轨索引管理
        /// <summary>
        /// 当前音轨索引变化处理
        /// </summary>
        private void OnCurrentTrackIndexChanged()
        {
            UpdateCurrentTrackNotes();
            
            // 通知属性变化
            OnPropertyChanged(nameof(CurrentTrack));
            OnPropertyChanged(nameof(CurrentTrackName));
            OnPropertyChanged(nameof(CurrentTrackColor));
            OnPropertyChanged(nameof(CurrentTrackNotes));
            
            // 重置选择状态
            SelectedNotes.Clear();
            
            // 更新滚动位置以显示当前音轨
            UpdateScrollPositionForCurrentTrack();
        }



        /// <summary>
        /// 更新滚动位置以显示当前音轨
        /// </summary>
        private void UpdateScrollPositionForCurrentTrack()
        {
            if (CurrentTrackNotes == null || !CurrentTrackNotes.Any())
                return;
                
            // 计算当前音轨音符的时间范围
            var minTime = CurrentTrackNotes.Min(n => n.StartPosition).ToDouble();
            var maxTime = CurrentTrackNotes.Max(n => n.StartPosition + n.Duration).ToDouble();
            var centerTime = (minTime + maxTime) / 2;
            
            // 计算对应的像素位置
            var centerPixel = centerTime * TimeToPixelScale;
            
            // 调整滚动位置使当前音轨居中
            var targetOffset = Math.Max(0, centerPixel - ViewportWidth / 2);
            
            if (targetOffset != HorizontalScrollOffset)
            {
                HorizontalScrollOffset = targetOffset;
            }
        }
        #endregion

        #region 音轨操作
        /// <summary>
        /// 添加新音轨
        /// </summary>
        public void AddTrack(string? name = null, Color? color = null)
        {
            var track = new TrackViewModel(Tracks.Count, "", name ?? $"Track {Tracks.Count + 1}")
            {
                Color = color ?? GetNextTrackColor(),
                Index = Tracks.Count
            };
            
            Tracks.Add(track);
            
            // 如果这是第一个音轨，设置为当前音轨
            if (Tracks.Count == 1)
            {
                CurrentTrackIndex = 0;
            }
        }

        /// <summary>
        /// 删除音轨
        /// </summary>
        public void RemoveTrack(int trackIndex)
        {
            if (trackIndex < 0 || trackIndex >= Tracks.Count)
                return;
                
            // 删除该音轨的所有音符
            var notesToRemove = Notes.Where(n => n.TrackIndex == trackIndex).ToList();
            foreach (var note in notesToRemove)
            {
                Notes.Remove(note);
            }
            
            // 删除音轨
            Tracks.RemoveAt(trackIndex);
            
            // 更新剩余音轨的索引
            for (int i = trackIndex; i < Tracks.Count; i++)
            {
                Tracks[i].Index = i;
            }
            
            // 更新音符的音轨索引
            foreach (var note in Notes)
            {
                if (note.TrackIndex > trackIndex)
                {
                    note.TrackIndex--;
                }
            }
            
            // 调整当前音轨索引
            if (CurrentTrackIndex >= Tracks.Count && Tracks.Count > 0)
            {
                CurrentTrackIndex = Tracks.Count - 1;
            }
            else if (CurrentTrackIndex == trackIndex && Tracks.Count > 0)
            {
                CurrentTrackIndex = Math.Min(trackIndex, Tracks.Count - 1);
            }
            else if (Tracks.Count == 0)
            {
                CurrentTrackIndex = -1;
            }
            
            UpdateCurrentTrackNotes();
            UpdateMaxScrollExtent();
        }

        /// <summary>
        /// 复制音轨
        /// </summary>
        public void DuplicateTrack(int trackIndex)
        {
            if (trackIndex < 0 || trackIndex >= Tracks.Count)
                return;
                
            var sourceTrack = Tracks[trackIndex];
            var newTrack = new TrackViewModel(Tracks.Count, "", $"{sourceTrack.Name} (Copy)")
            {
                Color = sourceTrack.Color,
                Index = Tracks.Count
            };
            
            Tracks.Add(newTrack);
            
            // 复制音符
            var sourceNotes = Notes.Where(n => n.TrackIndex == trackIndex).ToList();
            foreach (var sourceNote in sourceNotes)
            {
                var newNote = new NoteViewModel
                {
                    Pitch = sourceNote.Pitch,
                    StartPosition = sourceNote.StartPosition,
                    Duration = sourceNote.Duration,
                    Velocity = sourceNote.Velocity,
                    TrackIndex = newTrack.Index
                };
                Notes.Add(newNote);
            }
            
            // 设置为当前音轨
            CurrentTrackIndex = newTrack.Index;
        }

        /// <summary>
        /// 移动音轨
        /// </summary>
        public void MoveTrack(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex || fromIndex < 0 || fromIndex >= Tracks.Count || toIndex < 0 || toIndex >= Tracks.Count)
                return;
                
            var track = Tracks[fromIndex];
            Tracks.RemoveAt(fromIndex);
            Tracks.Insert(toIndex, track);
            
            // 更新音轨索引
            for (int i = 0; i < Tracks.Count; i++)
            {
                Tracks[i].Index = i;
            }
            
            // 更新音符的音轨索引
            foreach (var note in Notes)
            {
                if (note.TrackIndex == fromIndex)
                {
                    note.TrackIndex = toIndex;
                }
                else if (fromIndex < toIndex)
                {
                    if (note.TrackIndex > fromIndex && note.TrackIndex <= toIndex)
                    {
                        note.TrackIndex--;
                    }
                }
                else
                {
                    if (note.TrackIndex >= toIndex && note.TrackIndex < fromIndex)
                    {
                        note.TrackIndex++;
                    }
                }
            }
            
            // 更新当前音轨索引
            if (CurrentTrackIndex == fromIndex)
            {
                CurrentTrackIndex = toIndex;
            }
            else if (fromIndex < toIndex && CurrentTrackIndex > fromIndex && CurrentTrackIndex <= toIndex)
            {
                CurrentTrackIndex--;
            }
            else if (fromIndex > toIndex && CurrentTrackIndex >= toIndex && CurrentTrackIndex < fromIndex)
            {
                CurrentTrackIndex++;
            }
            
            UpdateCurrentTrackNotes();
        }

        /// <summary>
        /// 重命名音轨
        /// </summary>
        public void RenameTrack(int trackIndex, string newName)
        {
            if (trackIndex < 0 || trackIndex >= Tracks.Count)
                return;
                
            Tracks[trackIndex].Name = newName;
            
            if (CurrentTrackIndex == trackIndex)
            {
                OnPropertyChanged(nameof(CurrentTrackName));
            }
        }

        /// <summary>
        /// 设置音轨颜色
        /// </summary>
        public void SetTrackColor(int trackIndex, Color color)
        {
            if (trackIndex < 0 || trackIndex >= Tracks.Count)
                return;
                
            Tracks[trackIndex].Color = color;
            
            if (CurrentTrackIndex == trackIndex)
            {
                OnPropertyChanged(nameof(CurrentTrackColor));
            }
        }

        /// <summary>
        /// 静音音轨
        /// </summary>
        public void MuteTrack(int trackIndex, bool muted)
        {
            if (trackIndex < 0 || trackIndex >= Tracks.Count)
                return;
                
            Tracks[trackIndex].IsMuted = muted;
        }

        /// <summary>
        /// 独奏音轨
        /// </summary>
        public void SoloTrack(int trackIndex, bool solo)
        {
            if (trackIndex < 0 || trackIndex >= Tracks.Count)
                return;
                
            if (solo)
            {
                // 独奏模式：关闭其他所有音轨的独奏，关闭当前音轨的静音
                for (int i = 0; i < Tracks.Count; i++)
                {
                    if (i != trackIndex)
                    {
                        Tracks[i].IsSolo = false;
                        Tracks[i].IsMuted = true;
                    }
                    else
                    {
                        Tracks[i].IsSolo = true;
                        Tracks[i].IsMuted = false;
                    }
                }
            }
            else
            {
                // 取消独奏：只影响当前音轨
                Tracks[trackIndex].IsSolo = false;
            }
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取下一个音轨颜色
        /// </summary>
        private Color GetNextTrackColor()
        {
            var colors = new[]
            {
                Colors.Blue,
                Colors.Green,
                Colors.Red,
                Colors.Purple,
                Colors.Orange,
                Colors.Teal,
                Colors.Pink,
                Colors.Yellow,
                Colors.Cyan,
                Colors.Magenta
            };
            
            return colors[Tracks.Count % colors.Length];
        }

        /// <summary>
        /// 获取音轨统计信息
        /// </summary>
        public TrackStatistics GetTrackStatistics(int trackIndex)
        {
            if (trackIndex < 0 || trackIndex >= Tracks.Count)
                return new TrackStatistics();
                
            var trackNotes = Notes.Where(n => n.TrackIndex == trackIndex).ToList();
            
            if (!trackNotes.Any())
                return new TrackStatistics();
                
            return new TrackStatistics
            {
                NoteCount = trackNotes.Count,
                MinPitch = trackNotes.Min(n => n.Pitch),
                MaxPitch = trackNotes.Max(n => n.Pitch),
                MinTime = trackNotes.Min(n => n.StartPosition).ToDouble(),
                MaxTime = trackNotes.Max(n => n.StartPosition + n.Duration).ToDouble(),
                AverageVelocity = trackNotes.Average(n => n.Velocity),
                Duration = trackNotes.Max(n => n.StartPosition + n.Duration).ToDouble() - trackNotes.Min(n => n.StartPosition).ToDouble()
            };
        }

        /// <summary>
        /// 获取所有音轨的统计信息
        /// </summary>
        public Dictionary<int, TrackStatistics> GetAllTrackStatistics()
        {
            var statistics = new Dictionary<int, TrackStatistics>();
            
            for (int i = 0; i < Tracks.Count; i++)
            {
                statistics[i] = GetTrackStatistics(i);
            }
            
            return statistics;
        }
        #endregion
    }

    /// <summary>
    /// 音轨统计信息
    /// </summary>
    public class TrackStatistics
    {
        public int NoteCount { get; set; }
        public int MinPitch { get; set; }
        public int MaxPitch { get; set; }
        public double MinTime { get; set; }
        public double MaxTime { get; set; }
        public double AverageVelocity { get; set; }
        public double Duration { get; set; }
    }
}