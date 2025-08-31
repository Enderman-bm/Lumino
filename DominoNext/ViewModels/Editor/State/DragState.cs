using System.Collections.Generic;
using Avalonia;
using DominoNext.Models.Music;

namespace DominoNext.ViewModels.Editor.State
{
    /// <summary>
    /// 音符拖拽状态管理
    /// </summary>
    public class DragState
    {
        public bool IsDragging { get; private set; }
        public NoteViewModel? DraggingNote { get; private set; }
        public List<NoteViewModel> DraggingNotes { get; } = new();
        public Point DragStartPosition { get; private set; }
        
        // 记录原始位置用于实时预览与约束
        public Dictionary<NoteViewModel, (MusicalFraction OriginalStartPosition, int OriginalPitch)> 
            OriginalDragPositions { get; } = new();

        public void StartDrag(NoteViewModel note, Point startPosition)
        {
            IsDragging = true;
            DraggingNote = note;
            DragStartPosition = startPosition;
        }

        public void EndDrag()
        {
            IsDragging = false;
            DraggingNote = null;
            DraggingNotes.Clear();
            OriginalDragPositions.Clear();
        }

        public void Reset()
        {
            EndDrag();
        }
    }
}