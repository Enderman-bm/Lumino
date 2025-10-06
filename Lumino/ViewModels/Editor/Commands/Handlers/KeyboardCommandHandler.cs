using Avalonia.Input;
using Lumino.Models.Music;
using Lumino.Views.Controls.Editing;
using System.Collections.Generic;
using EnderDebugger;

namespace Lumino.ViewModels.Editor.Commands
{
    /// <summary>
    /// 键盘命令处理器 - 基于分数的新实现
    /// </summary>
    public class KeyboardCommandHandler
    {
        private PianoRollViewModel? _pianoRollViewModel;
        private readonly EnderLogger _logger = EnderLogger.Instance;

        public void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        public void HandleKey(KeyCommandArgs args)
        {
            if (_pianoRollViewModel == null) return;

            _logger.Debug("HandleKey", $"处理按键: {args.Key}, 修饰键: {args.Modifiers}");

            switch (args.Key)
            {
                case Key.Delete:
                    _logger.Debug("HandleKey", "Delete键被按下，调用DeleteSelectedNotes");
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
            }
        }

        private void DeleteSelectedNotes()
        {
            if (_pianoRollViewModel == null) return;

            _logger.Debug("DeleteSelectedNotes", "开始删除选中的音符");

            var notesToDelete = new List<(NoteViewModel note, int index)>();
            for (int i = _pianoRollViewModel.Notes.Count - 1; i >= 0; i--)
            {
                if (_pianoRollViewModel.Notes[i].IsSelected)
                {
                    notesToDelete.Add((_pianoRollViewModel.Notes[i], i));
                    _logger.Debug("DeleteSelectedNotes", $"找到选中的音符: 索引 {i}, 音符 {_pianoRollViewModel.Notes[i]}");
                }
            }

            _logger.Debug("DeleteSelectedNotes", $"找到 {notesToDelete.Count} 个选中的音符");

            if (notesToDelete.Count > 0)
            {
                var deleteOperation = new Lumino.Services.Implementation.DeleteNotesOperation(_pianoRollViewModel, notesToDelete);
                _pianoRollViewModel.UndoRedoService.ExecuteAndRecord(deleteOperation);
                _logger.Debug("DeleteSelectedNotes", "删除操作已执行");
            }
            else
            {
                _logger.Debug("DeleteSelectedNotes", "没有选中的音符，跳过删除操作");
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