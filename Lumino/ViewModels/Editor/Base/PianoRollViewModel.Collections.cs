using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor.Commands;
using Lumino.ViewModels.Editor.Modules;
using Lumino.ViewModels.Editor.State;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;
using EnderDebugger;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel集合管理
    /// 处理Notes集合的变化、当前音轨音符过滤和自动滚动逻辑
    /// </summary>
    public partial class PianoRollViewModel : ViewModelBase
    {
        #region Notes集合变化处理
        /// <summary>
        /// 处理Notes集合变化，自动更新滚动范围
        /// 当音符集合发生变化时，更新滚动范围并刷新当前音轨音符集合
        /// </summary>
        private void OnNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 批量操作期间跳过频繁的UI更新
            if (_isBatchOperationInProgress)
                return;

            // 🎯 关键修复：当添加新音符时，清理相关轨道的预加载数据，避免使用过期的缓存
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (NoteViewModel newNote in e.NewItems)
                {
                    _trackPreloader.ClearPreloadedTrack(newNote.TrackIndex);
                    _logger.Debug("OnNotesCollectionChanged", $"清理轨道 {newNote.TrackIndex} 的预加载数据，因为添加了新音符");
                }
            }

            // 音符集合发生变化时，自动更新滚动范围以支持自动延长小节功能
            UpdateMaxScrollExtent();

            // 更新当前音轨的音符集合
            UpdateCurrentTrackNotes();

            // 更新歌曲长度相关属性
            OnPropertyChanged(nameof(EffectiveSongLength));
            OnPropertyChanged(nameof(ScrollbarTotalLength));
            OnPropertyChanged(nameof(CurrentViewportRatio));
            OnPropertyChanged(nameof(CurrentScrollPositionRatio));

            // 触发UI更新
            InvalidateVisual();

            // 如果是添加音符且接近当前可 visible区域的末尾，考虑自动滚动
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (NoteViewModel newNote in e.NewItems)
                {
                    CheckAutoScrollForNewNote(newNote);
                }
            }
        }

        /// <summary>
        /// 处理当前音轨索引变化
        /// 当音轨索引变化时，更新当前音轨音符集合并根据轨道类型切换事件类型
        /// </summary>
        private void OnCurrentTrackIndexChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CurrentTrackIndex))
            {
                UpdateCurrentTrackNotes();

                // 如果切换到Conductor轨道，自动切换到Tempo事件类型
                if (IsCurrentTrackConductor && CurrentEventType != EventType.Tempo)
                {
                    CurrentEventType = EventType.Tempo;
                }
                // 如果从Conductor轨道切换到普通轨道，切换到Velocity事件类型
                else if (!IsCurrentTrackConductor && CurrentEventType == EventType.Tempo)
                {
                    CurrentEventType = EventType.Velocity;
                }

                OnPropertyChanged(nameof(IsCurrentTrackConductor));

                // 确保在切换音轨后滚动条连接正常
                EnsureScrollBarManagerConnection();

                // 预加载相邻音轨，提升用户体验
                PreloadAdjacentTracks();
            }
        }

        /// <summary>
        /// 更新当前音轨的音符集合
        /// 根据CurrentTrackIndex过滤出当前轨道的音符
        /// </summary>
        private void UpdateCurrentTrackNotes()
        {
            // ✅ 优化: 临时标记,批量更新时避免触发多次索引重建
            IsTrackLoading = true;

            try
            {
                CurrentTrackNotes.Clear();

                // 优先使用预加载的数据
                var preloadedNotes = _trackPreloader.GetPreloadedTrackNotes(CurrentTrackIndex);
                if (preloadedNotes != null)
                {
                    // ✅ 批量添加,减少CollectionChanged事件触发
                    foreach (var note in preloadedNotes)
                    {
                        CurrentTrackNotes.Add(note);
                    }
                    _logger.Info("UpdateCurrentTrackNotes", $"使用预加载数据更新音轨 {CurrentTrackIndex}, 音符数量: {preloadedNotes.Count}");
                }
                else
                {
                    // 回退到同步加载
                    var allNotesCount = Notes.Count;
                    var currentTrackNotes = Notes.Where(note => note.TrackIndex == CurrentTrackIndex).ToList();
                    
                    // ✅ 批量添加,一次性完成
                    foreach (var note in currentTrackNotes)
                    {
                        CurrentTrackNotes.Add(note);
                    }
                    
                    _logger.Info("UpdateCurrentTrackNotes", $"同步加载音轨 {CurrentTrackIndex}, 总音符数量: {allNotesCount}, 当前轨道音符数量: {currentTrackNotes.Count}, CurrentTrackNotes数量: {CurrentTrackNotes.Count}");
                    
                    // 调试：列出所有音符的TrackIndex
                    var allTrackIndices = Notes.Select(n => n.TrackIndex).Distinct().OrderBy(i => i).ToList();
                    _logger.Debug("UpdateCurrentTrackNotes", $"所有存在的轨道索引: {string.Join(", ", allTrackIndices)}");

                    // 触发异步预加载，为下次切换做准备
                    _ = _trackPreloader.PreloadTrackAsync(CurrentTrackIndex);
                }
            }
            finally
            {
                // ✅ 批量更新完成,恢复正常状态
                IsTrackLoading = false;
            }
        }

        /// <summary>
        /// 检查新添加的音符是否需要自动滚动
        /// 如果音符超出当前可见区域，则自动滚动以保持音符可见
        /// </summary>
        /// <param name="note">新添加的音符</param>
        private void CheckAutoScrollForNewNote(NoteViewModel note)
        {
            // 计算音符结束位置的像素坐标
            var noteEndTime = note.StartPosition + note.Duration;
            var noteEndPixels = noteEndTime.ToDouble() * BaseQuarterNoteWidth;

            // 获取当前可见区域的右边界
            var visibleEndPixels = CurrentScrollOffset + ViewportWidth;

            // 如果音符超出当前可见区域右边界，且距离不太远，则自动滚动
            var scrollThreshold = ViewportWidth * 0.1; // 10%的视口宽度作为阈值
            if (noteEndPixels > visibleEndPixels && (noteEndPixels - visibleEndPixels) <= scrollThreshold)
            {
                // 计算需要滚动的距离，让音符完全可见
                var targetScrollOffset = noteEndPixels - ViewportWidth * 0.8; // 留20%边距
                targetScrollOffset = Math.Max(0, Math.Min(targetScrollOffset, MaxScrollExtent - ViewportWidth));

                // 平滑滚动到目标位置
                Viewport.SetHorizontalScrollOffset(targetScrollOffset);
            }
        }
        #endregion

        #region 批量操作优化
        /// <summary>
        /// 批量操作标志 - 用于暂停集合变更通知以提升性能
        /// </summary>
        private bool _isBatchOperationInProgress = false;

        /// <summary>
        /// 开始批量操作，暂停集合变更通知以提升性能
        /// </summary>
        public void BeginBatchOperation()
        {
            _isBatchOperationInProgress = true;
        }

        /// <summary>
        /// 结束批量操作，恢复集合变更通知并手动触发更新
        /// </summary>
        public void EndBatchOperation()
        {
            _isBatchOperationInProgress = false;

            // 批量操作结束后，手动触发一次更新
            UpdateMaxScrollExtent();
            UpdateCurrentTrackNotes();
            InvalidateVisual();
        }

        /// <summary>
        /// 批量添加音符，避免频繁的UI更新
        /// </summary>
        /// <param name="noteViewModels">要添加的音符ViewModel集合</param>
        public void AddNotesInBatch(IEnumerable<NoteViewModel> noteViewModels)
        {
            // 检查当前轨道是否为Conductor轨，如果是则禁止创建音符
            if (IsCurrentTrackConductor)
            {
                _logger.Debug("PianoRollViewModel", "禁止在Conductor轨上创建音符");
                return;
            }

            BeginBatchOperation();

            try
            {
                // 由于Notes是ObservableCollection，不是线程安全的，需要在UI线程中添加
                Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                {
                    foreach (var noteViewModel in noteViewModels)
                    {
                        Notes.Add(noteViewModel);
                    }
                });
            }
            finally
            {
                EndBatchOperation();
            }
        }

        /// <summary>
        /// 获取所有音符
        /// </summary>
        /// <returns>所有音符的集合</returns>
        public IEnumerable<NoteViewModel> GetAllNotes()
        {
            return Notes;
        }

        /// <summary>
        /// 预加载相邻音轨，提升切换性能
        /// </summary>
        private void PreloadAdjacentTracks()
        {
            // 获取所有可用的音轨索引
            var availableTrackIndices = Notes
                .Select(note => note.TrackIndex)
                .Distinct()
                .OrderBy(index => index)
                .ToList();

            if (availableTrackIndices.Count == 0) return;

            // 预加载当前音轨的前一个和后一个音轨
            var currentIndex = availableTrackIndices.IndexOf(CurrentTrackIndex);
            if (currentIndex >= 0)
            {
                // 预加载前一个音轨
                if (currentIndex > 0)
                {
                    var prevTrackIndex = availableTrackIndices[currentIndex - 1];
                    if (!_trackPreloader.IsTrackPreloaded(prevTrackIndex))
                    {
                        _ = _trackPreloader.PreloadTrackAsync(prevTrackIndex);
                    }
                }

                // 预加载后一个音轨
                if (currentIndex < availableTrackIndices.Count - 1)
                {
                    var nextTrackIndex = availableTrackIndices[currentIndex + 1];
                    if (!_trackPreloader.IsTrackPreloaded(nextTrackIndex))
                    {
                        _ = _trackPreloader.PreloadTrackAsync(nextTrackIndex);
                    }
                }
            }
        }
        #endregion
    }
}