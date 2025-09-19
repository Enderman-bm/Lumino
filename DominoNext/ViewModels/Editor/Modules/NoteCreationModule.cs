using System;
using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.Models.Music;
using Lumino.ViewModels.Editor.Modules.Base;
using Lumino.ViewModels.Editor.Services;
using System.Diagnostics;

namespace Lumino.ViewModels.Editor.Modules
{
    /// <summary>
    /// ������������ģ�� - ���ڷ�������ʵ��
    /// �ع���ʹ�û����ͨ�÷��񣬼����ظ�����
    /// </summary>
    public class NoteCreationModule : EditorModuleBase
    {
        private readonly AntiShakeService _antiShakeService;

        public override string ModuleName => "NoteCreation";

        // ����״̬
        public bool IsCreatingNote { get; private set; }
        public NoteViewModel? CreatingNote { get; private set; }
        public Point CreatingStartPosition { get; private set; }
        
        // �򻯷�������ֻ���ʱ���ж�
        private DateTime _creationStartTime;

        public NoteCreationModule(ICoordinateService coordinateService) : base(coordinateService)
        {
            // ʹ��ʱ��������ã��ʺ����������Ķ̰�/��������
            _antiShakeService = new AntiShakeService(new AntiShakeConfig
            {
                PixelThreshold = 2.0,
                TimeThresholdMs = 100.0,
                EnablePixelAntiShake = false, // ����������Ҫ����ʱ�����
                EnableTimeAntiShake = true
            });
        }

        /// <summary>
        /// ��ʼ�������� - ʹ�û����ͨ�÷���
        /// </summary>
        public void StartCreating(Point position)
        {
            if (_pianoRollViewModel == null) return;

            var pitch = GetPitchFromPosition(position);
            var timeValue = GetTimeFromPosition(position);

            Debug.WriteLine("=== StartCreatingNote ===");

            if (EditorValidationService.IsValidNotePosition(pitch, timeValue))
            {
                // ʹ�û����ͨ����������
                var quantizedPosition = GetQuantizedTimeFromPosition(position);

                CreatingNote = new NoteViewModel
                {
                    Pitch = pitch,
                    StartPosition = quantizedPosition,
                    Duration = _pianoRollViewModel.UserDefinedNoteDuration,
                    Velocity = 100,
                    IsPreview = true
                };

                CreatingStartPosition = position;
                IsCreatingNote = true;
                _creationStartTime = DateTime.Now;

                Debug.WriteLine($"��ʼ��������: Pitch={pitch}, Duration={CreatingNote.Duration}");
                OnCreationStarted?.Invoke();
            }
        }

        /// <summary>
        /// ���´����е��������� - ���ڷ�������ʵ��
        /// </summary>
        public void UpdateCreating(Point currentPosition)
        {
            if (!IsCreatingNote || CreatingNote == null || _pianoRollViewModel == null) return;

            var currentTimeValue = GetTimeFromPosition(currentPosition);
            var startValue = CreatingNote.StartPosition.ToDouble();

            // ���������ĳ���
            var minDuration = _pianoRollViewModel.GridQuantization.ToDouble();
            var actualDuration = Math.Max(minDuration, currentTimeValue - startValue);

            if (actualDuration > 0)
            {
                var startFraction = CreatingNote.StartPosition;
                var endValue = startValue + actualDuration;
                var endFraction = MusicalFraction.FromDouble(endValue);
                
                var duration = MusicalFraction.CalculateQuantizedDuration(startFraction, endFraction, _pianoRollViewModel.GridQuantization);

                // ֻ�ڳ��ȷ����ı�ʱ����
                if (!CreatingNote.Duration.Equals(duration))
                {
                    Debug.WriteLine($"ʵʱ������������: {CreatingNote.Duration} -> {duration}");
                    CreatingNote.Duration = duration;
                    SafeInvalidateNoteCache(CreatingNote);

                    OnCreationUpdated?.Invoke();
                }
            }
        }

        /// <summary>
        /// ��ɴ������� - ʹ��ͳһ�ķ�������
        /// </summary>
        public void FinishCreating()
        {
            if (IsCreatingNote && CreatingNote != null && _pianoRollViewModel != null)
            {
                MusicalFraction finalDuration;

                // ʹ�÷��������ж�
                if (_antiShakeService.IsShortPress(_creationStartTime))
                {
                    // �̰���ʹ���û�Ԥ����ʱֵ
                    finalDuration = _pianoRollViewModel.UserDefinedNoteDuration;
                    Debug.WriteLine($"�̰�����������ʹ��Ԥ��ʱֵ: {finalDuration}");
                }
                else
                {
                    // ������ʹ����ק�ĳ���
                    finalDuration = CreatingNote.Duration;
                    Debug.WriteLine($"��������������ʹ����קʱֵ: {finalDuration}");
                }

                // ������������
                var finalNote = new NoteViewModel
                {
                    Pitch = CreatingNote.Pitch,
                    StartPosition = CreatingNote.StartPosition,
                    Duration = finalDuration,
                    Velocity = CreatingNote.Velocity,
                    TrackIndex = _pianoRollViewModel.CurrentTrackIndex, // ����Ϊ��ǰ����
                    IsPreview = false
                };

                // ���ӵ��������ϣ��⽫�Զ�����UpdateMaxScrollExtent��
                _pianoRollViewModel.Notes.Add(finalNote);

                // ֻ�г���ʱ�Ÿ����û�Ԥ�賤��
                if (!_antiShakeService.IsShortPress(_creationStartTime))
                {
                    _pianoRollViewModel.SetUserDefinedNoteDuration(CreatingNote.Duration);
                    Debug.WriteLine($"�����û��Զ��峤��Ϊ: {CreatingNote.Duration}");
                }

                Debug.WriteLine($"��ɴ�������: {finalNote.Duration}, TrackIndex: {finalNote.TrackIndex}");
            }

            ClearCreating();
            OnCreationCompleted?.Invoke();
        }

        /// <summary>
        /// ȡ����������
        /// </summary>
        public void CancelCreating()
        {
            if (IsCreatingNote)
            {
                Debug.WriteLine("ȡ����������");
            }

            ClearCreating();
            OnCreationCancelled?.Invoke();
        }

        private void ClearCreating()
        {
            IsCreatingNote = false;
            CreatingNote = null;
        }

        // �¼�
        public event Action? OnCreationStarted;
        public event Action? OnCreationUpdated;
        public event Action? OnCreationCompleted;
        public event Action? OnCreationCancelled;
    }
}