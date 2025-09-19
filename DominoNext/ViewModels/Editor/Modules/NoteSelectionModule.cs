using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Lumino.ViewModels.Editor.State;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor.Modules.Base;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// ����ѡ����ģ��
    /// �ع���ʹ�û��࣬�����ظ�����
    /// </summary>
    public class NoteSelectionModule : EditorModuleBase
    {
        private readonly SelectionState _selectionState;

        public override string ModuleName => "NoteSelection";

        public NoteSelectionModule(SelectionState selectionState, ICoordinateService coordinateService) 
            : base(coordinateService)
        {
            _selectionState = selectionState;
        }

        /// <summary>
        /// ��ȡָ��λ�õ����� - �Ż��汾
        /// </summary>
        public NoteViewModel? GetNoteAtPosition(Point position, IEnumerable<NoteViewModel> notes, double timeToPixelScale, double keyHeight)
        {
            if (_pianoRollViewModel == null) return null;

            foreach (var note in notes)
            {
                // ʹ��֧�ֹ���ƫ����������ת������
                var noteRect = _coordinateService.GetNoteRect(note, timeToPixelScale, keyHeight, 
                    _pianoRollViewModel.CurrentScrollOffset, _pianoRollViewModel.VerticalScrollOffset);
                if (noteRect.Contains(position))
                {
                    return note;
                }
            }
            return null;
        }

        /// <summary>
        /// ��ʼѡ���
        /// </summary>
        public void StartSelection(Point startPoint)
        {
            _selectionState.StartSelection(startPoint);
            OnSelectionUpdated?.Invoke();
        }

        /// <summary>
        /// ����ѡ���
        /// </summary>
        public void UpdateSelection(Point currentPoint)
        {
            _selectionState.UpdateSelection(currentPoint);
            OnSelectionUpdated?.Invoke();
        }

        /// <summary>
        /// ���ѡ���
        /// </summary>
        public void EndSelection(IEnumerable<NoteViewModel> notes)
        {
            if (_selectionState.SelectionStart.HasValue && _selectionState.SelectionEnd.HasValue && _pianoRollViewModel != null)
            {
                var start = _selectionState.SelectionStart.Value;
                var end = _selectionState.SelectionEnd.Value;

                var x = System.Math.Min(start.X, end.X);
                var y = System.Math.Min(start.Y, end.Y);
                var width = System.Math.Abs(end.X - start.X);
                var height = System.Math.Abs(end.Y - start.Y);

                var selectionRect = new Rect(x, y, width, height);
                SelectNotesInArea(selectionRect, notes);
            }

            _selectionState.EndSelection();
            OnSelectionUpdated?.Invoke();
        }

        /// <summary>
        /// ѡ��������������
        /// </summary>
        public void SelectNotesInArea(Rect area, IEnumerable<NoteViewModel> notes)
        {
            if (_pianoRollViewModel == null) return;

            foreach (var note in notes)
            {
                // ʹ��֧�ֹ���ƫ����������ת������
                var noteRect = _coordinateService.GetNoteRect(note, 
                    _pianoRollViewModel.TimeToPixelScale, 
                    _pianoRollViewModel.KeyHeight,
                    _pianoRollViewModel.CurrentScrollOffset,
                    _pianoRollViewModel.VerticalScrollOffset);
                
                if (area.Intersects(noteRect))
                {
                    note.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// ���ѡ��
        /// </summary>
        public void ClearSelection(IEnumerable<NoteViewModel> notes)
        {
            foreach (var note in notes)
            {
                note.IsSelected = false;
            }
        }

        /// <summary>
        /// ѡ����������
        /// </summary>
        public void SelectAll(IEnumerable<NoteViewModel> notes)
        {
            foreach (var note in notes)
            {
                note.IsSelected = true;
            }
        }

        // �¼�
        public event Action? OnSelectionUpdated;

        // ֻ������
        public bool IsSelecting => _selectionState.IsSelecting;
        public Point? SelectionStart => _selectionState.SelectionStart;
        public Point? SelectionEnd => _selectionState.SelectionEnd;
    }
}

