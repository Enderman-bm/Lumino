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
    /// ����������С����ģ�� - ���ڷ�������ʵ��
    /// �ع���ʹ�û����ͨ�÷��񣬼����ظ�����
    /// </summary>
    public class NoteResizeModule : EditorModuleBase
    {
        private readonly ResizeState _resizeState;

        public override string ModuleName => "NoteResize";

        // ��ק��Ե�����ֵ
        private const double ResizeEdgeThreshold = 8.0;

        public NoteResizeModule(ResizeState resizeState, ICoordinateService coordinateService) 
            : base(coordinateService)
        {
            _resizeState = resizeState;
        }

        /// <summary>
        /// ���λ���Ƿ�ӽ������ı�Ե
        /// </summary>
        public ResizeHandle GetResizeHandleAtPosition(Point position, NoteViewModel note)
        {
            if (_pianoRollViewModel?.CurrentTool != EditorTool.Pencil) return ResizeHandle.None;

            // ʹ��֧�ֹ���ƫ����������ת������
            var noteRect = _coordinateService.GetNoteRect(note, 
                _pianoRollViewModel.TimeToPixelScale, 
                _pianoRollViewModel.KeyHeight,
                _pianoRollViewModel.CurrentScrollOffset,
                _pianoRollViewModel.VerticalScrollOffset);

            if (!noteRect.Contains(position)) return ResizeHandle.None;

            // ����Ƿ�ӽ���ʼ��Ե
            if (Math.Abs(position.X - noteRect.Left) <= ResizeEdgeThreshold)
            {
                return ResizeHandle.StartEdge;
            }

            // ����Ƿ�ӽ�������Ե
            if (Math.Abs(position.X - noteRect.Right) <= ResizeEdgeThreshold)
            {
                return ResizeHandle.EndEdge;
            }

            return ResizeHandle.None;
        }

        /// <summary>
        /// ��ʼ������С
        /// </summary>
        public void StartResize(Point position, NoteViewModel note, ResizeHandle handle)
        {
            if (handle == ResizeHandle.None || _pianoRollViewModel == null) return;

            _resizeState.StartResize(note, handle);

            // ��ȡ����ѡ�е�������������ǰ����
            _resizeState.ResizingNotes = _pianoRollViewModel.Notes.Where(n => n.IsSelected).ToList();
            if (!_resizeState.ResizingNotes.Contains(note))
            {
                _resizeState.ResizingNotes.Add(note);
            }

            // ��¼ԭʼ������λ��
            _resizeState.OriginalDurations.Clear();
            foreach (var n in _resizeState.ResizingNotes)
            {
                _resizeState.OriginalDurations[n] = n.Duration;
                n.PropertyChanged += OnResizingNotePropertyChanged;
            }

            Debug.WriteLine($"��ʼ����������С: Handle={handle}, ѡ��������={_resizeState.ResizingNotes.Count}");
            OnResizeStarted?.Invoke();
        }

        /// <summary>
        /// ���µ�����С - ʹ�û����ͨ�÷���
        /// </summary>
        public void UpdateResize(Point currentPosition)
        {
            if (!_resizeState.IsResizing || _resizeState.ResizingNote == null || 
                _resizeState.ResizingNotes.Count == 0 || _pianoRollViewModel == null) return;

            try
            {
                // ʹ�û����ͨ������ת������
                var currentTimeValue = GetTimeFromPosition(currentPosition);
                bool anyNoteChanged = false;

                foreach (var note in _resizeState.ResizingNotes)
                {
                    var startValue = note.StartPosition.ToDouble();
                    var endValue = startValue + note.Duration.ToDouble();
                    var originalDuration = _resizeState.OriginalDurations[note];

                    MusicalFraction newDuration;
                    MusicalFraction newStartPosition = note.StartPosition;

                    if (_resizeState.CurrentResizeHandle == ResizeHandle.StartEdge)
                    {
                        // ������ʼλ��
                        var newStartValue = Math.Min(currentTimeValue, endValue - _pianoRollViewModel.GridQuantization.ToDouble());
                        var newStartFraction = MusicalFraction.FromDouble(newStartValue);
                        var quantizedStart = _pianoRollViewModel.SnapToGrid(newStartFraction);

                        var endFraction = MusicalFraction.FromDouble(endValue);
                        newDuration = endFraction - quantizedStart;
                        newStartPosition = quantizedStart;
                    }
                    else // EndEdge
                    {
                        // ��������λ��
                        var newEndValue = Math.Max(currentTimeValue, startValue + _pianoRollViewModel.GridQuantization.ToDouble());
                        var newEndFraction = MusicalFraction.FromDouble(newEndValue);
                        var quantizedEnd = _pianoRollViewModel.SnapToGrid(newEndFraction);

                        var startFraction = note.StartPosition;
                        newDuration = quantizedEnd - startFraction;
                    }

                    // Ӧ����С����Լ�� - ʹ����֤����
                    var minDuration = _pianoRollViewModel.GridQuantization;
                    if (!EditorValidationService.IsValidDuration(originalDuration, minDuration))
                    {
                        newDuration = originalDuration;
                    }
                    else
                    {
                        if (!EditorValidationService.IsValidDuration(newDuration, minDuration))
                        {
                            newDuration = minDuration;
                        }
                    }

                    // ֻ�ڳ��Ȼ�λ�÷����ı�ʱ����
                    bool durationChanged = !note.Duration.Equals(newDuration);
                    bool positionChanged = _resizeState.CurrentResizeHandle == ResizeHandle.StartEdge && !note.StartPosition.Equals(newStartPosition);

                    if (durationChanged || positionChanged)
                    {
                        if (positionChanged) note.StartPosition = newStartPosition;
                        if (durationChanged) note.Duration = newDuration;

                        SafeInvalidateNoteCache(note);
                        anyNoteChanged = true;
                    }
                }

                if (anyNoteChanged)
                {
                    OnResizeUpdated?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"����������Сʱ����: {ex.Message}");
            }
        }

        /// <summary>
        /// ��ɵ�����С
        /// </summary>
        public void EndResize()
        {
            if (_resizeState.IsResizing && _resizeState.ResizingNote != null && _pianoRollViewModel != null)
            {
                // �����û��Զ���ʱֵ - ͨ��Configuration����
                _pianoRollViewModel.Configuration.UserDefinedNoteDuration = _resizeState.ResizingNote.Duration;
                Debug.WriteLine($"��ɵ�����С�������û��Զ���ʱֵ: {_pianoRollViewModel.Configuration.UserDefinedNoteDuration}");
                
                // ������С���������¼��������Χ����Ϊ�����ĳ��Ȼ�λ�ÿ����Ѿ��ı�
                _pianoRollViewModel.UpdateMaxScrollExtent();
            }

            // ȡ�����Ա仯����
            foreach (var note in _resizeState.ResizingNotes)
            {
                note.PropertyChanged -= OnResizingNotePropertyChanged;
            }

            _resizeState.EndResize();
            OnResizeEnded?.Invoke();
        }

        /// <summary>
        /// ȡ��������С
        /// </summary>
        public void CancelResize()
        {
            if (_resizeState.IsResizing && _resizeState.ResizingNotes.Count > 0)
            {
                // �ָ�ԭʼ����
                foreach (var note in _resizeState.ResizingNotes)
                {
                    if (_resizeState.OriginalDurations.TryGetValue(note, out var originalDuration))
                    {
                        note.Duration = originalDuration;
                        SafeInvalidateNoteCache(note);
                    }
                    note.PropertyChanged -= OnResizingNotePropertyChanged;
                }

                Debug.WriteLine($"ȡ�������������ȣ��ָ� {_resizeState.ResizingNotes.Count} ��������ԭʼ����");
            }

            _resizeState.EndResize();
            OnResizeEnded?.Invoke();
        }

        private void OnResizingNotePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NoteViewModel.Duration) || e.PropertyName == nameof(NoteViewModel.StartPosition))
            {
                OnResizeUpdated?.Invoke();
            }
        }

        // �¼�
        public event Action? OnResizeStarted;
        public event Action? OnResizeUpdated;
        public event Action? OnResizeEnded;

        // ֻ������
        public bool IsResizing => _resizeState.IsResizing;
        public ResizeHandle CurrentResizeHandle => _resizeState.CurrentResizeHandle;
        public NoteViewModel? ResizingNote => _resizeState.ResizingNote;
        public System.Collections.Generic.List<NoteViewModel> ResizingNotes => _resizeState.ResizingNotes;
    }
}

