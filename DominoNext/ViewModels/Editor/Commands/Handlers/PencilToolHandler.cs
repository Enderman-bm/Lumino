using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using DominoNext.ViewModels.Editor.State;
using DominoNext.Views.Controls.Editing;
using DominoNext.Models.Music;

namespace DominoNext.ViewModels.Editor.Commands
{
    /// <summary>
    /// FL Studio风格的画笔工具处理器
    /// </summary>
    public class PencilToolHandler
    {
        private PianoRollViewModel? _pianoRollViewModel;
        private bool _isDrawingMode = false;
        private Point _lastDrawPosition;
        private NoteViewModel? _lastCreatedNote;

        public void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        /// <summary>
        /// 处理鼠标按下事件 - FL Studio风格
        /// </summary>
        public void HandlePress(Point position, NoteViewModel? clickedNote, KeyModifiers modifiers)
        {
            if (_pianoRollViewModel == null) return;

            Debug.WriteLine($"=== FL PencilTool: HandlePress ===");
            Debug.WriteLine($"Position: {position}, ClickedNote: {clickedNote?.Pitch ?? -1}");

            if (clickedNote == null)
            {
                // FL Studio风格：单击直接放置音符
                CreateNoteAtPosition(position);
                _isDrawingMode = true;
                _lastDrawPosition = position;
            }
            else
            {
                HandleExistingNoteClick(position, clickedNote, modifiers);
            }
        }

        /// <summary>
        /// 处理鼠标移动事件 - 用于连续绘制
        /// </summary>
        public void HandleMove(Point position, MouseButtons buttons)
        {
            if (!_isDrawingMode || _pianoRollViewModel == null) return;

            // 检查是否需要在新位置创建音符
            var currentPitch = _pianoRollViewModel.GetPitchFromY(position.Y);
            var lastPitch = _pianoRollViewModel.GetPitchFromY(_lastDrawPosition.Y);
            
            var currentTime = _pianoRollViewModel.GetTimeFromX(position.X);
            var lastTime = _pianoRollViewModel.GetTimeFromX(_lastDrawPosition.X);

            // 计算网格对齐的位置
            var snappedTime = _pianoRollViewModel.SnapToGridTime(currentTime);
            var snappedPitch = _pianoRollViewModel.SnapToGridPitch(currentPitch);

            // 如果移动到了新的网格位置，创建新音符
            if (Math.Abs(snappedTime - _pianoRollViewModel.GetTimeFromX(_lastDrawPosition.X)) >= _pianoRollViewModel.GridQuantization.ToTicks(_pianoRollViewModel.TicksPerBeat) ||
                snappedPitch != _pianoRollViewModel.GetPitchFromY(_lastDrawPosition.Y))
            {
                CreateNoteAtGridPosition(snappedPitch, snappedTime);
                _lastDrawPosition = position;
            }
        }

        /// <summary>
        /// 处理鼠标释放事件
        /// </summary>
        public void HandleRelease()
        {
            _isDrawingMode = false;
            _lastCreatedNote = null;
            Debug.WriteLine("=== FL PencilTool: HandleRelease ===");
        }

        /// <summary>
        /// 在指定位置创建音符 - FL Studio风格
        /// </summary>
        private void CreateNoteAtPosition(Point position)
        {
            if (_pianoRollViewModel == null) return;

            try
            {
                var pitch = _pianoRollViewModel.GetPitchFromY(position.Y);
                var startTime = _pianoRollViewModel.GetTimeFromX(position.X);

                // 网格对齐
                var snappedPitch = _pianoRollViewModel.SnapToGridPitch(pitch);
                var snappedTime = _pianoRollViewModel.SnapToGridTime(startTime);

                CreateNoteAtGridPosition(snappedPitch, snappedTime);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR creating note: {ex.Message}");
            }
        }

        /// <summary>
        /// 在网格位置创建音符
        /// </summary>
        private void CreateNoteAtGridPosition(int pitch, double startTime)
        {
            if (_pianoRollViewModel == null) return;

            // 检查位置是否有效
            if (pitch < 0 || pitch > 127 || startTime < 0) return;

            // 检查该位置是否已有音符
            var existingNote = _pianoRollViewModel.Notes.FirstOrDefault(n => 
                n.Pitch == pitch && 
                Math.Abs(n.StartPosition.ToTicks(_pianoRollViewModel.TicksPerBeat) - startTime) < _pianoRollViewModel.GridQuantization.ToTicks(_pianoRollViewModel.TicksPerBeat));

            if (existingNote != null) return; // 已有音符，不重复创建

            var quantizedPosition = MusicalFraction.FromTicks(startTime, _pianoRollViewModel.TicksPerBeat);

            var newNote = new NoteViewModel
            {
                Pitch = pitch,
                StartPosition = quantizedPosition,
                Duration = _pianoRollViewModel.UserDefinedNoteDuration,
                Velocity = 100,
                IsSelected = false
            };

            _pianoRollViewModel.Notes.Add(newNote);
            _pianoRollViewModel.SubscribeToNoteEvents(newNote);
            _lastCreatedNote = newNote;

            Debug.WriteLine($"FL PencilTool: Created note Pitch={pitch}, Start={quantizedPosition}");
        }

        /// <summary>
        /// 处理点击已存在音符的逻辑
        /// </summary>
        private void HandleExistingNoteClick(Point position, NoteViewModel clickedNote, KeyModifiers modifiers)
        {
            if (_pianoRollViewModel == null) return;

            // 检查是否点击在音符边缘进行大小调整
            var resizeHandle = _pianoRollViewModel.ResizeModule.GetResizeHandleAtPosition(position, clickedNote);

            if (resizeHandle != ResizeHandle.None)
            {
                // 处理选择逻辑
                HandleSelectionLogic(clickedNote, modifiers);
                _pianoRollViewModel.ResizeModule.StartResize(position, clickedNote, resizeHandle);
            }
            else
            {
                // 处理拖拽逻辑
                HandleSelectionLogic(clickedNote, modifiers);
                
                if (clickedNote.IsSelected)
                {
                    _pianoRollViewModel.DragModule.StartDrag(clickedNote, position);
                }
            }
        }

        /// <summary>
        /// 处理选择逻辑
        /// </summary>
        private void HandleSelectionLogic(NoteViewModel clickedNote, KeyModifiers modifiers)
        {
            if (_pianoRollViewModel == null) return;

            if (modifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl+点击：切换选择状态
                clickedNote.IsSelected = !clickedNote.IsSelected;
            }
            else
            {
                // 普通点击：清除其他选择，只选择当前音符
                if (!clickedNote.IsSelected)
                {
                    _pianoRollViewModel.SelectionModule.ClearSelection(_pianoRollViewModel.Notes);
                }
                clickedNote.IsSelected = true;
            }
        }
    }
}