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
                UpdateCurrentTrackControllerEvents();

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
            }
        }

        /// <summary>
        /// 更新当前音轨的音符集合
        /// 根据CurrentTrackIndex过滤出当前轨道的音符
        /// </summary>
        private void UpdateCurrentTrackNotes()
        {
            CurrentTrackNotes.Clear();

            var currentTrackNotes = Notes.Where(note => note.TrackIndex == CurrentTrackIndex);
            foreach (var note in currentTrackNotes)
            {
                CurrentTrackNotes.Add(note);
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
        /// 批量添加音符，按音轨批量添加以提高效率
        /// </summary>
        /// <param name="noteViewModels">要添加的音符ViewModel集合</param>
        public async System.Threading.Tasks.Task AddNotesInBatchAsync(IEnumerable<NoteViewModel> noteViewModels)
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
                // 按音轨分组
                var notesByTrack = noteViewModels
                    .GroupBy(n => n.TrackIndex)
                    .OrderBy(g => g.Key)
                    .ToList();

                int totalNotes = 0;
                int processedTracks = 0;

                foreach (var trackGroup in notesByTrack)
                {
                    var trackNotes = trackGroup.ToList();
                    
                    // 在UI线程一次性添加整个音轨的所有音符
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var noteViewModel in trackNotes)
                        {
                            Notes.Add(noteViewModel);
                        }
                    });

                    totalNotes += trackNotes.Count;
                    processedTracks++;

                    // 每个音轨添加完后让UI有机会刷新
                    await System.Threading.Tasks.Task.Yield();
                }

                _logger.Debug("PianoRollViewModel", $"批量添加了 {totalNotes} 个音符，分布在 {processedTracks} 个音轨");
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
        /// 从临时缓存流式添加音符（按音轨批量添加，提高效率）
        /// </summary>
        /// <param name="tempCacheService">临时缓存服务</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async System.Threading.Tasks.Task AddNotesFromCacheAsync(
            Lumino.Services.Interfaces.ITempProjectCacheService tempCacheService,
            System.Threading.CancellationToken cancellationToken = default)
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
                // 先读取所有音符并按音轨分组
                var notesByTrack = new Dictionary<int, List<NoteViewModel>>();
                long totalCount = 0;

                // 使用较大的批次读取以减少IO操作
                const int readBatchSize = 4096;
                
                await foreach (var noteBatch in tempCacheService.ReadNotesInBatchesAsync(readBatchSize, cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    foreach (var note in noteBatch)
                    {
                        var viewModel = new NoteViewModel
                        {
                            Pitch = note.Pitch,
                            StartPosition = note.StartPosition,
                            Duration = note.Duration,
                            Velocity = note.Velocity,
                            TrackIndex = note.TrackIndex,
                            MidiChannel = note.MidiChannel
                        };

                        if (!notesByTrack.TryGetValue(note.TrackIndex, out var trackNotes))
                        {
                            trackNotes = new List<NoteViewModel>();
                            notesByTrack[note.TrackIndex] = trackNotes;
                        }
                        trackNotes.Add(viewModel);
                        totalCount++;
                    }
                }

                _logger.Debug("PianoRollViewModel", $"从缓存读取了 {totalCount} 个音符，分布在 {notesByTrack.Count} 个音轨");

                // 按音轨批量添加到UI
                int processedTracks = 0;
                foreach (var kvp in notesByTrack.OrderBy(x => x.Key))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var trackIndex = kvp.Key;
                    var trackNotes = kvp.Value;

                    // 在UI线程一次性添加整个音轨的所有音符
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var noteViewModel in trackNotes)
                        {
                            Notes.Add(noteViewModel);
                        }
                    });

                    processedTracks++;
                    _logger.Debug("PianoRollViewModel", $"已添加音轨 {trackIndex} 的 {trackNotes.Count} 个音符 ({processedTracks}/{notesByTrack.Count})");

                    // 每个音轨添加完后让UI有机会刷新
                    await System.Threading.Tasks.Task.Yield();
                }

                _logger.Debug("PianoRollViewModel", $"从缓存添加了 {totalCount} 个音符");
            }
            finally
            {
                EndBatchOperation();
            }
        }
        #endregion
    }
}