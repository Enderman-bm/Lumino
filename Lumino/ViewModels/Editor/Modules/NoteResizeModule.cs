using System;
using System.Linq;
using Avalonia;
using Lumino.ViewModels.Editor.State;
using Lumino.Services.Interfaces;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Modules.Base;
using Lumino.ViewModels.Editor.Services;
using System.Diagnostics;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符调整大小功能模块 - 基于分数的新实现
    /// 重构后使用基类和通用服务，减少重复代码
    /// </summary>
    public class NoteResizeModule : EditorModuleBase
    {
        private readonly ResizeState _resizeState;

        public override string ModuleName => "NoteResize";

        // 拖拽边缘检测阈值
        private const double ResizeEdgeThreshold = 8.0;

        public NoteResizeModule(ResizeState resizeState, ICoordinateService coordinateService) 
            : base(coordinateService)
        {
            _resizeState = resizeState;
        }

        /// <summary>
        /// 检查位置是否接近音符的边缘
        /// </summary>
        public ResizeHandle GetResizeHandleAtPosition(Point position, NoteViewModel note)
        {
            if (_pianoRollViewModel?.CurrentTool != EditorTool.Pencil) return ResizeHandle.None;

            // 使用支持滚动偏移量的坐标转换方法
            var noteRect = _coordinateService.GetNoteRect(note, 
                _pianoRollViewModel.TimeToPixelScale, 
                _pianoRollViewModel.KeyHeight,
                _pianoRollViewModel.CurrentScrollOffset,
                _pianoRollViewModel.VerticalScrollOffset);

            if (!noteRect.Contains(position)) return ResizeHandle.None;

            // 检查是否接近开始边缘
            if (Math.Abs(position.X - noteRect.Left) <= ResizeEdgeThreshold)
            {
                return ResizeHandle.StartEdge;
            }

            // 检查是否接近结束边缘
            if (Math.Abs(position.X - noteRect.Right) <= ResizeEdgeThreshold)
            {
                return ResizeHandle.EndEdge;
            }

            return ResizeHandle.None;
        }

        /// <summary>
        /// 开始调整大小
        /// </summary>
        public void StartResize(Point position, NoteViewModel note, ResizeHandle handle)
        {
            if (handle == ResizeHandle.None || _pianoRollViewModel == null) return;

            _resizeState.StartResize(note, handle);

            // 获取所有选中的音符，包含当前音符
            _resizeState.ResizingNotes = _pianoRollViewModel.Notes.Where(n => n.IsSelected).ToList();
            if (!_resizeState.ResizingNotes.Contains(note))
            {
                _resizeState.ResizingNotes.Add(note);
            }

            // 记录原始音长和位置
            _resizeState.OriginalDurations.Clear();
            foreach (var n in _resizeState.ResizingNotes)
            {
                _resizeState.OriginalDurations[n] = n.Duration;
                n.PropertyChanged += OnResizingNotePropertyChanged;
            }

            Debug.WriteLine($"开始调整音符大小: Handle={handle}, 选中音符数={_resizeState.ResizingNotes.Count}");
            OnResizeStarted?.Invoke();
        }

        /// <summary>
        /// 更新调整大小 - 使用基类的通用方法
        /// </summary>
        public void UpdateResize(Point currentPosition)
        {
            if (!_resizeState.IsResizing || _resizeState.ResizingNote == null || 
                _resizeState.ResizingNotes.Count == 0 || _pianoRollViewModel == null) return;

            try
            {
                // 使用基类的通用坐标转换方法
                var currentTimeValue = GetTimeFromPosition(currentPosition);
                bool anyNoteChanged = false;

                foreach (var note in _resizeState.ResizingNotes)
                {
                    var startValue = note.StartPosition.ToDouble();
                    var endValue = startValue + note.Duration.ToDouble();
                    var originalDuration = _resizeState.OriginalDurations[note];

                    MusicalFraction newDuration;
                    MusicalFraction newStartPosition = note.StartPosition;

                    if (_resizeState.CurrentResizeHandle == ResizeHandle.StartEdge)
                    {
                        // 调整开始位置
                        var newStartValue = Math.Min(currentTimeValue, endValue - _pianoRollViewModel.GridQuantization.ToDouble());
                        var newStartFraction = MusicalFraction.FromDouble(newStartValue);
                        var quantizedStart = _pianoRollViewModel.SnapToGrid(newStartFraction);

                        var endFraction = MusicalFraction.FromDouble(endValue);
                        newDuration = endFraction - quantizedStart;
                        newStartPosition = quantizedStart;
                    }
                    else // EndEdge
                    {
                        // 调整结束位置
                        var newEndValue = Math.Max(currentTimeValue, startValue + _pianoRollViewModel.GridQuantization.ToDouble());
                        var newEndFraction = MusicalFraction.FromDouble(newEndValue);
                        var quantizedEnd = _pianoRollViewModel.SnapToGrid(newEndFraction);

                        var startFraction = note.StartPosition;
                        newDuration = quantizedEnd - startFraction;
                    }

                    // 应用最小长度约束 - 使用验证服务
                    var minDuration = _pianoRollViewModel.GridQuantization;
                    if (!EditorValidationService.IsValidDuration(originalDuration, minDuration))
                    {
                        newDuration = originalDuration;
                    }
                    else
                    {
                        if (!EditorValidationService.IsValidDuration(newDuration, minDuration))
                        {
                            newDuration = minDuration;
                        }
                    }

                    // 只在长度或位置发生改变时更新
                    bool durationChanged = !note.Duration.Equals(newDuration);
                    bool positionChanged = _resizeState.CurrentResizeHandle == ResizeHandle.StartEdge && !note.StartPosition.Equals(newStartPosition);

                    if (durationChanged || positionChanged)
                    {
                        if (positionChanged) note.StartPosition = newStartPosition;
                        if (durationChanged) note.Duration = newDuration;

                        SafeInvalidateNoteCache(note);
                        anyNoteChanged = true;
                    }
                }

                if (anyNoteChanged)
                {
                    OnResizeUpdated?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"调整音符大小时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 完成调整大小
        /// </summary>
        public void EndResize()
        {
            if (_resizeState.IsResizing && _resizeState.ResizingNote != null && _pianoRollViewModel != null)
            {
                // 更新用户自定义时值 - 通过Configuration设置
                _pianoRollViewModel.Configuration.UserDefinedNoteDuration = _resizeState.ResizingNote.Duration;
                Debug.WriteLine($"完成调整大小，更新用户自定义时值: {_pianoRollViewModel.Configuration.UserDefinedNoteDuration}");
                
                // 调整大小结束后重新计算滚动范围，因为音符的长度或位置可能已经改变
                _pianoRollViewModel.UpdateMaxScrollExtent();
            }

            // 取消属性变化监听
            foreach (var note in _resizeState.ResizingNotes)
            {
                note.PropertyChanged -= OnResizingNotePropertyChanged;
            }

            _resizeState.EndResize();
            OnResizeEnded?.Invoke();
        }

        /// <summary>
        /// 取消调整大小
        /// </summary>
        public void CancelResize()
        {
            if (_resizeState.IsResizing && _resizeState.ResizingNotes.Count > 0)
            {
                // 恢复原始长度
                foreach (var note in _resizeState.ResizingNotes)
                {
                    if (_resizeState.OriginalDurations.TryGetValue(note, out var originalDuration))
                    {
                        note.Duration = originalDuration;
                        SafeInvalidateNoteCache(note);
                    }
                    note.PropertyChanged -= OnResizingNotePropertyChanged;
                }

                Debug.WriteLine($"取消调整音符长度，恢复 {_resizeState.ResizingNotes.Count} 个音符的原始长度");
            }

            _resizeState.EndResize();
            OnResizeEnded?.Invoke();
        }

        private void OnResizingNotePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NoteViewModel.Duration) || e.PropertyName == nameof(NoteViewModel.StartPosition))
            {
                OnResizeUpdated?.Invoke();
            }
        }

        // 事件
        public event Action? OnResizeStarted;
        public event Action? OnResizeUpdated;
        public event Action? OnResizeEnded;

        // 只读属性
        public bool IsResizing => _resizeState.IsResizing;
        public ResizeHandle CurrentResizeHandle => _resizeState.CurrentResizeHandle;
        public NoteViewModel? ResizingNote => _resizeState.ResizingNote;
        public System.Collections.Generic.List<NoteViewModel> ResizingNotes => _resizeState.ResizingNotes;
    }
}

