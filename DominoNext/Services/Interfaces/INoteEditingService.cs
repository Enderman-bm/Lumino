using System.Threading.Tasks;
using System.Collections.Generic;
using Avalonia;
using DominoNext.ViewModels.Editor;

namespace DominoNext.Services.Interfaces
{
    public interface INoteEditingService
    {
        void CreateNoteAtPosition(Point position);
        void StartNoteDrag(NoteViewModel note, Point startPosition);
        void UpdateNoteDrag(Point currentPosition);
        void EndNoteDrag();
        void SelectNotesInArea(Rect area);
        void ClearSelection();
        void DeleteSelectedNotes();
        void DuplicateSelectedNotes();
        void QuantizeSelectedNotes();

        // 异步批量操作接口，提升大量音符处理性能
        Task QuantizeSelectedNotesAsync();
        Task DeleteSelectedNotesAsync();
        Task DuplicateSelectedNotesAsync();
        Task SelectNotesInAreaAsync(Rect area);
        Task ClearSelectionAsync();
        Task QuantizeNotesAsync(IEnumerable<NoteViewModel> notes);

        bool IsValidNotePosition(int pitch, double startTime);
    }
}