using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;
using Lumino.ViewModels.Editor.Modules;
using Lumino.ViewModels.Editor.State;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的事件处理部分
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 事件订阅
        /// <summary>
        /// 订阅所有事件
        /// </summary>
        private void SubscribeToEvents()
        {
            // 订阅模块事件
            SubscribeToModuleEvents();

            // 订阅组件事件
            SubscribeToComponentEvents();
        }

        /// <summary>
        /// 处理事件类型相关属性变化
        /// </summary>
        private void OnEventTypePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CurrentEventType))
            {
                OnPropertyChanged(nameof(CurrentEventTypeText));
                OnPropertyChanged(nameof(CurrentEventValueRange));
                OnPropertyChanged(nameof(CurrentEventDescription));
            }
            else if (e.PropertyName == nameof(CurrentCCNumber))
            {
                OnPropertyChanged(nameof(CurrentEventTypeText));
                OnPropertyChanged(nameof(CurrentEventDescription));
            }
        }
        #endregion

        #region 模块事件处理
        /// <summary>
        /// 订阅模块事件
        /// </summary>
        private void SubscribeToModuleEvents()
        {
            // 拖拽模块事件
            DragModule.OnDragUpdated += InvalidateVisual;
            DragModule.OnDragEnded += InvalidateVisual;

            ResizeModule.OnResizeUpdated += InvalidateVisual;
            ResizeModule.OnResizeEnded += InvalidateVisual;

            CreationModule.OnCreationUpdated += InvalidateVisual;
            CreationModule.OnCreationCompleted += OnNoteCreated;

            // 选择模块事件
            SelectionModule.OnSelectionUpdated += InvalidateVisual;

            // 力度编辑模块事件
            VelocityEditingModule.OnVelocityUpdated += InvalidateVisual;

            // 事件曲线绘制模块事件
            EventCurveDrawingModule.OnCurveUpdated += InvalidateVisual;
            EventCurveDrawingModule.OnCurveCompleted += OnCurveDrawingCompleted;
            EventCurveDrawingModule.OnCurveCancelled += InvalidateVisual;

            // 订阅选择状态变更事件
            SelectionState.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SelectionState.SelectionStart) ||
                    e.PropertyName == nameof(SelectionState.SelectionEnd) ||
                    e.PropertyName == nameof(SelectionState.IsSelecting))
                {
                    OnPropertyChanged(nameof(SelectionStart));
                    OnPropertyChanged(nameof(SelectionEnd));
                    OnPropertyChanged(nameof(IsSelecting));
                    InvalidateVisual();
                }
            };
        }

        /// <summary>
        /// 处理音符创建完成事件
        /// </summary>
        private void OnNoteCreated()
        {
            InvalidateVisual();

            // 同步最新创建音符的时值到UI显示
            if (Notes.Count > 0)
            {
                var lastNote = Notes.Last();
                if (!lastNote.Duration.Equals(Configuration.UserDefinedNoteDuration))
                {
                    OnPropertyChanged(nameof(CurrentNoteTimeValueText));
                }
            }

            UpdateMaxScrollExtent();
        }

        /// <summary>
        /// 处理曲线绘制完成事件
        /// </summary>
        private void OnCurveDrawingCompleted(List<CurvePoint> curvePoints)
        {
            // TODO: 将曲线点转换为MIDI事件并保存到项目中
            System.Diagnostics.Debug.WriteLine($"曲线绘制完成，包含 {curvePoints.Count} 个点，事件类型：{CurrentEventType}");
            
            foreach (var point in curvePoints)
            {
                System.Diagnostics.Debug.WriteLine($"  时间: {point.Time:F1}, 数值: {point.Value}");
            }
            
            InvalidateVisual();
        }
        #endregion

        #region 组件事件处理
        /// <summary>
        /// 订阅组件事件
        /// </summary>
        private void SubscribeToComponentEvents()
        {
            // 配置变更事件
            Configuration.PropertyChanged += OnConfigurationPropertyChanged;

            // 视口变更事件
            Viewport.PropertyChanged += OnViewportPropertyChanged;
            
            // 缩放管理器变更事件
            ZoomManager.PropertyChanged += OnZoomManagerPropertyChanged;

            // 命令组件事件
            Commands.SelectAllRequested += () => SelectionModule.SelectAll(CurrentTrackNotes);
            Commands.ConfigurationChanged += InvalidateVisual;
            Commands.ViewportChanged += InvalidateVisual;
        }

        /// <summary>
        /// 处理配置属性变化
        /// </summary>
        private void OnConfigurationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将配置变更传播到主ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(Configuration.IsEventViewVisible):
                    OnPropertyChanged(nameof(IsEventViewVisible));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
                    break;
                case nameof(Configuration.CurrentTool):
                    OnPropertyChanged(nameof(CurrentTool));
                    break;
                case nameof(Configuration.GridQuantization):
                    OnPropertyChanged(nameof(GridQuantization));
                    OnPropertyChanged(nameof(CurrentNoteDurationText));
                    break;
                case nameof(Configuration.UserDefinedNoteDuration):
                    OnPropertyChanged(nameof(UserDefinedNoteDuration));
                    OnPropertyChanged(nameof(CurrentNoteTimeValueText));
                    break;
                case nameof(Configuration.IsNoteDurationDropDownOpen):
                    OnPropertyChanged(nameof(IsNoteDurationDropDownOpen));
                    break;
                case nameof(Configuration.CustomFractionInput):
                    OnPropertyChanged(nameof(CustomFractionInput));
                    break;
            }

            InvalidateVisual();
        }

        /// <summary>
        /// 处理缩放管理器属性变化
        /// </summary>
        private void OnZoomManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将缩放管理器变更传播到主ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(ZoomManager.Zoom):
                    OnPropertyChanged(nameof(Zoom));
                    OnPropertyChanged(nameof(BaseQuarterNoteWidth));
                    OnPropertyChanged(nameof(TimeToPixelScale));
                    OnPropertyChanged(nameof(MeasureWidth));
                    OnPropertyChanged(nameof(BeatWidth));
                    OnPropertyChanged(nameof(EighthNoteWidth));
                    OnPropertyChanged(nameof(SixteenthNoteWidth));
                    // 缩放变化时必须更新滚动范围
                    UpdateMaxScrollExtent();
                    // 同时更新最大滚动范围的属性通知
                    OnPropertyChanged(nameof(MaxScrollExtent));
                    // 更新歌曲长度相关属性
                    OnPropertyChanged(nameof(EffectiveSongLength));
                    OnPropertyChanged(nameof(ScrollbarTotalLength));
                    OnPropertyChanged(nameof(CurrentViewportRatio));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    InvalidateNoteCache();
                    break;
                case nameof(ZoomManager.VerticalZoom):
                    OnPropertyChanged(nameof(VerticalZoom));
                    OnPropertyChanged(nameof(KeyHeight));
                    OnPropertyChanged(nameof(TotalHeight));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
                    InvalidateNoteCache();
                    break;
                case nameof(ZoomManager.ZoomSliderValue):
                    OnPropertyChanged(nameof(ZoomSliderValue));
                    // 滑块值变化也要更新滚动范围
                    UpdateMaxScrollExtent();
                    OnPropertyChanged(nameof(MaxScrollExtent));
                    // 更新歌曲长度相关属性
                    OnPropertyChanged(nameof(EffectiveSongLength));
                    OnPropertyChanged(nameof(ScrollbarTotalLength));
                    OnPropertyChanged(nameof(CurrentViewportRatio));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    break;
                case nameof(ZoomManager.VerticalZoomSliderValue):
                    OnPropertyChanged(nameof(VerticalZoomSliderValue));
                    break;
            }

            InvalidateVisual();
        }

        /// <summary>
        /// 处理视口属性变化
        /// </summary>
        private void OnViewportPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 将视口变更传播到主ViewModel的属性通知
            switch (e.PropertyName)
            {
                case nameof(Viewport.CurrentScrollOffset):
                    OnPropertyChanged(nameof(CurrentScrollOffset));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    break;
                case nameof(Viewport.VerticalScrollOffset):
                    OnPropertyChanged(nameof(VerticalScrollOffset));
                    break;
                case nameof(Viewport.ViewportWidth):
                case nameof(Viewport.ViewportHeight):
                    OnPropertyChanged(nameof(ViewportWidth));
                    OnPropertyChanged(nameof(ViewportHeight));
                    OnPropertyChanged(nameof(EffectiveScrollableHeight));
                    OnPropertyChanged(nameof(ActualRenderHeight));
                    // 视口大小变化影响比例计算
                    OnPropertyChanged(nameof(CurrentViewportRatio));
                    OnPropertyChanged(nameof(CurrentScrollPositionRatio));
                    break;
            }

            InvalidateVisual();
        }

        /// <summary>
        /// 触发UI更新
        /// </summary>
        private void InvalidateVisual()
        {
            // 触发UI更新的方法，由View层实现
        }

        /// <summary>
        /// 使音符缓存失效
        /// </summary>
        private void InvalidateNoteCache()
        {
            foreach (var note in Notes)
            {
                note.InvalidateCache();
            }
        }
        #endregion

        #region 音符集合变化处理
        /// <summary>
        /// 处理Notes集合变化，自动更新滚动范围
        /// </summary>
        private void OnNotesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

            // 如果是添加音符且接近当前可见区域的末尾，考虑自动滚动
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (NoteViewModel newNote in e.NewItems)
                {
                    CheckAutoScrollForNewNote(newNote);
                }
            }
        }

        /// <summary>
        /// 处理当前音轨索引变化
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
                
                // ScrollBarManager已废弃，使用Viewport替代
                // EnsureScrollBarManagerConnection();
            }
        }

        /// <summary>
        /// 更新当前音轨的音符集合
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
        /// 设置当前音轨索引
        /// </summary>
        public void SetCurrentTrackIndex(int trackIndex)
        {
            if (CurrentTrackIndex != trackIndex)
            {
                CurrentTrackIndex = trackIndex;
                
                // ScrollBarManager已废弃，使用Viewport替代
                // EnsureScrollBarManagerConnection();
            }
        }

        /// <summary>
        /// 检查新添加的音符是否需要自动滚动
        /// </summary>
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

        /// <summary>
        /// 设置当前音轨的ViewModel
        /// </summary>
        public void SetCurrentTrack(TrackViewModel? track)
        {
            if (CurrentTrack != track)
            {
                CurrentTrack = track;
                OnPropertyChanged(nameof(IsCurrentTrackConductor));
                
                // 根据轨道类型自动设置事件类型
                if (IsCurrentTrackConductor && CurrentEventType != EventType.Tempo)
                {
                    CurrentEventType = EventType.Tempo;
                }
                else if (!IsCurrentTrackConductor && CurrentEventType == EventType.Tempo)
                {
                    CurrentEventType = EventType.Velocity;
                }
            }
        }
        #endregion
    }
}