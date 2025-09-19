using System;
using System.Linq;
using Avalonia;
using Lumino.ViewModels.Editor.State;
using Lumino.Services.Interfaces;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Modules.Base;
using Lumino.ViewModels.Editor.Services;
using System.Diagnostics;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// ������ק����ģ�� - ���ڷ�������ʵ��
    /// �ع���ʹ�û����ͨ�÷��񣬼����ظ�����
    /// </summary>
    public class NoteDragModule : EditorModuleBase
    {
        private readonly DragState _dragState;
        private readonly AntiShakeService _antiShakeService;

        public override string ModuleName => "NoteDrag";

        public NoteDragModule(DragState dragState, ICoordinateService coordinateService) 
            : base(coordinateService)
        {
            _dragState = dragState;
            // ʹ�ü���������ã�ֻ��������΢С���ƶ�
            _antiShakeService = new AntiShakeService(AntiShakeConfig.Minimal);
        }

        /// <summary>
        /// ��ʼ��ק����
        /// </summary>
        public void StartDrag(NoteViewModel note, Point startPosition)
        {
            if (_pianoRollViewModel == null) return;

            _dragState.StartDrag(note, startPosition);
            
            // ��ȡ����ѡ�е�����������ק
            _dragState.DraggingNotes = _pianoRollViewModel.Notes.Where(n => n.IsSelected).ToList();

            // ��¼���б���ק������ԭʼλ��
            _dragState.OriginalDragPositions.Clear();
            foreach (var dragNote in _dragState.DraggingNotes)
            {
                _dragState.OriginalDragPositions[dragNote] = (dragNote.StartPosition, dragNote.Pitch);
            }

            Debug.WriteLine($"��ʼ��ק {_dragState.DraggingNotes.Count} ������");
        }

        /// <summary>
        /// ������ק - ʹ��ͳһ�ķ�������
        /// </summary>
        public void UpdateDrag(Point currentPosition)
        {
            if (!_dragState.IsDragging || _pianoRollViewModel == null) return;

            // ʹ��ͳһ�ķ������
            if (_antiShakeService.ShouldIgnoreMovement(_dragState.DragStartPosition, currentPosition))
            {
                return; // ����΢С�ƶ�
            }

            var deltaX = currentPosition.X - _dragState.DragStartPosition.X;
            var deltaY = currentPosition.Y - _dragState.DragStartPosition.Y;

            // ����ʱ��ƫ�ƣ����ڷ�����
            var timeDelta = deltaX / _pianoRollViewModel.BaseQuarterNoteWidth; // ���ķ�����Ϊ��λ
            var pitchDelta = -(int)(deltaY / _pianoRollViewModel.KeyHeight);

            // ֱ�Ӹ������б���ק������
            foreach (var note in _dragState.DraggingNotes)
            {
                if (_dragState.OriginalDragPositions.TryGetValue(note, out var originalPos))
                {
                    var originalTimeValue = originalPos.OriginalStartPosition.ToDouble();
                    var newTimeValue = Math.Max(0, originalTimeValue + timeDelta);
                    var newPitch = EditorValidationService.ClampPitch(originalPos.OriginalPitch + pitchDelta);

                    // ת��Ϊ����������
                    var newTimeFraction = MusicalFraction.FromDouble(newTimeValue);
                    var quantizedPosition = _pianoRollViewModel.SnapToGrid(newTimeFraction);

                    // ֱ�Ӹ���
                    note.StartPosition = quantizedPosition;
                    note.Pitch = newPitch;
                    SafeInvalidateNoteCache(note);
                }
            }

            // ��������֪ͨ
            OnDragUpdated?.Invoke();
        }

        /// <summary>
        /// ������ק
        /// </summary>
        public void EndDrag()
        {
            if (_dragState.IsDragging)
            {
                Debug.WriteLine($"������ק {_dragState.DraggingNotes.Count} ������");
                
                // ��ק���������¼��������Χ����Ϊ����λ�ÿ����Ѿ��ı�
                _pianoRollViewModel?.UpdateMaxScrollExtent();
            }

            _dragState.EndDrag();
            OnDragEnded?.Invoke();
        }

        /// <summary>
        /// ȡ����ק���ָ�ԭʼλ��
        /// </summary>
        public void CancelDrag()
        {
            if (_dragState.IsDragging && _dragState.DraggingNotes.Count > 0)
            {
                foreach (var note in _dragState.DraggingNotes)
                {
                    if (_dragState.OriginalDragPositions.TryGetValue(note, out var originalPos))
                    {
                        note.StartPosition = originalPos.OriginalStartPosition;
                        note.Pitch = originalPos.OriginalPitch;
                        SafeInvalidateNoteCache(note);
                    }
                }
                Debug.WriteLine($"ȡ����ק���ָ� {_dragState.DraggingNotes.Count} ��������ԭʼλ��");
            }

            EndDrag();
        }

        // �¼�
        public event Action? OnDragUpdated;
        public event Action? OnDragEnded;

        // ֻ������
        public bool IsDragging => _dragState.IsDragging;
        public NoteViewModel? DraggingNote => _dragState.DraggingNote;
        public System.Collections.Generic.List<NoteViewModel> DraggingNotes => _dragState.DraggingNotes;
    }
}