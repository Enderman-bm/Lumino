using System;
using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.Models.Music;

namespace Lumino.ViewModels.Editor.Modules.Base
{
    /// <summary>
    /// �༭��ģ����� - �ṩͨ�ù��ܺ͹淶
    /// ��ѭMVVM���ԭ�򣬼��ٴ����ظ�
    /// </summary>
    public abstract class EditorModuleBase
    {
        #region ��������
        protected readonly ICoordinateService _coordinateService;
        protected PianoRollViewModel? _pianoRollViewModel;
        #endregion

        #region ͨ�ó���
        /// <summary>
        /// ��׼������������ֵ
        /// </summary>
        protected const double STANDARD_ANTI_SHAKE_PIXEL_THRESHOLD = 1.0;
        
        /// <summary>
        /// ��׼������ʱ����ֵ�����룩
        /// </summary>
        protected const double STANDARD_ANTI_SHAKE_TIME_THRESHOLD_MS = 100.0;
        #endregion

        #region ���캯��
        protected EditorModuleBase(ICoordinateService coordinateService)
        {
            _coordinateService = coordinateService ?? throw new ArgumentNullException(nameof(coordinateService));
        }
        #endregion

        #region ͨ�÷���
        /// <summary>
        /// ���ø��پ���ViewModel����
        /// </summary>
        public virtual void SetPianoRollViewModel(PianoRollViewModel viewModel)
        {
            _pianoRollViewModel = viewModel;
        }

        /// <summary>
        /// ͨ������λ����֤
        /// </summary>
        protected static bool IsValidNotePosition(int pitch, double timeValue)
        {
            return pitch >= 0 && pitch <= 127 && timeValue >= 0;
        }

        /// <summary>
        /// ͨ������ת�� - ��ȡ����
        /// </summary>
        protected int GetPitchFromPosition(Point position)
        {
            if (_pianoRollViewModel == null) return 0;
            return _pianoRollViewModel.GetPitchFromScreenY(position.Y);
        }

        /// <summary>
        /// ͨ������ת�� - ��ȡʱ��
        /// </summary>
        protected double GetTimeFromPosition(Point position)
        {
            if (_pianoRollViewModel == null) return 0;
            return _pianoRollViewModel.GetTimeFromScreenX(position.X);
        }

        /// <summary>
        /// ͨ������ת�� - ��ȡ�������ʱ�����
        /// </summary>
        protected MusicalFraction GetQuantizedTimeFromPosition(Point position)
        {
            if (_pianoRollViewModel == null) return new MusicalFraction(0, 1);
            
            var timeValue = GetTimeFromPosition(position);
            var timeFraction = MusicalFraction.FromDouble(timeValue);
            return _pianoRollViewModel.SnapToGrid(timeFraction);
        }

        /// <summary>
        /// ͨ�÷�������� - �������ؾ���
        /// </summary>
        protected static bool IsMovementBelowPixelThreshold(Point start, Point current, double threshold = STANDARD_ANTI_SHAKE_PIXEL_THRESHOLD)
        {
            var deltaX = current.X - start.X;
            var deltaY = current.Y - start.Y;
            var totalMovement = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            return totalMovement < threshold;
        }

        /// <summary>
        /// ͨ�÷�������� - ����ʱ��
        /// </summary>
        protected static bool IsTimeBelowThreshold(DateTime startTime, double thresholdMs = STANDARD_ANTI_SHAKE_TIME_THRESHOLD_MS)
        {
            return (DateTime.Now - startTime).TotalMilliseconds < thresholdMs;
        }

        /// <summary>
        /// ��ȫ����������ʧЧ����
        /// </summary>
        protected static void SafeInvalidateNoteCache(NoteViewModel? note)
        {
            note?.InvalidateCache();
        }

        /// <summary>
        /// ������������ʧЧ����
        /// </summary>
        protected static void SafeInvalidateNotesCache(System.Collections.Generic.IEnumerable<NoteViewModel> notes)
        {
            foreach (var note in notes)
            {
                SafeInvalidateNoteCache(note);
            }
        }
        #endregion

        #region ���󷽷� - �������ʵ��
        /// <summary>
        /// ģ������ - ���ڵ��Ժ���־
        /// </summary>
        public abstract string ModuleName { get; }
        #endregion

        #region ���ⷽ�� - �����ѡ����д
        /// <summary>
        /// ģ���ʼ��
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// ģ������
        /// </summary>
        public virtual void Cleanup() { }
        #endregion
    }
}