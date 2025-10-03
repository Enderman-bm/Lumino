using System;
using System.Collections.Generic;
using System.Linq;
using Lumino.Models.Music;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;

namespace Lumino.Services.Implementation
{
    /// <summary>
    /// 添加音符操作
    /// </summary>
    public class AddNoteOperation : IUndoRedoOperation
    {
        private readonly PianoRollViewModel _pianoRollViewModel;
        private readonly NoteViewModel _note;

        public AddNoteOperation(PianoRollViewModel pianoRollViewModel, NoteViewModel note)
        {
            _pianoRollViewModel = pianoRollViewModel;
            _note = note;
        }

        public string Description => "添加音符";

        public void Execute()
        {
            _pianoRollViewModel.Notes.Add(_note);
            _pianoRollViewModel.UpdateMaxScrollExtent();
        }

        public void Undo()
        {
            _pianoRollViewModel.Notes.Remove(_note);
            _pianoRollViewModel.UpdateMaxScrollExtent();
        }
    }

    /// <summary>
    /// 删除音符操作
    /// </summary>
    public class DeleteNoteOperation : IUndoRedoOperation
    {
        private readonly PianoRollViewModel _pianoRollViewModel;
        private readonly NoteViewModel _note;
        private readonly int _originalIndex;

        public DeleteNoteOperation(PianoRollViewModel pianoRollViewModel, NoteViewModel note, int originalIndex)
        {
            _pianoRollViewModel = pianoRollViewModel;
            _note = note;
            _originalIndex = originalIndex;
        }

        public string Description => "删除音符";

        public void Execute()
        {
            _pianoRollViewModel.Notes.Remove(_note);
            _pianoRollViewModel.UpdateMaxScrollExtent();
        }

        public void Undo()
        {
            if (_originalIndex >= 0 && _originalIndex <= _pianoRollViewModel.Notes.Count)
            {
                _pianoRollViewModel.Notes.Insert(_originalIndex, _note);
            }
            else
            {
                _pianoRollViewModel.Notes.Add(_note);
            }
            _pianoRollViewModel.UpdateMaxScrollExtent();
        }
    }

    /// <summary>
    /// 批量删除音符操作
    /// </summary>
    public class DeleteNotesOperation : IUndoRedoOperation
    {
        private readonly PianoRollViewModel _pianoRollViewModel;
        private readonly List<(NoteViewModel note, int index)> _deletedNotes;

        public DeleteNotesOperation(PianoRollViewModel pianoRollViewModel, List<(NoteViewModel note, int index)> deletedNotes)
        {
            _pianoRollViewModel = pianoRollViewModel;
            _deletedNotes = deletedNotes;
        }

        public string Description => $"删除 {_deletedNotes.Count} 个音符";

        public void Execute()
        {
            foreach (var (note, _) in _deletedNotes)
            {
                _pianoRollViewModel.Notes.Remove(note);
            }
            _pianoRollViewModel.UpdateMaxScrollExtent();
        }

        public void Undo()
        {
            foreach (var (note, index) in _deletedNotes.OrderBy(x => x.index))
            {
                if (index >= 0 && index <= _pianoRollViewModel.Notes.Count)
                {
                    _pianoRollViewModel.Notes.Insert(index, note);
                }
                else
                {
                    _pianoRollViewModel.Notes.Add(note);
                }
            }
            _pianoRollViewModel.UpdateMaxScrollExtent();
        }
    }

    /// <summary>
    /// 修改音符操作
    /// </summary>
    public class ModifyNoteOperation : IUndoRedoOperation
    {
        private readonly NoteViewModel _note;
        private readonly MusicalFraction _oldStartPosition;
        private readonly MusicalFraction _oldDuration;
        private readonly int _oldPitch;
        private readonly int _oldVelocity;
        private readonly MusicalFraction _newStartPosition;
        private readonly MusicalFraction _newDuration;
        private readonly int _newPitch;
        private readonly int _newVelocity;

        public ModifyNoteOperation(
            NoteViewModel note,
            MusicalFraction oldStartPosition,
            MusicalFraction oldDuration,
            int oldPitch,
            int oldVelocity,
            MusicalFraction newStartPosition,
            MusicalFraction newDuration,
            int newPitch,
            int newVelocity)
        {
            _note = note;
            _oldStartPosition = oldStartPosition;
            _oldDuration = oldDuration;
            _oldPitch = oldPitch;
            _oldVelocity = oldVelocity;
            _newStartPosition = newStartPosition;
            _newDuration = newDuration;
            _newPitch = newPitch;
            _newVelocity = newVelocity;
        }

        public string Description => "修改音符";

        public void Execute()
        {
            _note.StartPosition = _newStartPosition;
            _note.Duration = _newDuration;
            _note.Pitch = _newPitch;
            _note.Velocity = _newVelocity;
        }

        public void Undo()
        {
            _note.StartPosition = _oldStartPosition;
            _note.Duration = _oldDuration;
            _note.Pitch = _oldPitch;
            _note.Velocity = _oldVelocity;
        }
    }

    /// <summary>
    /// 移动音符操作
    /// </summary>
    public class MoveNotesOperation : IUndoRedoOperation
    {
        private readonly List<(NoteViewModel note, MusicalFraction oldPosition, int oldPitch)> _movedNotes;
        private readonly MusicalFraction _deltaTime;
        private readonly int _deltaPitch;

        public MoveNotesOperation(
            List<(NoteViewModel note, MusicalFraction oldPosition, int oldPitch)> movedNotes,
            MusicalFraction deltaTime,
            int deltaPitch)
        {
            _movedNotes = movedNotes;
            _deltaTime = deltaTime;
            _deltaPitch = deltaPitch;
        }

        public string Description => $"移动 {_movedNotes.Count} 个音符";

        public void Execute()
        {
            foreach (var (note, _, _) in _movedNotes)
            {
                note.StartPosition += _deltaTime;
                note.Pitch += _deltaPitch;
            }
        }

        public void Undo()
        {
            foreach (var (note, oldPosition, oldPitch) in _movedNotes)
            {
                note.StartPosition = oldPosition;
                note.Pitch = oldPitch;
            }
        }
    }
}