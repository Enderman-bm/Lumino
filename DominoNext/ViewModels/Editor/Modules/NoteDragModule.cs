using System;
using System.Linq;
using Avalonia;
using DominoNext.ViewModels.Editor.State;
using DominoNext.Services.Interfaces;
using DominoNext.Models.Music;
using System.Diagnostics;

namespace DominoNext.ViewModels.Editor.Modules
{
    /// <summary>
    /// 音符拖拽模块 - 重置为简洁有效的拖拽系统
    /// </summary>
    public class NoteDragModule
    {
        private readonly DragState _dragState;
        private readonly ICoordinateService _coordinateService;
        private PianoRollViewModel? _pianoRollViewModel;



        public NoteDragModule(DragState dragState, ICoordinateService coordinateService)
        {
            _dragState = dragState;
            _coordinateService = coordinateService;
        }

        public void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        /// <summary>
        /// 开始拖拽操作
        /// </summary>
        public void StartDrag(NoteViewModel note, Point startPosition)
        {
            if (_pianoRollViewModel == null) return;

            _dragState.StartDrag(note, startPosition);
            
            // 获取所有选中的音符进行拖拽
            _dragState.DraggingNotes.Clear();
            foreach (var selectedNote in _pianoRollViewModel.Notes.Where(n => n.IsSelected))
            {
                _dragState.DraggingNotes.Add(selectedNote);
            }

            // 记录拖拽前所有音符的原始位置
            _dragState.OriginalDragPositions.Clear();
            foreach (var dragNote in _dragState.DraggingNotes)
            {
                _dragState.OriginalDragPositions[dragNote] = (dragNote.StartPosition, dragNote.Pitch);
            }

            Debug.WriteLine($"开始拖拽 {_dragState.DraggingNotes.Count} 个音符");
        }

        /// <summary>
        /// 更新拖拽 - 实时更新音符位置
        /// </summary>
        public void UpdateDrag(Point currentPosition)
        {
            if (!_dragState.IsDragging || _pianoRollViewModel == null) return;

            var deltaX = currentPosition.X - _dragState.DragStartPosition.X;
            var deltaY = currentPosition.Y - _dragState.DragStartPosition.Y;



            // 计算时间和音高的精确偏移量
            var timeDeltaInTicks = deltaX / (_pianoRollViewModel.PixelsPerTick * _pianoRollViewModel.Zoom);
            var pitchDelta = -(int)(deltaY / _pianoRollViewModel.KeyHeight);

            // 实时更新所有拖拽中的音符位置
            foreach (var note in _dragState.DraggingNotes)
            {
                if (_dragState.OriginalDragPositions.TryGetValue(note, out var originalPos))
                {
                    var originalTimeInTicks = originalPos.OriginalStartPosition.ToTicks(_pianoRollViewModel.TicksPerBeat);
                    var newTimeInTicks = Math.Max(0, originalTimeInTicks + timeDeltaInTicks);
                    var newPitch = Math.Max(0, Math.Min(127, originalPos.OriginalPitch + pitchDelta));

                    // 网格对齐
                    var quantizedTimeInTicks = _pianoRollViewModel.SnapToGridTime(newTimeInTicks);
                    
                    var newStartPosition = MusicalFraction.FromTicks(quantizedTimeInTicks, _pianoRollViewModel.TicksPerBeat);

                    // 立即更新音符位置
                    note.StartPosition = newStartPosition;
                    note.Pitch = newPitch;
                    note.InvalidateCache();
                }
            }

            // 触发拖拽更新事件
            OnDragUpdated?.Invoke();
            
            // 强制刷新UI，确保拖拽过程流畅无延迟
            _pianoRollViewModel?.RequestRenderRefresh();
        }

        /// <summary>
        /// 结束拖拽
        /// </summary>
        public void EndDrag()
        {
            if (_dragState.IsDragging)
            {
                Debug.WriteLine($"结束拖拽 {_dragState.DraggingNotes.Count} 个音符");
            }

            _dragState.EndDrag();
            OnDragEnded?.Invoke();
        }

        /// <summary>
        /// 取消拖拽并恢复原始位置
        /// </summary>
        public void CancelDrag()
        {
            if (_dragState.IsDragging && _dragState.DraggingNotes.Count > 0)
            {
                foreach (var note in _dragState.DraggingNotes)
                {
                    if (_dragState.OriginalDragPositions.TryGetValue(note, out var originalPos))
                    {
                        note.StartPosition = originalPos.OriginalStartPosition;
                        note.Pitch = originalPos.OriginalPitch;
                        note.InvalidateCache();
                    }
                }
                Debug.WriteLine($"取消拖拽并恢复 {_dragState.DraggingNotes.Count} 个音符到原始位置");
            }

            EndDrag();
        }

        // �¼�
        public event Action? OnDragUpdated;
        public event Action? OnDragEnded;

        // ֻ������
        public bool IsDragging => _dragState.IsDragging;
        public NoteViewModel? DraggingNote => _dragState.DraggingNote;
        public System.Collections.Generic.List<NoteViewModel> DraggingNotes => _dragState.DraggingNotes;
    }
}