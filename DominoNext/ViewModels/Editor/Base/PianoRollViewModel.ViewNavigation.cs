using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的视图和缩放功能
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 视图导航命令
        /// <summary>
        /// 缩放到选定区域命令
        /// </summary>
        [RelayCommand]
        private void ZoomToSelection()
        {
            if (!SelectedNotes.Any()) return;

            var selectedNotes = SelectedNotes.ToList();
            if (!selectedNotes.Any()) return;

            // 计算选定区域的边界
            var minTime = selectedNotes.Min(n => n.StartTime);
            var maxTime = selectedNotes.Max(n => n.StartTime + n.Duration.ToDouble());
            var minPitch = selectedNotes.Min(n => n.Pitch);
            var maxPitch = selectedNotes.Max(n => n.Pitch);

            // 添加一些边距
            var timeMargin = (maxTime - minTime) * 0.1;
            var pitchMargin = Math.Max(1, (maxPitch - minPitch) / 10);

            minTime -= timeMargin;
            maxTime += timeMargin;
            minPitch -= pitchMargin;
            maxPitch += pitchMargin;

            // 确保音高范围合理
            minPitch = Math.Max(0, minPitch);
            maxPitch = Math.Min(127, maxPitch);

            // 缩放到选定区域
            ZoomToRegion(minTime, maxTime, minPitch, maxPitch);
        }

        /// <summary>
        /// 缩放到整个歌曲命令
        /// </summary>
        [RelayCommand]
        private void ZoomToFit()
        {
            if (!Notes.Any()) return;

            // 计算整个歌曲的边界
            var minTime = 0.0;
            var maxTime = SongLengthInSeconds;
            var minPitch = Notes.Any() ? Notes.Min(n => n.Pitch) : 60;
            var maxPitch = Notes.Any() ? Notes.Max(n => n.Pitch) : 72;

            // 添加一些边距
            var timeMargin = (maxTime - minTime) * 0.05;
            var pitchMargin = Math.Max(1, (maxPitch - minPitch) / 10);

            minTime -= timeMargin;
            maxTime += timeMargin;
            minPitch -= pitchMargin;
            maxPitch += pitchMargin;

            // 确保音高范围合理
            minPitch = Math.Max(0, minPitch);
            maxPitch = Math.Min(127, maxPitch);

            // 缩放到整个歌曲
            ZoomToRegion(minTime, maxTime, minPitch, maxPitch);
        }

        /// <summary>
        /// 缩放到当前音轨命令
        /// </summary>
        [RelayCommand]
        private void ZoomToCurrentTrack()
        {
            if (CurrentTrack == null) return;

            var trackNotes = CurrentTrackNotes.ToList();
            if (!trackNotes.Any()) return;

            // 计算当前音轨的边界
            var minTime = trackNotes.Min(n => n.StartTime);
            var maxTime = trackNotes.Max(n => n.StartTime + n.Duration.ToDouble());
            var minPitch = trackNotes.Min(n => n.Pitch);
            var maxPitch = trackNotes.Max(n => n.Pitch);

            // 添加一些边距
            var timeMargin = (maxTime - minTime) * 0.1;
            var pitchMargin = Math.Max(1, (maxPitch - minPitch) / 10);

            minTime -= timeMargin;
            maxTime += timeMargin;
            minPitch -= pitchMargin;
            maxPitch += pitchMargin;

            // 确保音高范围合理
            minPitch = Math.Max(0, minPitch);
            maxPitch = Math.Min(127, maxPitch);

            // 缩放到当前音轨
            ZoomToRegion(minTime, maxTime, minPitch, maxPitch);
        }

        /// <summary>
        /// 居中到当前播放位置命令
        /// </summary>
        [RelayCommand]
        private void CenterToPlaybackPosition()
        {
            var playbackTime = CurrentPlaybackTime;
            var playbackPitch = 60; // 默认中央C

            // 计算视口中心位置
            var centerX = playbackTime * TimeToPixelScale;
            var centerY = (127 - playbackPitch) * PitchToPixelScale;

            // 设置视口位置
            Viewport.SetHorizontalScrollOffset(centerX - Viewport.ViewportWidth / 2);
            Viewport.SetVerticalScrollOffset(centerY - Viewport.ViewportHeight / 2, 127 * PitchToPixelScale);

            // 确保视口在合理范围内
            ClampViewport();
        }

        /// <summary>
        /// 居中到选定音符命令
        /// </summary>
        [RelayCommand]
        private void CenterToSelectedNotes()
        {
            if (!SelectedNotes.Any()) return;

            var selectedNotes = SelectedNotes.ToList();
            if (!selectedNotes.Any()) return;

            // 计算选定区域的中心
            var minTime = selectedNotes.Min(n => n.StartTime);
            var maxTime = selectedNotes.Max(n => n.StartTime + n.Duration.ToDouble());
            var minPitch = selectedNotes.Min(n => n.Pitch);
            var maxPitch = selectedNotes.Max(n => n.Pitch);

            var centerTime = (minTime + maxTime) / 2;
            var centerPitch = (minPitch + maxPitch) / 2;

            // 计算视口中心位置
            var centerX = centerTime * TimeToPixelScale;
            var centerY = (127 - centerPitch) * PitchToPixelScale;

            // 设置视口位置
            Viewport.SetHorizontalScrollOffset(centerX - Viewport.ViewportWidth / 2);
            Viewport.SetVerticalScrollOffset(centerY - Viewport.ViewportHeight / 2, 127 * PitchToPixelScale);

            // 确保视口在合理范围内
            ClampViewport();
        }

        /// <summary>
        /// 缩放到特定区域
        /// </summary>
        private void ZoomToRegion(double minTime, double maxTime, double minPitch, double maxPitch)
        {
            if (maxTime <= minTime || maxPitch <= minPitch) return;

            // 计算目标缩放级别
            var timeRange = maxTime - minTime;
            var pitchRange = maxPitch - minPitch;

            var targetTimeZoom = Viewport.ViewportWidth / timeRange;
            var targetPitchZoom = Viewport.ViewportHeight / pitchRange;

            // 使用较小的缩放级别以确保整个区域都能显示
            var targetZoom = Math.Min(targetTimeZoom, targetPitchZoom);

            // 应用缩放 - TimeToPixelScale是只读属性，通过ZoomManager设置
            // TimeToPixelScale = targetZoom; // 错误：TimeToPixelScale是只读的
            PitchToPixelScale = targetZoom;

            // 计算视口中心位置
            var centerTime = (minTime + maxTime) / 2;
            var centerPitch = (minPitch + maxPitch) / 2;

            var centerX = centerTime * TimeToPixelScale;
            var centerY = (127 - centerPitch) * PitchToPixelScale;

            // 设置视口位置
            Viewport.SetHorizontalScrollOffset(centerX - Viewport.ViewportWidth / 2);
            Viewport.SetVerticalScrollOffset(centerY - Viewport.ViewportHeight / 2, 127 * PitchToPixelScale);

            // 确保视口在合理范围内
            ClampViewport();
        }
        #endregion

        #region 缩放控制命令
        /// <summary>
        /// 放大命令
        /// </summary>
        [RelayCommand]
        private void ZoomIn()
        {
            // TimeToPixelScale是只读属性，通过ZoomManager设置
            // TimeToPixelScale *= 1.2; // 错误：TimeToPixelScale是只读的
            ZoomManager.SetZoom(ZoomManager.Zoom * 1.2);
            PitchToPixelScale *= 1.2;
            
            // 保持视口中心位置
            var centerX = Viewport.CurrentScrollOffset + Viewport.ViewportWidth / 2;
            var centerY = Viewport.VerticalScrollOffset + Viewport.ViewportHeight / 2;
            
            Viewport.SetHorizontalScrollOffset(centerX - Viewport.ViewportWidth / 2);
            Viewport.SetVerticalScrollOffset(centerY - Viewport.ViewportHeight / 2, 127 * PitchToPixelScale);
            
            ClampViewport();
        }

        /// <summary>
        /// 缩小命令
        /// </summary>
        [RelayCommand]
        private void ZoomOut()
        {
            ZoomManager.SetZoom(ZoomManager.Zoom / 1.2);
            PitchToPixelScale /= 1.2;
            
            // 保持视口中心位置
            var centerX = Viewport.CurrentScrollOffset + Viewport.ViewportWidth / 2;
            var centerY = Viewport.VerticalScrollOffset + Viewport.ViewportHeight / 2;
            
            Viewport.SetHorizontalScrollOffset(centerX - Viewport.ViewportWidth / 2);
            Viewport.SetVerticalScrollOffset(centerY - Viewport.ViewportHeight / 2, 127 * PitchToPixelScale);
            
            ClampViewport();
        }

        /// <summary>
        /// 重置缩放命令
        /// </summary>
        [RelayCommand]
        private void ResetZoom()
        {
            ZoomManager.SetZoom(1.0);
            PitchToPixelScale = 1.0;
            
            ClampViewport();
        }

        /// <summary>
        /// 水平缩放命令
        /// </summary>
        [RelayCommand]
        private void ZoomHorizontal(double factor)
        {
            ZoomManager.SetZoom(ZoomManager.Zoom * factor);
            ClampViewport();
        }

        /// <summary>
        /// 垂直缩放命令
        /// </summary>
        [RelayCommand]
        private void ZoomVertical(double factor)
        {
            PitchToPixelScale *= factor;
            ClampViewport();
        }

        /// <summary>
        /// 缩放到特定时间范围命令
        /// </summary>
        private void ZoomToTimeRange(double startTime, double endTime)
        {
            if (endTime <= startTime) return;
            
            var timeRange = endTime - startTime;
            var newTimeToPixelScale = Viewport.ViewportWidth / timeRange;
            // TimeToPixelScale is read-only, we need to set it through ZoomManager.Zoom
        // which will automatically update TimeToPixelScale via PianoRollCalculations
        var baseWidth = newTimeToPixelScale;
        var targetZoom = baseWidth / 100.0; // BaseQuarterNoteWidth = 100.0 * ZoomManager.Zoom
        ZoomManager.SetZoom(targetZoom);
            
            // 居中到时间范围
            var centerTime = (startTime + endTime) / 2;
            var centerX = centerTime * TimeToPixelScale;
            Viewport.SetHorizontalScrollOffset(centerX - Viewport.ViewportWidth / 2);
            
            ClampViewport();
        }

        /// <summary>
        /// 缩放到特定音高范围命令
        /// </summary>
        private void ZoomToPitchRange(double minPitch, double maxPitch)
        {
            if (maxPitch <= minPitch) return;
            
            var pitchRange = maxPitch - minPitch;
            PitchToPixelScale = Viewport.ViewportHeight / pitchRange;
            
            // 居中到音高范围
            var centerPitch = (minPitch + maxPitch) / 2;
            var centerY = (127 - centerPitch) * PitchToPixelScale;
            Viewport.SetVerticalScrollOffset(centerY - Viewport.ViewportHeight / 2, 127 * PitchToPixelScale);
            
            ClampViewport();
        }
        #endregion

        #region 视图导航属性

        /// <summary>
        /// 音高到像素缩放比例
        /// </summary>
        public double PitchToPixelScale
        {
            get => _pitchToPixelScale;
            set
            {
                if (SetProperty(ref _pitchToPixelScale, Math.Max(0.01, Math.Min(1000, value))))
            {
                OnPropertyChanged(nameof(PitchToPixelScaleText));
            }
            }
        }
        private double _pitchToPixelScale = 1.0;

        /// <summary>
        /// 时间缩放文本
        /// </summary>
        public string TimeToPixelScaleText => $"{TimeToPixelScale:F1}x";

        /// <summary>
        /// 音高缩放文本
        /// </summary>
        public string PitchToPixelScaleText => $"{PitchToPixelScale:F1}x";

        // 这些属性现在由 ObservableProperty 特性自动生成
        // public double VisibleTimeRange => Viewport.Width / TimeToPixelScale;
        // public double VisiblePitchRange => Viewport.Height / PitchToPixelScale;

        /// <summary>
        /// 当前视口左边界时间
        /// </summary>
        public double ViewportLeftTime => Viewport.CurrentScrollOffset / TimeToPixelScale;

        /// <summary>
        /// 当前视口右边界时间
        /// </summary>
        public double ViewportRightTime => (Viewport.CurrentScrollOffset + Viewport.ViewportWidth) / TimeToPixelScale;

        /// <summary>
        /// 当前视口顶部音高
        /// </summary>
        public double ViewportTopPitch => 127 - (Viewport.VerticalScrollOffset / PitchToPixelScale);

        /// <summary>
        /// 当前视口底部音高
        /// </summary>
        public double ViewportBottomPitch => 127 - ((Viewport.VerticalScrollOffset + Viewport.ViewportHeight) / PitchToPixelScale);
        #endregion

        #region 视图辅助方法
        /// <summary>
        /// 强制刷新滚动系统
        /// </summary>
        public void ForceRefreshScrollSystem()
        {
            Viewport.ValidateAndClampScrollOffsets();
            // ScrollBarManager已废弃，使用Viewport替代
            // ScrollBarManager?.ForceUpdateScrollBars();
        }

        /// <summary>
        /// 设置当前水平滚动偏移量
        /// </summary>
        public void SetCurrentScrollOffset(double offset)
        {
            Viewport.SetHorizontalScrollOffset(offset);
        }

        /// <summary>
        /// 设置垂直滚动偏移量
        /// </summary>
        public void SetVerticalScrollOffset(double offset)
        {
            Viewport.SetVerticalScrollOffset(offset, 127 * PitchToPixelScale);
        }

        /// <summary>
        /// 限制视口范围
        /// </summary>
        private void ClampViewport()
        {
            var maxX = Math.Max(0, SongLengthInSeconds * TimeToPixelScale - Viewport.ViewportWidth);
            var maxY = Math.Max(0, 127 * PitchToPixelScale - Viewport.ViewportHeight);

            Viewport.SetHorizontalScrollOffset(Math.Max(0, Math.Min(Viewport.CurrentScrollOffset, maxX)));
            Viewport.SetVerticalScrollOffset(Math.Max(0, Math.Min(Viewport.VerticalScrollOffset, maxY)), 127 * PitchToPixelScale);
        }

        /// <summary>
        /// 滚动到时间位置
        /// </summary>
        public void ScrollToTime(double time)
        {
            var targetX = time * TimeToPixelScale - Viewport.ViewportWidth / 2;
            Viewport.SetHorizontalScrollOffset(Math.Max(0, Math.Min(targetX, SongLengthInSeconds * TimeToPixelScale - Viewport.ViewportWidth)));
        }

        /// <summary>
        /// 滚动到音高位置
        /// </summary>
        public void ScrollToPitch(int pitch)
        {
            var targetY = (127 - pitch) * PitchToPixelScale - Viewport.ViewportHeight / 2;
            Viewport.SetVerticalScrollOffset(Math.Max(0, Math.Min(targetY, 127 * PitchToPixelScale - Viewport.ViewportHeight)), 127 * PitchToPixelScale);
        }

        /// <summary>
        /// 确保时间位置在视口中可见
        /// </summary>
        public void EnsureTimeVisible(double time)
        {
            var timeX = time * TimeToPixelScale;
            
            if (timeX < Viewport.CurrentScrollOffset)
            {
                ScrollToTime(time);
            }
            else if (timeX > Viewport.CurrentScrollOffset + Viewport.ViewportWidth)
            {
                ScrollToTime(time);
            }
        }

        /// <summary>
        /// 确保音高位置在视口中可见
        /// </summary>
        public void EnsurePitchVisible(int pitch)
        {
            var pitchY = (127 - pitch) * PitchToPixelScale;
            
            if (pitchY < Viewport.VerticalScrollOffset)
            {
                ScrollToPitch(pitch);
            }
            else if (pitchY > Viewport.VerticalScrollOffset + Viewport.ViewportHeight)
            {
                ScrollToPitch(pitch);
            }
        }

        /// <summary>
        /// 确保音符在视口中可见
        /// </summary>
        public void EnsureNoteVisible(NoteViewModel note)
        {
            if (note == null) return;
            
            EnsureTimeVisible(note.StartTime);
            EnsureTimeVisible(note.StartTime + note.Duration.ToDouble());
            EnsurePitchVisible(note.Pitch);
        }

        /// <summary>
        /// 确保选定音符在视口中可见
        /// </summary>
        public void EnsureSelectedNotesVisible()
        {
            if (!SelectedNotes.Any()) return;
            
            var selectedNotes = SelectedNotes.ToList();
            if (!selectedNotes.Any()) return;

            // 计算选定区域的边界
            var minTime = selectedNotes.Min(n => n.StartTime);
            var maxTime = selectedNotes.Max(n => n.StartTime + n.Duration.ToDouble());
            var minPitch = selectedNotes.Min(n => n.Pitch);
            var maxPitch = selectedNotes.Max(n => n.Pitch);

            // 确保边界在视口中可见
            EnsureTimeVisible(minTime);
            EnsureTimeVisible(maxTime);
            EnsurePitchVisible(minPitch);
            EnsurePitchVisible(maxPitch);
        }
        #endregion
    }
}