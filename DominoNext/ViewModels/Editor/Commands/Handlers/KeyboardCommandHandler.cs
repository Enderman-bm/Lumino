using Avalonia.Input;
using DominoNext.Models.Music;
using DominoNext.Views.Controls.Editing;

namespace DominoNext.ViewModels.Editor.Commands
{
    /// <summary>
    /// 键盘命令处理器 - 基于分数的新实现
    /// </summary>
    public class KeyboardCommandHandler
    {
        private PianoRollViewModel? _pianoRollViewModel;

        public void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        public void HandleKey(KeyCommandArgs args)
        {
            if (_pianoRollViewModel == null) return;

            switch (args.Key)
            {
                case Key.Delete:
                    DeleteSelectedNotes();
                    break;

                case Key.A when args.Modifiers.HasFlag(KeyModifiers.Control):
                    _pianoRollViewModel.SelectionModule.SelectAll(_pianoRollViewModel.Notes);
                    break;

                case Key.D when args.Modifiers.HasFlag(KeyModifiers.Control):
                    DuplicateSelectedNotes();
                    break;

                case Key.Q:
                    QuantizeSelectedNotes();
                    break;

                // 工具快捷键
                case Key.D1:
                case Key.P:
                    _pianoRollViewModel.CurrentTool = EditorTool.Pencil;
                    break;

                case Key.D2:
                case Key.S:
                    _pianoRollViewModel.CurrentTool = EditorTool.Select;
                    break;

                case Key.D3:
                case Key.E:
                    _pianoRollViewModel.CurrentTool = EditorTool.Eraser;
                    break;

                case Key.D4:
                case Key.C:
                    _pianoRollViewModel.CurrentTool = EditorTool.Cut;
                    break;

                // ESC取消操作
                case Key.Escape:
                    if (_pianoRollViewModel.CreationModule.IsCreatingNote)
                    {
                        _pianoRollViewModel.CreationModule.CancelCreating();
                    }
                    else if (_pianoRollViewModel.ResizeState.IsResizing)
                    {
                        _pianoRollViewModel.ResizeModule.CancelResize();
                    }
                    break;
            }
        }

        private void DeleteSelectedNotes()
        {
            if (_pianoRollViewModel == null) return;

            for (int i = _pianoRollViewModel.Notes.Count - 1; i >= 0; i--)
            {
                if (_pianoRollViewModel.Notes[i].IsSelected)
                {
                    _pianoRollViewModel.Notes.RemoveAt(i);
                }
            }
            
            // 删除音符后重新计算滚动范围，支持自动缩短小节功能
            _pianoRollViewModel.UpdateMaxScrollExtent();
        }

        private void DuplicateSelectedNotes()
        {
            if (_pianoRollViewModel == null) return;

            var selectedNotes = new System.Collections.Generic.List<NoteViewModel>();
            foreach (var note in _pianoRollViewModel.Notes)
            {
                if (note.IsSelected)
                {
                    selectedNotes.Add(note);
                }
            }

            foreach (var note in selectedNotes)
            {
                var newNote = new NoteViewModel
                {
                    Pitch = note.Pitch,
                    StartPosition = note.StartPosition + note.Duration,
                    Duration = note.Duration,
                    Velocity = note.Velocity,
                    IsSelected = true
                };
                _pianoRollViewModel.Notes.Add(newNote);
                note.IsSelected = false;
            }
            
            // 复制音符后重新计算滚动范围，支持自动延长小节功能
            _pianoRollViewModel.UpdateMaxScrollExtent();
        }

        private void QuantizeSelectedNotes()
        {
            if (_pianoRollViewModel == null) return;

            foreach (var note in _pianoRollViewModel.Notes)
            {
                if (note.IsSelected)
                {
                    // 使用基于分数的量化
                    var quantizedPosition = _pianoRollViewModel.SnapToGrid(note.StartPosition);
                    note.StartPosition = quantizedPosition;
                }
            }
        }
    }
}