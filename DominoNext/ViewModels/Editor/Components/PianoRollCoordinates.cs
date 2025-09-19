using System;
using Avalonia;
using Lumino.Services.Interfaces;
using Lumino.ViewModels.Editor;

namespace Lumino.ViewModels.Editor.Components
{
    /// <summary>
    /// ���پ�������ת����� - �������е�����ת������
    /// ���ϵ�һְ��ԭ��רע������ת���߼��ķ�װ
    /// </summary>
    public class PianoRollCoordinates
    {
        #region ����
        private readonly ICoordinateService _coordinateService;
        private readonly PianoRollCalculations _calculations;
        private readonly PianoRollViewport _viewport;
        #endregion

        #region ���캯��
        public PianoRollCoordinates(
            ICoordinateService coordinateService,
            PianoRollCalculations calculations,
            PianoRollViewport viewport)
        {
            _coordinateService = coordinateService ?? throw new ArgumentNullException(nameof(coordinateService));
            _calculations = calculations ?? throw new ArgumentNullException(nameof(calculations));
            _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        }
        #endregion

        #region ��������ת������
        /// <summary>
        /// ��Y�����ȡ����
        /// </summary>
        public int GetPitchFromY(double y)
        {
            return _coordinateService.GetPitchFromY(y, _calculations.KeyHeight);
        }

        /// <summary>
        /// ��X�����ȡʱ��
        /// </summary>
        public double GetTimeFromX(double x)
        {
            return _coordinateService.GetTimeFromX(x, _calculations.TimeToPixelScale);
        }

        /// <summary>
        /// ��������ȡλ��
        /// </summary>
        public Point GetPositionFromNote(NoteViewModel note)
        {
            return _coordinateService.GetPositionFromNote(note, _calculations.TimeToPixelScale, _calculations.KeyHeight);
        }

        /// <summary>
        /// ��ȡ�����ľ�������
        /// </summary>
        public Rect GetNoteRect(NoteViewModel note)
        {
            return _coordinateService.GetNoteRect(note, _calculations.TimeToPixelScale, _calculations.KeyHeight);
        }
        #endregion

        #region ֧�ֹ���ƫ�Ƶ�����ת������
        /// <summary>
        /// ����ĻY�����ȡ���ߣ����Ǵ�ֱ������
        /// </summary>
        public int GetPitchFromScreenY(double screenY)
        {
            return _coordinateService.GetPitchFromY(screenY, _calculations.KeyHeight, _viewport.VerticalScrollOffset);
        }

        /// <summary>
        /// ����ĻX�����ȡʱ�䣨����ˮƽ������
        /// </summary>
        public double GetTimeFromScreenX(double screenX)
        {
            return _coordinateService.GetTimeFromX(screenX, _calculations.TimeToPixelScale, _viewport.CurrentScrollOffset);
        }

        /// <summary>
        /// ��������ȡ��Ļλ�ã����ǹ���ƫ�ƣ�
        /// </summary>
        public Point GetScreenPositionFromNote(NoteViewModel note)
        {
            return _coordinateService.GetPositionFromNote(
                note, 
                _calculations.TimeToPixelScale, 
                _calculations.KeyHeight, 
                _viewport.CurrentScrollOffset, 
                _viewport.VerticalScrollOffset);
        }

        /// <summary>
        /// ��ȡ��������Ļ�������򣨿��ǹ���ƫ�ƣ�
        /// </summary>
        public Rect GetScreenNoteRect(NoteViewModel note)
        {
            return _coordinateService.GetNoteRect(
                note, 
                _calculations.TimeToPixelScale, 
                _calculations.KeyHeight, 
                _viewport.CurrentScrollOffset, 
                _viewport.VerticalScrollOffset);
        }
        #endregion

        #region �ɼ��Լ��
        /// <summary>
        /// ��������Ƿ��ڵ�ǰ�ɼ�������
        /// </summary>
        public bool IsNoteVisible(NoteViewModel note)
        {
            var noteRect = GetNoteRect(note);
            
            // ���ˮƽ�ɼ���
            var noteStartX = noteRect.X;
            var noteEndX = noteRect.X + noteRect.Width;
            var visibleStartX = _viewport.CurrentScrollOffset;
            var visibleEndX = _viewport.CurrentScrollOffset + _viewport.ViewportWidth;
            
            if (noteEndX < visibleStartX || noteStartX > visibleEndX)
                return false;
            
            // ��鴹ֱ�ɼ���
            var noteStartY = noteRect.Y;
            var noteEndY = noteRect.Y + noteRect.Height;
            var visibleStartY = _viewport.VerticalScrollOffset;
            var visibleEndY = _viewport.VerticalScrollOffset + _viewport.VerticalViewportSize;
            
            return !(noteEndY < visibleStartY || noteStartY > visibleEndY);
        }

        /// <summary>
        /// ��ȡ��ǰ�ɼ������ʱ�䷶Χ
        /// </summary>
        public (double startTime, double endTime) GetVisibleTimeRange()
        {
            var startTime = GetTimeFromScreenX(0);
            var endTime = GetTimeFromScreenX(_viewport.ViewportWidth);
            return (startTime, endTime);
        }

        /// <summary>
        /// ��ȡ��ǰ�ɼ���������߷�Χ
        /// </summary>
        public (int lowPitch, int highPitch) GetVisiblePitchRange()
        {
            var highPitch = GetPitchFromScreenY(0);
            var lowPitch = GetPitchFromScreenY(_viewport.VerticalViewportSize);
            return (lowPitch, highPitch);
        }
        #endregion

        #region ��������
        /// <summary>
        /// ����������ת��Ϊ��Ļ����
        /// </summary>
        public Point WorldToScreen(Point worldPosition)
        {
            return new Point(
                worldPosition.X - _viewport.CurrentScrollOffset,
                worldPosition.Y - _viewport.VerticalScrollOffset);
        }

        /// <summary>
        /// ����Ļ����ת��Ϊ��������
        /// </summary>
        public Point ScreenToWorld(Point screenPosition)
        {
            return new Point(
                screenPosition.X + _viewport.CurrentScrollOffset,
                screenPosition.Y + _viewport.VerticalScrollOffset);
        }

        /// <summary>
        /// �����Ļ���Ƿ���������
        /// </summary>
        public bool IsPointInNote(Point screenPoint, NoteViewModel note)
        {
            var screenRect = GetScreenNoteRect(note);
            return screenRect.Contains(screenPoint);
        }
        #endregion
    }
}