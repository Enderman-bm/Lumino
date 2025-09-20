using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;
using Color = System.Drawing.Color;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的撤销重做功能
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 撤销重做命令
        /// <summary>
        /// 撤销命令
        /// </summary>
        [RelayCommand]
        private void Undo()
        {
            if (_undoRedoManager != null && _undoRedoManager.CanUndo)
            {
                _undoRedoManager.Undo();
            }
        }

        /// <summary>
        /// 重做命令
        /// </summary>
        [RelayCommand]
        private void Redo()
        {
            if (_undoRedoManager != null && _undoRedoManager.CanRedo)
            {
                _undoRedoManager.Redo();
            }
        }
        #endregion

        #region 音符操作撤销重做
        /// <summary>
        /// 添加音符（带撤销重做支持）
        /// </summary>
        public void AddNoteWithUndo(NoteViewModel note)
        {
            var command = new AddNoteCommand(this, note);
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 删除音符（带撤销重做支持）
        /// </summary>
        public void RemoveNoteWithUndo(NoteViewModel note)
        {
            var command = new RemoveNoteCommand(this, note);
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 删除多个音符（带撤销重做支持）
        /// </summary>
        public void RemoveNotesWithUndo(IEnumerable<NoteViewModel> notes)
        {
            var command = new RemoveNotesCommand(this, notes.ToList());
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 移动音符（带撤销重做支持）
        /// </summary>
        public void MoveNoteWithUndo(NoteViewModel note, MusicalFraction oldStartPosition, int oldPitch)
        {
            var command = new MoveNoteCommand(this, note, oldStartPosition, oldPitch);
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 调整音符大小（带撤销重做支持）
        /// </summary>
        public void ResizeNoteWithUndo(NoteViewModel note, MusicalFraction oldDuration)
        {
            var command = new ResizeNoteCommand(this, note, oldDuration);
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 修改音符力度（带撤销重做支持）
        /// </summary>
        public void ChangeNoteVelocityWithUndo(NoteViewModel note, int oldVelocity)
        {
            var command = new ChangeNoteVelocityCommand(this, note, oldVelocity);
            _undoRedoManager?.ExecuteCommand(command);
        }
        #endregion

        #region 音轨操作撤销重做
        /// <summary>
        /// 添加音轨（带撤销重做支持）
        /// </summary>
        public void AddTrackWithUndo(string? name = null, System.Drawing.Color? color = null)
        {
            var command = new AddTrackCommand(this, name, color);
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 删除音轨（带撤销重做支持）
        /// </summary>
        public void RemoveTrackWithUndo(int trackIndex)
        {
            var command = new RemoveTrackCommand(this, trackIndex);
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 移动音轨（带撤销重做支持）
        /// </summary>
        public void MoveTrackWithUndo(int fromIndex, int toIndex)
        {
            var command = new MoveTrackCommand(this, fromIndex, toIndex);
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 重命名音轨（带撤销重做支持）
        /// </summary>
        public void RenameTrackWithUndo(int trackIndex, string oldName, string newName)
        {
            var command = new RenameTrackCommand(this, trackIndex, oldName, newName);
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 设置音轨颜色（带撤销重做支持）
        /// </summary>
        public void SetTrackColorWithUndo(int trackIndex, System.Drawing.Color oldColor, System.Drawing.Color newColor)
        {
            var command = new SetTrackColorCommand(this, trackIndex, oldColor, newColor);
            _undoRedoManager?.ExecuteCommand(command);
        }
        #endregion

        #region 批量操作撤销重做
        /// <summary>
        /// 批量移动音符（带撤销重做支持）
        /// </summary>
        public void MoveNotesWithUndo(IEnumerable<NoteViewModel> notes, MusicalFraction deltaTime, int deltaPitch)
        {
            var command = new MoveNotesCommand(this, notes.ToList(), deltaTime, deltaPitch);
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 批量调整音符大小（带撤销重做支持）
        /// </summary>
        public void ResizeNotesWithUndo(IEnumerable<NoteViewModel> notes, MusicalFraction deltaDuration)
        {
            var command = new ResizeNotesCommand(this, notes.ToList(), deltaDuration);
            _undoRedoManager?.ExecuteCommand(command);
        }

        /// <summary>
        /// 批量修改音符力度（带撤销重做支持）
        /// </summary>
        public void ChangeNotesVelocityWithUndo(IEnumerable<NoteViewModel> notes, int deltaVelocity)
        {
            var command = new ChangeNotesVelocityCommand(this, notes.ToList(), deltaVelocity);
            _undoRedoManager?.ExecuteCommand(command);
        }
        #endregion

        #region 撤销重做命令实现
        /// <summary>
        /// 撤销重做命令接口
        /// </summary>
        private interface IUndoableCommand
        {
            void Execute();
            void Undo();
        }

        /// <summary>
        /// 撤销重做管理器
        /// </summary>
        private class UndoRedoManager
        {
            private readonly Stack<IUndoableCommand> _undoStack = new Stack<IUndoableCommand>();
            private readonly Stack<IUndoableCommand> _redoStack = new Stack<IUndoableCommand>();

            public bool CanUndo => _undoStack.Count > 0;
            public bool CanRedo => _redoStack.Count > 0;

            public void ExecuteCommand(IUndoableCommand command)
            {
                command.Execute();
                _undoStack.Push(command);
                _redoStack.Clear(); // 执行新命令后清空重做栈
            }

            public void Undo()
            {
                if (CanUndo)
                {
                    var command = _undoStack.Pop();
                    command.Undo();
                    _redoStack.Push(command);
                }
            }

            public void Redo()
            {
                if (CanRedo)
                {
                    var command = _redoStack.Pop();
                    command.Execute();
                    _undoStack.Push(command);
                }
            }

            public void Clear()
            {
                _undoStack.Clear();
                _redoStack.Clear();
            }
        }

        private UndoRedoManager? _undoRedoManager;

        /// <summary>
        /// 初始化撤销重做管理器
        /// </summary>
        private void InitializeUndoRedoManager()
        {
            _undoRedoManager = new UndoRedoManager();
        }

        /// <summary>
        /// 添加音符命令
        /// </summary>
        private class AddNoteCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly NoteViewModel _note;

            public AddNoteCommand(PianoRollViewModel viewModel, NoteViewModel note)
            {
                _viewModel = viewModel;
                _note = note;
            }

            public void Execute()
            {
                _viewModel.Notes.Add(_note);
                if (_note.TrackIndex == _viewModel.CurrentTrackIndex)
                {
                    _viewModel.CurrentTrackNotes.Add(_note);
                }
                _viewModel.UpdateMaxScrollExtent();
            }

            public void Undo()
            {
                _viewModel.Notes.Remove(_note);
                _viewModel.CurrentTrackNotes.Remove(_note);
                _viewModel.UpdateMaxScrollExtent();
            }
        }

        /// <summary>
        /// 删除音符命令
        /// </summary>
        private class RemoveNoteCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly NoteViewModel _note;

            public RemoveNoteCommand(PianoRollViewModel viewModel, NoteViewModel note)
            {
                _viewModel = viewModel;
                _note = note;
            }

            public void Execute()
            {
                _viewModel.Notes.Remove(_note);
                _viewModel.CurrentTrackNotes.Remove(_note);
                _viewModel.UpdateMaxScrollExtent();
            }

            public void Undo()
            {
                _viewModel.Notes.Add(_note);
                if (_note.TrackIndex == _viewModel.CurrentTrackIndex)
                {
                    _viewModel.CurrentTrackNotes.Add(_note);
                }
                _viewModel.UpdateMaxScrollExtent();
            }
        }

        /// <summary>
        /// 批量删除音符命令
        /// </summary>
        private class RemoveNotesCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly List<NoteViewModel> _notes;

            public RemoveNotesCommand(PianoRollViewModel viewModel, List<NoteViewModel> notes)
            {
                _viewModel = viewModel;
                _notes = notes.ToList();
            }

            public void Execute()
            {
                foreach (var note in _notes)
                {
                    _viewModel.Notes.Remove(note);
                    _viewModel.CurrentTrackNotes.Remove(note);
                }
                _viewModel.UpdateMaxScrollExtent();
            }

            public void Undo()
            {
                foreach (var note in _notes)
                {
                    _viewModel.Notes.Add(note);
                    if (note.TrackIndex == _viewModel.CurrentTrackIndex)
                    {
                        _viewModel.CurrentTrackNotes.Add(note);
                    }
                }
                _viewModel.UpdateMaxScrollExtent();
            }
        }

        /// <summary>
        /// 移动音符命令
        /// </summary>
        private class MoveNoteCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly NoteViewModel _note;
            private readonly MusicalFraction _oldStartPosition;
            private readonly int _oldPitch;

            public MoveNoteCommand(PianoRollViewModel viewModel, NoteViewModel note, MusicalFraction oldStartPosition, int oldPitch)
            {
                _viewModel = viewModel;
                _note = note;
                _oldStartPosition = oldStartPosition;
                _oldPitch = oldPitch;
            }

            public void Execute()
            {
                // 已在移动操作中执行，这里不需要重复执行
            }

            public void Undo()
            {
                _note.StartPosition = _oldStartPosition;
                _note.Pitch = _oldPitch;
                _viewModel.OnPropertyChanged(nameof(_note.StartPosition));
                _viewModel.OnPropertyChanged(nameof(_note.Pitch));
                _viewModel.UpdateMaxScrollExtent();
            }
        }

        /// <summary>
        /// 调整音符大小命令
        /// </summary>
        private class ResizeNoteCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly NoteViewModel _note;
            private readonly MusicalFraction _oldDuration;

            public ResizeNoteCommand(PianoRollViewModel viewModel, NoteViewModel note, MusicalFraction oldDuration)
            {
                _viewModel = viewModel;
                _note = note;
                _oldDuration = oldDuration;
            }

            public void Execute()
            {
                // 已在调整大小操作中执行，这里不需要重复执行
            }

            public void Undo()
            {
                _note.Duration = _oldDuration;
                _viewModel.OnPropertyChanged(nameof(_note.Duration));
                _viewModel.UpdateMaxScrollExtent();
            }
        }

        /// <summary>
        /// 修改音符力度命令
        /// </summary>
        private class ChangeNoteVelocityCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly NoteViewModel _note;
            private readonly int _oldVelocity;

            public ChangeNoteVelocityCommand(PianoRollViewModel viewModel, NoteViewModel note, int oldVelocity)
            {
                _viewModel = viewModel;
                _note = note;
                _oldVelocity = oldVelocity;
            }

            public void Execute()
            {
                // 已在修改力度操作中执行，这里不需要重复执行
            }

            public void Undo()
            {
                _note.Velocity = _oldVelocity;
                _viewModel.OnPropertyChanged(nameof(_note.Velocity));
            }
        }

        /// <summary>
        /// 添加音轨命令
        /// </summary>
        private class AddTrackCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly string? _name;
            private readonly Color? _color;
            private int _trackIndex = -1;

            public AddTrackCommand(PianoRollViewModel viewModel, string? name, Color? color)
            {
                _viewModel = viewModel;
                _name = name;
                _color = color;
            }

            public void Execute()
            {
                var track = new TrackViewModel(_viewModel.Tracks.Count, "", _name ?? $"Track {_viewModel.Tracks.Count + 1}", false)
                {
                    Color = ConvertDrawingColorToAvalonia(_color ?? System.Drawing.Color.FromArgb(255, 0, 0, 255)),
                    Index = _viewModel.Tracks.Count
                };
                _viewModel.Tracks.Add(track);
                _trackIndex = _viewModel.Tracks.Count - 1;
            }

            public void Undo()
            {
                if (_trackIndex >= 0 && _trackIndex < _viewModel.Tracks.Count)
                {
                    _viewModel.Tracks.RemoveAt(_trackIndex);
                    // 重新索引
                    for (int i = _trackIndex; i < _viewModel.Tracks.Count; i++)
                    {
                        _viewModel.Tracks[i].Index = i;
                    }
                }
            }
        }

        /// <summary>
        /// 删除音轨命令
        /// </summary>
        private class RemoveTrackCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly int _trackIndex;
            private TrackViewModel? _removedTrack;
            private List<NoteViewModel> _removedNotes = new List<NoteViewModel>();

            public RemoveTrackCommand(PianoRollViewModel viewModel, int trackIndex)
            {
                _viewModel = viewModel;
                _trackIndex = trackIndex;
            }

            public void Execute()
            {
                if (_trackIndex >= 0 && _trackIndex < _viewModel.Tracks.Count)
                {
                    _removedTrack = _viewModel.Tracks[_trackIndex];
                    _viewModel.Tracks.RemoveAt(_trackIndex);

                    // 删除该音轨的所有音符
                    _removedNotes = _viewModel.Notes.Where(n => n.TrackIndex == _trackIndex).ToList();
                    foreach (var note in _removedNotes)
                    {
                        _viewModel.Notes.Remove(note);
                        _viewModel.CurrentTrackNotes.Remove(note);
                    }

                    // 重新索引
                    for (int i = _trackIndex; i < _viewModel.Tracks.Count; i++)
                    {
                        _viewModel.Tracks[i].Index = i;
                    }

                    // 重新索引音符
                    foreach (var note in _viewModel.Notes.Where(n => n.TrackIndex > _trackIndex))
                    {
                        note.TrackIndex--;
                    }

                    _viewModel.UpdateMaxScrollExtent();
                }
            }

            public void Undo()
            {
                if (_removedTrack != null)
                {
                    _viewModel.Tracks.Insert(_trackIndex, _removedTrack);

                    // 恢复音符
                    foreach (var note in _removedNotes)
                    {
                        _viewModel.Notes.Add(note);
                        if (note.TrackIndex == _viewModel.CurrentTrackIndex)
                        {
                            _viewModel.CurrentTrackNotes.Add(note);
                        }
                    }

                    // 重新索引
                    for (int i = _trackIndex; i < _viewModel.Tracks.Count; i++)
                    {
                        _viewModel.Tracks[i].Index = i;
                    }

                    // 重新索引音符
                    foreach (var note in _viewModel.Notes.Where(n => n.TrackIndex >= _trackIndex))
                    {
                        note.TrackIndex++;
                    }

                    _viewModel.UpdateMaxScrollExtent();
                }
            }
        }

        /// <summary>
        /// 移动音轨命令
        /// </summary>
        private class MoveTrackCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly int _fromIndex;
            private readonly int _toIndex;

            public MoveTrackCommand(PianoRollViewModel viewModel, int fromIndex, int toIndex)
            {
                _viewModel = viewModel;
                _fromIndex = fromIndex;
                _toIndex = toIndex;
            }

            public void Execute()
            {
                if (_fromIndex >= 0 && _fromIndex < _viewModel.Tracks.Count &&
                    _toIndex >= 0 && _toIndex < _viewModel.Tracks.Count)
                {
                    var track = _viewModel.Tracks[_fromIndex];
                    _viewModel.Tracks.RemoveAt(_fromIndex);
                    _viewModel.Tracks.Insert(_toIndex, track);

                    // 重新索引
                    for (int i = Math.Min(_fromIndex, _toIndex); i < _viewModel.Tracks.Count; i++)
                    {
                        _viewModel.Tracks[i].Index = i;
                    }

                    // 更新音符的音轨索引
                    foreach (var note in _viewModel.Notes)
                    {
                        if (note.TrackIndex == _fromIndex)
                        {
                            note.TrackIndex = _toIndex;
                        }
                        else if (_fromIndex < _toIndex && note.TrackIndex > _fromIndex && note.TrackIndex <= _toIndex)
                        {
                            note.TrackIndex--;
                        }
                        else if (_fromIndex > _toIndex && note.TrackIndex < _fromIndex && note.TrackIndex >= _toIndex)
                        {
                            note.TrackIndex++;
                        }
                    }
                }
            }

            public void Undo()
            {
                // 执行反向移动
                var tempCommand = new MoveTrackCommand(_viewModel, _toIndex, _fromIndex);
                tempCommand.Execute();
            }
        }

        /// <summary>
        /// 重命名音轨命令
        /// </summary>
        private class RenameTrackCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly int _trackIndex;
            private readonly string _oldName;
            private readonly string _newName;

            public RenameTrackCommand(PianoRollViewModel viewModel, int trackIndex, string oldName, string newName)
            {
                _viewModel = viewModel;
                _trackIndex = trackIndex;
                _oldName = oldName;
                _newName = newName;
            }

            public void Execute()
            {
                if (_trackIndex >= 0 && _trackIndex < _viewModel.Tracks.Count)
                {
                    _viewModel.Tracks[_trackIndex].Name = _newName;
                    if (_viewModel.CurrentTrackIndex == _trackIndex)
                    {
                        _viewModel.OnPropertyChanged(nameof(_viewModel.CurrentTrackName));
                    }
                }
            }

            public void Undo()
            {
                if (_trackIndex >= 0 && _trackIndex < _viewModel.Tracks.Count)
                {
                    _viewModel.Tracks[_trackIndex].Name = _oldName;
                    if (_viewModel.CurrentTrackIndex == _trackIndex)
                    {
                        _viewModel.OnPropertyChanged(nameof(_viewModel.CurrentTrackName));
                    }
                }
            }
        }

        /// <summary>
        /// 设置音轨颜色命令
        /// </summary>
        private class SetTrackColorCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly int _trackIndex;
            private readonly System.Drawing.Color _oldColor;
            private readonly System.Drawing.Color _newColor;

            public SetTrackColorCommand(PianoRollViewModel viewModel, int trackIndex, System.Drawing.Color oldColor, System.Drawing.Color newColor)
            {
                _viewModel = viewModel;
                _trackIndex = trackIndex;
                _oldColor = oldColor;
                _newColor = newColor;
            }

            public void Execute()
            {
                if (_trackIndex >= 0 && _trackIndex < _viewModel.Tracks.Count)
                {
                    _viewModel.Tracks[_trackIndex].Color = ConvertDrawingColorToAvalonia(_newColor);
                    if (_viewModel.CurrentTrackIndex == _trackIndex)
                    {
                        _viewModel.OnPropertyChanged(nameof(_viewModel.CurrentTrackColor));
                    }
                }
            }

            public void Undo()
            {
                if (_trackIndex >= 0 && _trackIndex < _viewModel.Tracks.Count)
                {
                    _viewModel.Tracks[_trackIndex].Color = ConvertDrawingColorToAvalonia(_oldColor);
                    if (_viewModel.CurrentTrackIndex == _trackIndex)
                    {
                        _viewModel.OnPropertyChanged(nameof(_viewModel.CurrentTrackColor));
                    }
                }
            }
        }

        /// <summary>
        /// 批量移动音符命令
        /// </summary>
        private class MoveNotesCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly List<NoteViewModel> _notes;
            private readonly MusicalFraction _deltaTime;
            private readonly int _deltaPitch;

            public MoveNotesCommand(PianoRollViewModel viewModel, List<NoteViewModel> notes, MusicalFraction deltaTime, int deltaPitch)
            {
                _viewModel = viewModel;
                _notes = notes;
                _deltaTime = deltaTime;
                _deltaPitch = deltaPitch;
            }

            public void Execute()
            {
                foreach (var note in _notes)
                {
                    note.StartPosition += _deltaTime;
                    note.Pitch += _deltaPitch;
                    _viewModel.OnPropertyChanged(nameof(note.StartPosition));
                    _viewModel.OnPropertyChanged(nameof(note.Pitch));
                }
                _viewModel.UpdateMaxScrollExtent();
            }

            public void Undo()
            {
                foreach (var note in _notes)
                {
                    note.StartPosition -= _deltaTime;
                    note.Pitch -= _deltaPitch;
                    _viewModel.OnPropertyChanged(nameof(note.StartPosition));
                    _viewModel.OnPropertyChanged(nameof(note.Pitch));
                }
                _viewModel.UpdateMaxScrollExtent();
            }
        }

        /// <summary>
        /// 批量调整音符大小命令
        /// </summary>
        private class ResizeNotesCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly List<NoteViewModel> _notes;
            private readonly MusicalFraction _deltaDuration;

            public ResizeNotesCommand(PianoRollViewModel viewModel, List<NoteViewModel> notes, MusicalFraction deltaDuration)
            {
                _viewModel = viewModel;
                _notes = notes;
                _deltaDuration = deltaDuration;
            }

            public void Execute()
            {
                foreach (var note in _notes)
                {
                    note.Duration += _deltaDuration;
                    _viewModel.OnPropertyChanged(nameof(note.Duration));
                }
                _viewModel.UpdateMaxScrollExtent();
            }

            public void Undo()
            {
                foreach (var note in _notes)
                {
                    note.Duration -= _deltaDuration;
                    _viewModel.OnPropertyChanged(nameof(note.Duration));
                }
                _viewModel.UpdateMaxScrollExtent();
            }
        }

        /// <summary>
        /// 批量修改音符力度命令
        /// </summary>
        private class ChangeNotesVelocityCommand : IUndoableCommand
        {
            private readonly PianoRollViewModel _viewModel;
            private readonly List<NoteViewModel> _notes;
            private readonly int _deltaVelocity;

            public ChangeNotesVelocityCommand(PianoRollViewModel viewModel, List<NoteViewModel> notes, int deltaVelocity)
            {
                _viewModel = viewModel;
                _notes = notes;
                _deltaVelocity = deltaVelocity;
            }

            public void Execute()
            {
                foreach (var note in _notes)
                {
                    note.Velocity = Math.Max(0, Math.Min(127, note.Velocity + _deltaVelocity));
                    _viewModel.OnPropertyChanged(nameof(note.Velocity));
                }
            }

            public void Undo()
            {
                foreach (var note in _notes)
                {
                    note.Velocity = Math.Max(0, Math.Min(127, note.Velocity - _deltaVelocity));
                    _viewModel.OnPropertyChanged(nameof(note.Velocity));
                }
            }
        }
        #endregion

        private static Avalonia.Media.Color ConvertDrawingColorToAvalonia(System.Drawing.Color drawingColor)
        {
            return Avalonia.Media.Color.FromRgb(drawingColor.R, drawingColor.G, drawingColor.B);
        }
    }
}