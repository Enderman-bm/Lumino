using System.Collections.Generic;
using Avalonia;
using Lumino.Models.Music;

namespace Lumino.ViewModels.Editor.State
{
    /// <summary>
    /// ������ק״̬����
    /// </summary>
    public class DragState
    {
        public bool IsDragging { get; set; }
        public NoteViewModel? DraggingNote { get; set; }
        public List<NoteViewModel> DraggingNotes { get; set; } = new();
        public Point DragStartPosition { get; set; }
        
        // ��¼ԭʼλ������ʵʱԤ����Լ��
        public Dictionary<NoteViewModel, (MusicalFraction OriginalStartPosition, int OriginalPitch)> 
            OriginalDragPositions { get; set; } = new();

        public void StartDrag(NoteViewModel note, Point startPosition)
        {
            IsDragging = true;
            DraggingNote = note;
            DragStartPosition = startPosition;
            
            if (!note.IsSelected)
            {
                note.IsSelected = true;
            }
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