using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor.State;
using Lumino.Views.Rendering.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// ���ȱ༭ģ�� - ���ڷ�������ʵ��
    /// </summary>
    public class VelocityEditingModule
    {
        private readonly ICoordinateService _coordinateService;
        private readonly VelocityEditingState _state;
        private PianoRollViewModel? _pianoRollViewModel;

        // ���ظ�ģʽ���Ѵ�����������¼
        private HashSet<NoteViewModel> _processedNotes = new();

        private double _canvasHeight = 100.0; // �����߶�

        public event Action? OnVelocityUpdated;

        public VelocityEditingModule(ICoordinateService coordinateService)
        {
            _coordinateService = coordinateService;
            _state = new VelocityEditingState();
        }

        public void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        /// <summary>
        /// ���û����߶� - ��VelocityViewCanvas����
        /// </summary>
        public void SetCanvasHeight(double height)
        {
            _canvasHeight = height;
        }

        #region ��������

        public bool IsEditingVelocity => _state.IsEditing;
        public List<NoteViewModel>? EditingNotes => _state.EditingNotes;
        public List<Point> EditingPath => _state.EditingPath;
        public Point? CurrentEditPosition => _state.CurrentPosition;

        #endregion

        #region ���ȱ༭����

        /// <summary>
        /// ��ʼ���ȱ༭
        /// </summary>
        public void StartEditing(Point position)
        {
            if (_pianoRollViewModel == null) return;

            _state.StartEditing(position);
            _processedNotes.Clear(); // ����Ѵ���������¼

            // ���ݵ�ǰ����ģʽȷ���༭Ŀ��
            switch (_pianoRollViewModel.CurrentTool)
            {
                case EditorTool.Select:
                    // ѡ�񹤾ߣ��༭ѡ�е�����
                    StartSelectModeEditing(position);
                    break;
                    
                case EditorTool.Pencil:
                    // Ǧ�ʹ��ߣ�����ģʽ�༭
                    StartPencilModeEditing(position);
                    break;
            }

            OnVelocityUpdated?.Invoke();
        }

        /// <summary>
        /// �������ȱ༭
        /// </summary>
        public void UpdateEditing(Point position)
        {
            if (!_state.IsEditing || _pianoRollViewModel == null) return;

            _state.UpdatePosition(position);

            switch (_pianoRollViewModel.CurrentTool)
            {
                case EditorTool.Select:
                    UpdateSelectModeEditing(position);
                    break;
                    
                case EditorTool.Pencil:
                    UpdatePencilModeEditing(position);
                    break;
            }

            OnVelocityUpdated?.Invoke();
        }

        /// <summary>
        /// �������ȱ༭
        /// </summary>
        public void EndEditing()
        {
            if (!_state.IsEditing) return;

            // Ӧ�����յ����ȸ���
            ApplyVelocityChanges();
            
            _state.EndEditing();
            _processedNotes.Clear(); // ����Ѵ���������¼
            OnVelocityUpdated?.Invoke();
        }

        /// <summary>
        /// ȡ�����ȱ༭
        /// </summary>
        public void CancelEditing()
        {
            if (!_state.IsEditing) return;

            // �ָ�ԭʼ����ֵ
            RestoreOriginalVelocities();
            
            _state.EndEditing();
            _processedNotes.Clear(); // ����Ѵ���������¼
            OnVelocityUpdated?.Invoke();
        }

        #endregion

        #region ѡ�񹤾�ģʽ

        private void StartSelectModeEditing(Point position)
        {
            if (_pianoRollViewModel == null) return;

            // ��ȡѡ�е�����
            var selectedNotes = _pianoRollViewModel.Notes.Where(n => n.IsSelected).ToList();
            
            if (!selectedNotes.Any())
            {
                // ���û��ѡ����������ѡ����λ�õ�����
                var clickedNote = FindNoteAtPosition(position);
                if (clickedNote != null)
                {
                    selectedNotes.Add(clickedNote);
                    clickedNote.IsSelected = true;
                }
            }

            if (selectedNotes.Any())
            {
                _state.SetEditingNotes(selectedNotes);
                _state.SaveOriginalVelocities(selectedNotes);
            }
        }

        private void UpdateSelectModeEditing(Point position)
        {
            if (_state.EditingNotes?.Any() != true) return;

            // �������ȱ仯��
            var deltaY = position.Y - _state.StartPosition.Y;
            var velocityChange = CalculateVelocityChange(deltaY);

            // Ӧ�����ȱ仯�����б༭�е�����
            foreach (var note in _state.EditingNotes)
            {
                if (_state.OriginalVelocities.TryGetValue(note, out var originalVelocity))
                {
                    var newVelocity = Math.Max(1, Math.Min(127, originalVelocity + velocityChange));
                    note.Velocity = newVelocity;
                }
            }
        }

        #endregion

        #region Ǧ�ʹ���ģʽ - ���ڷ�������ʵ��

        private void StartPencilModeEditing(Point position)
        {
            if (_pianoRollViewModel == null) return;
            
            // ����Ѵ���������¼
            _processedNotes.Clear();
            
            // ������ǰλ�õ�����
            ProcessNotesAtPositionSimple(position);
            _state.AddToPath(position);
        }

        private void UpdatePencilModeEditing(Point position)
        {
            if (_pianoRollViewModel == null) return;

            // ��ȡ��һ��λ��
            Point? lastPosition = null;
            if (_state.EditingPath.Count > 0)
            {
                lastPosition = _state.EditingPath[^1];
            }

            _state.AddToPath(position);

            // �������һ��λ�ã����в�ֵ����
            if (lastPosition.HasValue)
            {
                ProcessPathBetweenPoints(lastPosition.Value, position);
            }
            else
            {
                // ���û����һ��λ�ã�ֱ�Ӵ�����ǰλ��
                ProcessNotesAtPositionSimple(position);
            }
        }

        /// <summary>
        /// ��������֮����в�ֵ������ȷ���켣����
        /// </summary>
        private void ProcessPathBetweenPoints(Point startPoint, Point endPoint)
        {
            // ��������֮��ľ���
            var deltaX = endPoint.X - startPoint.X;
            var deltaY = endPoint.Y - startPoint.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // ����̫С��ֱ�Ӵ����յ�
            if (distance < 2.0)
            {
                ProcessNotesAtPositionSimple(endPoint);
                return;
            }

            // ���ݾ���ȷ����ֵ������ÿ2����һ���㣩
            var steps = Math.Max(1, (int)Math.Ceiling(distance / 2.0));
            
            // ���в�ֵ
            for (int i = 0; i <= steps; i++)
            {
                var t = (double)i / steps;
                var interpolatedPoint = new Point(
                    startPoint.X + deltaX * t,
                    startPoint.Y + deltaY * t
                );

                ProcessNotesAtPositionSimple(interpolatedPoint);
            }
        }

        /// <summary>
        /// �򻯰洦���㷨 - ���ڷ�������ʵ��
        /// </summary>
        private void ProcessNotesAtPositionSimple(Point position)
        {
            if (_pianoRollViewModel == null) return;

            // ���㵱ǰλ�ö�Ӧ�ľ�������ֵ
            var velocity = CalculateVelocityFromY(position.Y);
            var timeValue = _pianoRollViewModel.GetTimeFromX(position.X);
            
            // �����ڵ�ǰʱ��λ�ø��ǵ���������
            foreach (var note in _pianoRollViewModel.Notes)
            {
                var noteStartValue = note.StartPosition.ToDouble();
                var noteEndValue = noteStartValue + note.Duration.ToDouble();
                
                // ���ʱ���Ƿ���������Χ��
                if (timeValue >= noteStartValue && timeValue <= noteEndValue)
                {
                    // ����Ƿ�������ͷ����ǰ25%��ʱ�䷶Χ�ڣ�
                    var noteDuration = noteEndValue - noteStartValue;
                    var startThreshold = noteDuration * 0.25; // ����ͷ��25%��ʱ�䷶Χ
                    
                    if (timeValue <= noteStartValue + startThreshold)
                    {
                        // ����������Ψһ��ʶ��������λ�ú����ߣ�
                        var noteId = $"{noteStartValue}_{note.Pitch}";
                        
                        // ����Ƿ��Ѿ���������ʹ�ø���ȷ�ı�ʶ����
                        if (!_processedNotes.Any(n => $"{n.StartPosition.ToDouble()}_{n.Pitch}" == noteId))
                        {
                            // ����ԭʼ���ȣ������û�еĻ�
                            if (_state.EditingNotes?.Contains(note) != true)
                            {
                                _state.AddEditingNote(note);
                                _state.SaveOriginalVelocity(note);
                            }
                            
                            // ֱ����������ֵ
                            note.Velocity = velocity;
                            _processedNotes.Add(note);
                        }
                    }
                }
            }
        }

        #endregion

        #region ��������

        private NoteViewModel? FindNoteAtPosition(Point position)
        {
            if (_pianoRollViewModel == null) return null;

            return _pianoRollViewModel.GetNoteAtPosition(position);
        }

        private int CalculateVelocityChange(double deltaY)
        {
            // ����ֱ�仯ת��Ϊ���ȱ仯
            // ����100���ظ߶ȶ�Ӧ127������ֵ
            return (int)Math.Round(-deltaY * 127.0 / 100.0);
        }

        private int CalculateVelocityFromY(double y)
        {
            // ʹ��VelocityBarRenderer�Ĺ�ʽ�������������ֵ
            // ʹ��ʵ�ʵĻ����߶�
            return VelocityBarRenderer.CalculateVelocityFromY(y, _canvasHeight);
        }

        private void ApplyVelocityChanges()
        {
            // Ԥ�����ڳ���/����֧��
            // ��ǰʵ��ֱ��Ӧ�ø���
        }

        private void RestoreOriginalVelocities()
        {
            if (_state.EditingNotes == null) return;

            foreach (var note in _state.EditingNotes)
            {
                if (_state.OriginalVelocities.TryGetValue(note, out var originalVelocity))
                {
                    note.Velocity = originalVelocity;
                }
            }
        }

        #endregion
    }}