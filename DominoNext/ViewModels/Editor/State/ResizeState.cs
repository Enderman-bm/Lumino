using System.Collections.Generic;
using Avalonia;
using Lumino.Models.Music;

namespace Lumino.ViewModels.Editor.State
{
    /// <summary>
    /// ����������С״̬����
    /// </summary>
    public class ResizeState
    {
        public bool IsResizing { get; set; }
        public ResizeHandle CurrentResizeHandle { get; set; } = ResizeHandle.None;
        public NoteViewModel? ResizingNote { get; set; }
        public List<NoteViewModel> ResizingNotes { get; set; } = new();
        
        // ��¼ԭʼ��������Լ��
        public Dictionary<NoteViewModel, MusicalFraction> OriginalDurations { get; set; } = new();

        public void StartResize(NoteViewModel note, ResizeHandle handle)
        {
            IsResizing = true;
            ResizingNote = note;
            CurrentResizeHandle = handle;
        }

        public void EndResize()
        {
            IsResizing = false;
            CurrentResizeHandle = ResizeHandle.None;
            ResizingNote = null;
            ResizingNotes.Clear();
            OriginalDurations.Clear();
        }

        public void Reset()
        {
            EndResize();
        }
    }

    /// <summary>
    /// ������С�ֱ�����
    /// </summary>
    public enum ResizeHandle
    {
        None,
        StartEdge,
        EndEdge
    }
}