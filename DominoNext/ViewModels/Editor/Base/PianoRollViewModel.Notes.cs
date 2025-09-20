using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.Input;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.ViewModels.Editor
{
    /// <summary>
    /// PianoRollViewModel的音符操作功能
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 音符创建命令
        /// <summary>
        /// 创建音符命令
        /// </summary>
        private void CreateNote(int pitch, double startTime, double duration, int velocity = 100)
        {
            if (CurrentTrack == null) return;

            var note = new NoteViewModel
            {
                Pitch = pitch,
                StartTime = startTime,
                Duration = MusicalFraction.FromDouble(duration),
                Velocity = velocity,
                TrackIndex = CurrentTrackIndex,
                Color = CurrentTrack.Color
            };

            // 添加到当前音轨
            CurrentTrack.Notes.Add(note);
            Notes.Add(note);

            // 添加到撤销历史
            // var command = new AddNoteCommand(this, note);
            // UndoRedoManager.ExecuteCommand(command);
            
            // 暂时不添加到撤销历史，等待命令类实现

            // 触发音符创建事件
            OnNoteCreated(note);
        }

        /// <summary>
        /// 删除音符命令
        /// </summary>
        private void DeleteNote(NoteViewModel note)
        {
            if (note == null) return;

            // 从音符集合中移除
            Notes.Remove(note);
            
            // 从对应音轨中移除
            var track = Tracks.FirstOrDefault(t => t.Index == note.TrackIndex);
            track?.Notes.Remove(note);

            // 添加到撤销历史
            // var command = new DeleteNoteCommand(this, note);
            // UndoRedoManager.ExecuteCommand(command);
            
            // 暂时不添加到撤销历史，等待命令类实现

            // 触发音符删除事件
            OnNoteDeleted(note);
        }

        /// <summary>
        /// 复制音符命令
        /// </summary>
        private void DuplicateNote(NoteViewModel note)
        {
            if (note == null) return;

            var newNote = new NoteViewModel
            {
                Pitch = note.Pitch,
                StartTime = note.StartTime + note.Duration.ToDouble(), // 复制到原音符后面
                Duration = note.Duration,
                Velocity = note.Velocity,
                TrackIndex = note.TrackIndex,
                Color = note.Color
            };

            // 添加到音符集合
            Notes.Add(newNote);
            
            // 添加到对应音轨
            var track = Tracks.FirstOrDefault(t => t.Index == note.TrackIndex);
            track?.Notes.Add(newNote);

            // 添加到撤销历史
            // var command = new AddNoteCommand(this, newNote);
            // UndoRedoManager.ExecuteCommand(command);
            
            // 暂时不添加到撤销历史，等待命令类实现
            OnPropertyChanged(nameof(Notes));

            // 触发音符创建事件
            OnNoteCreated(newNote);
        }

        /// <summary>
        /// 分割音符命令
        /// </summary>
        private void SplitNote(NoteViewModel note, double splitTime)
        {
            if (note == null) return;
            if (splitTime <= note.StartTime || splitTime >= note.StartTime + note.Duration.ToDouble()) return;

            // 计算分割位置
            var firstDuration = splitTime - note.StartTime;
            var secondDuration = note.Duration.ToDouble() - firstDuration;

            // 创建第二个音符
            var secondNote = new NoteViewModel
            {
                Pitch = note.Pitch,
                StartTime = splitTime,
                Duration = MusicalFraction.FromDouble(secondDuration),
                Velocity = note.Velocity,
                TrackIndex = note.TrackIndex,
                Color = note.Color
            };

            // 修改原音符的时长
            note.Duration = MusicalFraction.FromDouble(firstDuration);

            // 添加第二个音符
            Notes.Add(secondNote);
            
            // 添加到对应音轨
            var track = Tracks.FirstOrDefault(t => t.Index == note.TrackIndex);
            track?.Notes.Add(secondNote);

            // 添加到撤销历史
            // var command = new SplitNoteCommand(this, note, secondNote);
            // UndoRedoManager.ExecuteCommand(command);
            
            // 暂时不添加到撤销历史，等待命令类实现

            // 触发音符分割事件
            OnNoteSplit(note, secondNote);
        }

        /// <summary>
        /// 合并音符命令
        /// </summary>
        private void MergeNotes(NoteViewModel firstNote, NoteViewModel secondNote)
        {
            if (firstNote == null || secondNote == null) return;
            if (firstNote.Pitch != secondNote.Pitch) return;
            if (firstNote.TrackIndex != secondNote.TrackIndex) return;
            if (firstNote.StartTime + firstNote.Duration.ToDouble() != secondNote.StartTime) return;

            // 合并时长
            firstNote.Duration = MusicalFraction.FromDouble(firstNote.Duration.ToDouble() + secondNote.Duration.ToDouble());

            // 删除第二个音符
            Notes.Remove(secondNote);
            
            // 从对应音轨中移除
            var track = Tracks.FirstOrDefault(t => t.Index == secondNote.TrackIndex);
            track?.Notes.Remove(secondNote);

            // 添加到撤销历史
            // var command = new MergeNotesCommand(this, firstNote, secondNote);
            // UndoRedoManager.ExecuteCommand(command);
            
            // 暂时不添加到撤销历史，等待命令类实现

            // 触发音符合并事件
            OnNotesMerged(firstNote, secondNote);
        }
        #endregion

        #region 音符编辑命令
        /// <summary>
        /// 移动音符命令
        /// </summary>
        private void MoveNote(NoteViewModel note, double newStartTime, int newPitch)
        {
            if (note == null) return;

            var oldStartTime = note.StartTime;
            var oldPitch = note.Pitch;

            // 更新音符位置
            note.StartTime = newStartTime;
            note.Pitch = newPitch;

            // 添加到撤销历史
            // var command = new MoveNoteCommand(this, note, oldStartTime, oldPitch);
            // UndoRedoManager.ExecuteCommand(command);
            
            // 暂时不添加到撤销历史，等待命令类实现

            // 触发音符移动事件
            OnNoteMoved(note, oldStartTime, oldPitch);
        }

        /// <summary>
        /// 调整音符大小命令
        /// </summary>
        private void ResizeNote(NoteViewModel note, double newDuration)
        {
            if (note == null) return;
            if (newDuration <= 0) return;

            var oldDuration = note.Duration;

            // 更新音符时长
            note.Duration = MusicalFraction.FromDouble(newDuration);

            // 添加到撤销历史
            // var command = new ResizeNoteCommand(this, note, oldDuration);
            // UndoRedoManager.ExecuteCommand(command);
            
            // 暂时不添加到撤销历史，等待命令类实现

            // 触发音符大小调整事件
            OnNoteResized(note, oldDuration.ToDouble());
        }

        /// <summary>
        /// 修改音符力度命令
        /// </summary>
        private void ChangeNoteVelocity(NoteViewModel note, int newVelocity)
        {
            if (note == null) return;
            if (newVelocity < 0 || newVelocity > 127) return;

            var oldVelocity = note.Velocity;

            // 更新音符力度
            note.Velocity = newVelocity;

            // 添加到撤销历史
            // var command = new ChangeVelocityCommand(this, note, oldVelocity);
            // UndoRedoManager.ExecuteCommand(command);
            
            // 暂时不添加到撤销历史，等待命令类实现

            // 触发音符力度变化事件
            OnNoteVelocityChanged(note, oldVelocity);
        }

        /// <summary>
        /// 修改音符音高命令
        /// </summary>
        private void ChangeNotePitch(NoteViewModel note, int newPitch)
        {
            if (note == null) return;
            if (newPitch < 0 || newPitch > 127) return;

            var oldPitch = note.Pitch;

            // 更新音符音高
            note.Pitch = newPitch;

            // 添加到撤销历史
            // var command = new ChangePitchCommand(this, note, oldPitch);
            // UndoRedoManager.ExecuteCommand(command);
            
            // 暂时不添加到撤销历史，等待命令类实现

            // 触发音符音高变化事件
            OnNotePitchChanged(note, oldPitch);
        }

        /// <summary>
        /// 修改音符音轨命令
        /// </summary>
        private void ChangeNoteTrack(NoteViewModel note, int newTrackIndex)
        {
            if (note == null) return;
            if (newTrackIndex < 0 || newTrackIndex >= Tracks.Count) return;

            var oldTrackIndex = note.TrackIndex;

            // 从原音轨中移除
            var oldTrack = Tracks.FirstOrDefault(t => t.Index == oldTrackIndex);
            oldTrack?.Notes.Remove(note);

            // 更新音符音轨索引
            note.TrackIndex = newTrackIndex;

            // 添加到新音轨
            var newTrack = Tracks.FirstOrDefault(t => t.Index == newTrackIndex);
            if (newTrack != null)
            {
                newTrack.Notes.Add(note);
                note.Color = newTrack.Color;
            }

            // 添加到撤销历史
            // var command = new ChangeNoteTrackCommand(this, note, oldTrackIndex);
            // UndoRedoManager.ExecuteCommand(command);
            
            // 暂时不添加到撤销历史，等待命令类实现
            OnPropertyChanged(nameof(Tracks));

            // 触发音符音轨变化事件
            OnNoteTrackChanged(note, oldTrackIndex);
        }
        #endregion

        #region 批量音符操作
        /// <summary>
        /// 批量移动音符命令
        /// </summary>
        private void MoveSelectedNotes(double timeDelta, int pitchDelta)
        {
            var selectedNotes = SelectedNotes.ToList();
            if (!selectedNotes.Any()) return;

            // 批量操作开始
            BeginBatchOperation();

            try
            {
                // 移动每个选中的音符
                foreach (var note in selectedNotes)
                {
                    var newStartTime = Math.Max(0, note.StartTime + timeDelta);
                    var newPitch = Math.Max(0, Math.Min(127, note.Pitch + pitchDelta));
                    
                    MoveNote(note, newStartTime, newPitch);
                }
            }
            finally
            {
                // 批量操作结束
                OnBatchOperationCompleted();
            }
        }

        /// <summary>
        /// 批量调整音符大小命令
        /// </summary>
        private void ResizeSelectedNotes(double durationDelta)
        {
            var selectedNotes = SelectedNotes.ToList();
            if (!selectedNotes.Any()) return;

            // 批量操作开始
            BeginBatchOperation();

            try
            {
                // 调整每个选中的音符大小
                foreach (var note in selectedNotes)
                {
                    var newDuration = Math.Max(0.1, note.Duration.ToDouble() + durationDelta);
                    ResizeNote(note, newDuration);
                }
            }
            finally
            {
                // 批量操作结束
                OnBatchOperationCompleted();
            }
        }

        /// <summary>
        /// 批量修改音符力度命令
        /// </summary>
        private void ChangeSelectedNotesVelocity(int velocityDelta)
        {
            var selectedNotes = SelectedNotes.ToList();
            if (!selectedNotes.Any()) return;

            // 批量操作开始
            BeginBatchOperation();

            try
            {
                // 修改每个选中的音符力度
                foreach (var note in selectedNotes)
                {
                    var newVelocity = Math.Max(0, Math.Min(127, note.Velocity + velocityDelta));
                    ChangeNoteVelocity(note, newVelocity);
                }
            }
            finally
            {
                // 批量操作结束
                OnBatchOperationCompleted();
            }
        }

        /// <summary>
        /// 批量删除音符命令
        /// </summary>
        private void DeleteSelectedNotes()
        {
            var selectedNotes = SelectedNotes.ToList();
            if (!selectedNotes.Any()) return;

            // 批量操作开始
            BeginBatchOperation();

            try
            {
                // 删除每个选中的音符
                foreach (var note in selectedNotes.ToList())
                {
                    DeleteNote(note);
                }
            }
            finally
            {
                // 批量操作结束
                OnBatchOperationCompleted();
            }
        }
        #endregion

        #region 音符操作事件
        /// <summary>
        /// 音符创建事件
        /// </summary>
        public event EventHandler<NoteViewModel>? NoteCreated;

        /// <summary>
        /// 音符删除事件
        /// </summary>
        public event EventHandler<NoteViewModel>? NoteDeleted;

        /// <summary>
        /// 音符移动事件
        /// </summary>
        public event EventHandler<(NoteViewModel note, double oldStartTime, int oldPitch)>? NoteMoved;

        /// <summary>
        /// 音符大小调整事件
        /// </summary>
        public event EventHandler<(NoteViewModel note, double oldDuration)>? NoteResized;

        /// <summary>
        /// 音符力度变化事件
        /// </summary>
        public event EventHandler<(NoteViewModel note, int oldVelocity)>? NoteVelocityChanged;

        /// <summary>
        /// 音符音高变化事件
        /// </summary>
        public event EventHandler<(NoteViewModel note, int oldPitch)>? NotePitchChanged;

        /// <summary>
        /// 音符音轨变化事件
        /// </summary>
        public event EventHandler<(NoteViewModel note, int oldTrackIndex)>? NoteTrackChanged;

        /// <summary>
        /// 音符分割事件
        /// </summary>
        public event EventHandler<(NoteViewModel originalNote, NoteViewModel newNote)>? NoteSplit;

        /// <summary>
        /// 音符合并事件
        /// </summary>
        public event EventHandler<(NoteViewModel firstNote, NoteViewModel secondNote)>? NotesMerged;

        /// <summary>
        /// 触发音符创建事件
        /// </summary>
        private void OnNoteCreated(NoteViewModel note)
        {
            NoteCreated?.Invoke(this, note);
        }

        /// <summary>
        /// 触发音符删除事件
        /// </summary>
        private void OnNoteDeleted(NoteViewModel note)
        {
            NoteDeleted?.Invoke(this, note);
        }

        /// <summary>
        /// 触发音符移动事件
        /// </summary>
        private void OnNoteMoved(NoteViewModel note, double oldStartTime, int oldPitch)
        {
            NoteMoved?.Invoke(this, (note, oldStartTime, oldPitch));
        }

        /// <summary>
        /// 触发音符大小调整事件
        /// </summary>
        private void OnNoteResized(NoteViewModel note, double oldDuration)
        {
            NoteResized?.Invoke(this, (note, oldDuration));
        }

        /// <summary>
        /// 触发音符力度变化事件
        /// </summary>
        private void OnNoteVelocityChanged(NoteViewModel note, int oldVelocity)
        {
            NoteVelocityChanged?.Invoke(this, (note, oldVelocity));
        }

        /// <summary>
        /// 触发音符音高变化事件
        /// </summary>
        private void OnNotePitchChanged(NoteViewModel note, int oldPitch)
        {
            NotePitchChanged?.Invoke(this, (note, oldPitch));
        }

        /// <summary>
        /// 触发音符音轨变化事件
        /// </summary>
        private void OnNoteTrackChanged(NoteViewModel note, int oldTrackIndex)
        {
            NoteTrackChanged?.Invoke(this, (note, oldTrackIndex));
        }

        /// <summary>
        /// 触发音符分割事件
        /// </summary>
        private void OnNoteSplit(NoteViewModel originalNote, NoteViewModel newNote)
        {
            NoteSplit?.Invoke(this, (originalNote, newNote));
        }

        /// <summary>
        /// 触发音符合并事件
        /// </summary>
        private void OnNotesMerged(NoteViewModel firstNote, NoteViewModel secondNote)
        {
            NotesMerged?.Invoke(this, (firstNote, secondNote));
        }
        #endregion
    }
}