using Avalonia.Input;
using Lumino.Models.Music;
using Lumino.Views.Controls.Editing;
using System.Collections.Generic;

namespace Lumino.ViewModels.Editor.Commands
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
                    _pianoRollViewModel.SetCurrentTool(EditorTool.Pencil);
                    break;

                case Key.D2:
                case Key.S:
                    _pianoRollViewModel.SetCurrentTool(EditorTool.Select);
                    break;

                case Key.D3:
                case Key.E:
                    _pianoRollViewModel.SetCurrentTool(EditorTool.Eraser);
                    break;

                case Key.D4:
                case Key.C:
                    _pianoRollViewModel.SetCurrentTool(EditorTool.Cut);
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

                // 撤销重做快捷键
                case Key.Z when args.Modifiers.HasFlag(KeyModifiers.Control):
                    _pianoRollViewModel.Undo();
                    break;

                case Key.Y when args.Modifiers.HasFlag(KeyModifiers.Control):
                    _pianoRollViewModel.Redo();
                    break;

                case Key.Z when args.Modifiers.HasFlag(KeyModifiers.Control) && args.Modifiers.HasFlag(KeyModifiers.Shift):
                    _pianoRollViewModel.Redo();
                    break;

                // CC 点微调快捷键
                case Key.OemPlus:
                case Key.Add:
                    _pianoRollViewModel.NudgeSelectedControllerEvent(1);
                    break;

                case Key.OemMinus:
                case Key.Subtract:
                    _pianoRollViewModel.NudgeSelectedControllerEvent(-1);
                    break;
            }
        }

        private void DeleteSelectedNotes()
        {
            if (_pianoRollViewModel == null) return;

            var notesToDelete = new List<(NoteViewModel note, int index)>();
            for (int i = _pianoRollViewModel.Notes.Count - 1; i >= 0; i--)
            {
                if (_pianoRollViewModel.Notes[i].IsSelected)
                {
                    notesToDelete.Add((_pianoRollViewModel.Notes[i], i));
                }
            }

            if (notesToDelete.Count > 0)
            {
                var deleteOperation = new Lumino.Services.Implementation.DeleteNotesOperation(_pianoRollViewModel, notesToDelete);
                _pianoRollViewModel.UndoRedoService.ExecuteAndRecord(deleteOperation);
            }
        }

        private void DuplicateSelectedNotes()
        {
            if (_pianoRollViewModel == null) return;

            var notesToAdd = new List<NoteViewModel>();
            foreach (var note in _pianoRollViewModel.Notes)
            {
                if (note.IsSelected)
                {
                    var newNote = new NoteViewModel
                    {
                        Pitch = note.Pitch,
                        StartPosition = note.StartPosition + note.Duration,
                        Duration = note.Duration,
                        Velocity = note.Velocity,
                        IsSelected = true
                    };
                    notesToAdd.Add(newNote);
                    note.IsSelected = false;
                }
            }

            // 批量添加复制的音符
            foreach (var note in notesToAdd)
            {
                var addOperation = new Lumino.Services.Implementation.AddNoteOperation(_pianoRollViewModel, note);
                _pianoRollViewModel.UndoRedoService.ExecuteAndRecord(addOperation);
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