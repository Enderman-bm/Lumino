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
    /// PianoRollViewModel的选择操作功能
    /// </summary>
    public partial class PianoRollViewModel
    {
        #region 选择命令
        /// <summary>
        /// 选择音符命令
        /// </summary>
        [RelayCommand]
        private void SelectNote(NoteViewModel note)
        {
            // TODO: 实现选择音符功能
            // SelectionModule.SelectNote(note);
            if (note != null)
            {
                note.IsSelected = true;
            }
        }

        /// <summary>
        /// 取消选择音符命令
        /// </summary>
        [RelayCommand]
        private void DeselectNote(NoteViewModel note)
        {
            // TODO: 实现取消选择音符功能
            // SelectionModule.DeselectNote(note);
            if (note != null)
            {
                note.IsSelected = false;
            }
        }

        /// <summary>
        /// 切换音符选择状态命令
        /// </summary>
        [RelayCommand]
        private void ToggleNoteSelection(NoteViewModel note)
        {
            // TODO: 实现切换音符选择状态
            // SelectionModule.ToggleNoteSelection(note);
            if (note != null)
            {
                note.IsSelected = !note.IsSelected;
            }
        }

        /// <summary>
        /// 选择所有音符命令
        /// </summary>
        [RelayCommand]
        private void SelectAllNotes()
        {
            // TODO: 实现选择所有音符
            // SelectionModule.SelectAll(CurrentTrackNotes);
            foreach (var note in CurrentTrackNotes)
            {
                note.IsSelected = true;
            }
        }

        /// <summary>
        /// 取消选择所有音符命令
        /// </summary>
        [RelayCommand]
        private void DeselectAllNotes()
        {
            // TODO: 实现取消选择所有音符
            // SelectionModule.DeselectAll();
            foreach (var note in CurrentTrackNotes)
            {
                note.IsSelected = false;
            }
        }

        /// <summary>
        /// 反向选择命令
        /// </summary>
        [RelayCommand]
        private void InvertSelection()
        {
            // TODO: 实现反向选择功能
            // SelectionModule.InvertSelection(CurrentTrackNotes);
            var selectedNotes = CurrentTrackNotes.Where(n => n.IsSelected).ToList();
            var unselectedNotes = CurrentTrackNotes.Where(n => !n.IsSelected).ToList();
            
            foreach (var note in selectedNotes)
            {
                note.IsSelected = false;
            }
            
            foreach (var note in unselectedNotes)
            {
                note.IsSelected = true;
            }
        }

        /// <summary>
        /// 选择前一个音符命令
        /// </summary>
        [RelayCommand]
        private void SelectPreviousNote()
        {
            // TODO: 实现选择前一个音符功能
            // SelectionModule.SelectPreviousNote(CurrentTrackNotes);
            var currentTime = CurrentPlaybackTime;
            var previousNote = CurrentTrackNotes
                .Where(n => n.StartPosition.ToDouble() < currentTime)
                .OrderByDescending(n => n.StartPosition.ToDouble())
                .FirstOrDefault();
                
            if (previousNote != null)
            {
                DeselectAllNotes();
                previousNote.IsSelected = true;
            }
        }

        /// <summary>
        /// 选择后一个音符命令
        /// </summary>
        [RelayCommand]
        private void SelectNextNote()
        {
            // TODO: 实现选择后一个音符功能
            // SelectionModule.SelectNextNote(CurrentTrackNotes);
            var currentTime = CurrentPlaybackTime;
            var nextNote = CurrentTrackNotes
                .Where(n => n.StartPosition.ToDouble() > currentTime)
                .OrderBy(n => n.StartPosition.ToDouble())
                .FirstOrDefault();
                
            if (nextNote != null)
            {
                DeselectAllNotes();
                nextNote.IsSelected = true;
            }
        }
        #endregion

        #region 区域选择
        /// <summary>
        /// 开始区域选择
        /// </summary>
        public void StartRegionSelection(Point startPoint)
        {
            SelectionModule.StartSelection(startPoint);
        }

        /// <summary>
        /// 更新区域选择
        /// </summary>
        public void UpdateRegionSelection(Point currentPoint)
        {
            SelectionModule.UpdateSelection(currentPoint);
        }

        /// <summary>
        /// 结束区域选择
        /// </summary>
        public void EndRegionSelection()
        {
            SelectionModule.EndSelection(CurrentTrackNotes);
        }

        /// <summary>
        /// 取消区域选择
        /// </summary>
        public void CancelRegionSelection()
        {
            SelectionModule.ClearSelection(CurrentTrackNotes);
        }
        #endregion

        #region 选择操作
        /// <summary>
        /// 复制选中音符
        /// </summary>
        public void CopySelectedNotes()
        {
            if (!HasSelectedNotes)
                return;
                
            var clipboardData = new NoteClipboardData
            {
                Notes = SelectedNotes.Select(n => new NoteClipboardItem
                {
                    Pitch = n.Pitch,
                    StartPosition = n.StartPosition,
                    Duration = n.Duration,
                    Velocity = n.Velocity
                }).ToList(),
                SourceTrackIndex = CurrentTrackIndex
            };
            
            // TODO: 实现剪贴板服务
            // ClipboardService.CopyNotes(clipboardData);
        }

        /// <summary>
        /// 剪切选中音符
        /// </summary>
        public void CutSelectedNotes()
        {
            if (!HasSelectedNotes)
                return;
                
            CopySelectedNotes();
            DeleteSelectedNotes();
        }

        /// <summary>
        /// 粘贴音符
        /// </summary>
        public void PasteNotes(MusicalFraction? pastePosition = null)
        {
            // TODO: 实现剪贴板服务
            // var clipboardData = ClipboardService.GetNotes();
            // if (clipboardData == null || !clipboardData.Notes.Any())
            //     return;
            
            // 临时返回，等待剪贴板服务实现
            return;
                
            // var targetPosition = pastePosition ?? GetPastePosition(clipboardData);
            // var firstNoteTime = clipboardData.Notes.Min(n => n.StartPosition);
            // var timeOffset = targetPosition - firstNoteTime;
            
            // using (BeginBatchOperation())
            // {
            //     // 取消当前选择
            //     DeselectAllNotes();
            //     
            //     // 创建粘贴的音符
            //     foreach (var noteData in clipboardData.Notes)
            //     {
            //         var newNote = new NoteViewModel
            //         {
            //             Pitch = noteData.Pitch,
            //             StartPosition = noteData.StartPosition + timeOffset,
            //             Duration = noteData.Duration,
            //             Velocity = noteData.Velocity,
            //             TrackIndex = CurrentTrackIndex,
            //             IsSelected = true
            //         };
            //         
            //         AddNoteWithUndo(newNote);
            //     }
            // }
        }

        // 删除选中音符 - 现在由 RelayCommand 自动生成
        // public void DeleteSelectedNotes()

        /// <summary>
        /// 复制选中音符到新的音轨
        /// </summary>
        public void DuplicateSelectedNotesToNewTrack()
        {
            if (!HasSelectedNotes)
                return;
                
            // 创建新音轨
            AddTrackWithUndo($"{CurrentTrackName} (Copy)");
            var newTrackIndex = Tracks.Count - 1;
            
            // 复制音符到新音轨
            var selectedNotes = SelectedNotes.ToList();
            using (BeginBatchOperation())
            {
                foreach (var note in selectedNotes)
                {
                    var newNote = new NoteViewModel
                    {
                        Pitch = note.Pitch,
                        StartPosition = note.StartPosition,
                        Duration = note.Duration,
                        Velocity = note.Velocity,
                        TrackIndex = newTrackIndex,
                        IsSelected = true
                    };
                    
                    AddNoteWithUndo(newNote);
                }
            }
            
            // 切换到新音轨
            CurrentTrackIndex = newTrackIndex;
        }

        /// <summary>
        /// 移动选中音符到其他音轨
        /// </summary>
        public void MoveSelectedNotesToTrack(int targetTrackIndex)
        {
            if (!HasSelectedNotes || targetTrackIndex < 0 || targetTrackIndex >= Tracks.Count)
                return;
                
            if (targetTrackIndex == CurrentTrackIndex)
                return;
                
            var selectedNotes = SelectedNotes.ToList();
            using (BeginBatchOperation())
            {
                foreach (var note in selectedNotes)
                {
                    // 创建移动音符命令
                    var oldTrackIndex = note.TrackIndex;
                    note.TrackIndex = targetTrackIndex;
                    
                    // 这里可以添加专门的移动音符到音轨的撤销重做命令
                    // 为了简化，这里直接修改，实际项目中应该使用专门的命令
                }
            }
            
            // 更新当前音轨音符
            UpdateCurrentTrackNotes();
            
            // 切换到目标音轨
            CurrentTrackIndex = targetTrackIndex;
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取粘贴位置
        /// </summary>
        private MusicalFraction GetPastePosition(NoteClipboardData clipboardData)
        {
            // 如果有选中音符，粘贴到选中音符之后
            if (HasSelectedNotes)
            {
                var lastSelectedNote = SelectedNotes.OrderBy(n => n.StartPosition + n.Duration).Last();
                return lastSelectedNote.StartPosition + lastSelectedNote.Duration;
            }
            
            // 如果有播放头位置，粘贴到播放头位置
            // if (PlaybackService != null && PlaybackService.IsPlaying)
            // {
            //     return MusicalFraction.FromDouble(PlaybackService.CurrentTime);
            // }
            
            // 否则粘贴到剪贴板音符的原始位置
            var firstNoteTime = clipboardData.Notes.Min(n => n.StartPosition);
            return firstNoteTime;
        }

        /// <summary>
        /// 选择时间范围内的音符
        /// </summary>
        public void SelectNotesInTimeRange(MusicalFraction startTime, MusicalFraction endTime)
        {
            var notesInRange = CurrentTrackNotes.Where(n => 
                n.StartPosition < endTime && (n.StartPosition + n.Duration) > startTime).ToList();
                
            using (BeginBatchOperation())
            {
                DeselectAllNotes();
                foreach (var note in notesInRange)
                {
                    note.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// 选择音高范围内的音符
        /// </summary>
        public void SelectNotesInPitchRange(int minPitch, int maxPitch)
        {
            var notesInRange = CurrentTrackNotes.Where(n => n.Pitch >= minPitch && n.Pitch <= maxPitch).ToList();
                
            using (BeginBatchOperation())
            {
                DeselectAllNotes();
                foreach (var note in notesInRange)
                {
                    note.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// 选择相似音符
        /// </summary>
        public void SelectSimilarNotes(NoteViewModel referenceNote)
        {
            var similarNotes = CurrentTrackNotes.Where(n => 
                n.Pitch == referenceNote.Pitch && n != referenceNote).ToList();
                
            foreach (var note in similarNotes)
            {
                note.IsSelected = true;
            }
        }

        /// <summary>
        /// 选择相同长度的音符
        /// </summary>
        public void SelectNotesWithSameDuration(NoteViewModel referenceNote)
        {
            var sameDurationNotes = CurrentTrackNotes.Where(n => 
                n.Duration == referenceNote.Duration && n != referenceNote).ToList();
                
            foreach (var note in sameDurationNotes)
            {
                note.IsSelected = true;
            }
        }

        /// <summary>
        /// 选择相同力度的音符
        /// </summary>
        public void SelectNotesWithSameVelocity(NoteViewModel referenceNote)
        {
            var sameVelocityNotes = CurrentTrackNotes.Where(n => 
                n.Velocity == referenceNote.Velocity && n != referenceNote).ToList();
                
            foreach (var note in sameVelocityNotes)
            {
                note.IsSelected = true;
            }
        }

        /// <summary>
        /// 根据条件选择音符
        /// </summary>
        public void SelectNotesByCondition(Func<NoteViewModel, bool> condition)
        {
            var matchingNotes = CurrentTrackNotes.Where(condition).ToList();
                
            using (BeginBatchOperation())
            {
                DeselectAllNotes();
                foreach (var note in matchingNotes)
                {
                    note.IsSelected = true;
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// 音符剪贴板数据
    /// </summary>
    public class NoteClipboardData
    {
        public List<NoteClipboardItem> Notes { get; set; } = new List<NoteClipboardItem>();
        public int SourceTrackIndex { get; set; }
    }

    /// <summary>
    /// 音符剪贴板项
    /// </summary>
    public class NoteClipboardItem
    {
        public int Pitch { get; set; }
        public MusicalFraction StartPosition { get; set; }
        public MusicalFraction Duration { get; set; }
        public int Velocity { get; set; }
    }
}