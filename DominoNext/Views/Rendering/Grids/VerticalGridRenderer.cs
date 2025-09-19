using System;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Models.Music;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Views.Rendering.Grids
{
    /// <summary>
    /// ��ֱ��������Ⱦ�� - �޸������ܶ�����
    /// ֧�ֲ�ͬ�ĺţ�4/4�ġ�3/4�ġ�8/4�ĵȣ��Ķ�̬����
    /// �Ż����ԣ��ڲ������������������ִ�л�����ȷ���ȶ���
    /// </summary>
    public class VerticalGridRenderer
    {
        // �����ϴ���Ⱦ�Ĳ����������Ż�����
        private double _lastHorizontalScrollOffset = double.NaN;
        private double _lastZoom = double.NaN;
        private double _lastViewportWidth = double.NaN;
        private double _lastTimeToPixelScale = double.NaN;

        // ���������
        private double _cachedVisibleStartTime;
        private double _cachedVisibleEndTime;
        private bool _cacheValid = false;

        // ʹ�ö�̬���ʻ�ȡ��ȷ��������״̬ͬ��
        private IPen SixteenthNotePen => new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", "#FFafafaf"), 0.5) { DashStyle = new DashStyle(new double[] { 1, 3 }, 0) };
        private IPen EighthNotePen => new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", "#FFafafaf"), 0.7) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) };
        private IPen BeatLinePen => new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", "#FFafafaf"), 0.8);
        private IPen MeasureLinePen => new Pen(RenderingUtils.GetResourceBrush("MeasureLineBrush", "#FF000080"), 1.2);

        /// <summary>
        /// ��Ⱦ��ֱ�����ߣ��޸������ܶ����⣩
        /// </summary>
        public void RenderVerticalGrid(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, double scrollOffset)
        {
            var timeToPixelScale = viewModel.TimeToPixelScale;
            
            // ����Ƿ���Ҫ���¼���ɼ���Χ�������Ż���
            bool needsRecalculation = !_cacheValid ||
                !AreEqual(_lastHorizontalScrollOffset, scrollOffset) ||
                !AreEqual(_lastZoom, viewModel.Zoom) ||
                !AreEqual(_lastViewportWidth, bounds.Width) ||
                !AreEqual(_lastTimeToPixelScale, timeToPixelScale);

            double visibleStartTime, visibleEndTime;

            if (needsRecalculation)
            {
                // ����ɼ���ʱ�䷶Χ�����ķ�����Ϊ��λ��
                visibleStartTime = scrollOffset / viewModel.BaseQuarterNoteWidth;
                visibleEndTime = (scrollOffset + bounds.Width) / viewModel.BaseQuarterNoteWidth;

                // ���»���
                _cachedVisibleStartTime = visibleStartTime;
                _cachedVisibleEndTime = visibleEndTime;
                _lastHorizontalScrollOffset = scrollOffset;
                _lastZoom = viewModel.Zoom;
                _lastViewportWidth = bounds.Width;
                _lastTimeToPixelScale = timeToPixelScale;
                _cacheValid = true;
            }
            else
            {
                // ʹ�û���ֵ
                visibleStartTime = _cachedVisibleStartTime;
                visibleEndTime = _cachedVisibleEndTime;
            }

            var totalKeyHeight = 128 * viewModel.KeyHeight;
            var startY = 0;
            var endY = Math.Min(bounds.Height, totalKeyHeight);

            // ����ִ�л��ƣ�ȷ����ʾ�ȶ�
            // ���մ�ϸ���ֵ�˳����������ߣ�ȷ�����߸���ϸ��
            RenderSixteenthNoteLines(context, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
            RenderEighthNoteLines(context, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
            RenderBeatLines(context, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
            RenderMeasureLines(context, viewModel, bounds, scrollOffset, visibleStartTime, visibleEndTime, startY, endY);
        }

        /// <summary>
        /// �Ƚ�����doubleֵ�Ƿ���ȣ��������㾫�����⣩
        /// </summary>
        private static bool AreEqual(double a, double b, double tolerance = 1e-10)
        {
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            if (double.IsNaN(a) || double.IsNaN(b)) return false;
            return Math.Abs(a - b) < tolerance;
        }

        /// <summary>
        /// ��Ⱦʮ�������������� - �޸����
        /// </summary>
        private void RenderSixteenthNoteLines(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            var sixteenthWidth = viewModel.SixteenthNoteWidth;
            if (sixteenthWidth <= 5) return; // ̫�ܼ�ʱ������

            // ʮ����������ࣺ1/4�ķ����� = 0.25
            var sixteenthInterval = 0.25;
            var startSixteenth = (int)(visibleStartTime / sixteenthInterval);
            var endSixteenth = (int)(visibleEndTime / sixteenthInterval) + 1;

            // ʹ�ö�̬��ȡ�Ļ���
            var pen = SixteenthNotePen;

            for (int i = startSixteenth; i <= endSixteenth; i++)
            {
                // �����������غϵ�λ�ã�ÿ4��ʮ�������� = 1���ķ�������
                if (i % 4 == 0) continue;

                var timeValue = i * sixteenthInterval; // ���ķ�����Ϊ��λ
                var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                }
            }
        }

        /// <summary>
        /// ��Ⱦ�˷����������� - �޸����
        /// </summary>
        private void RenderEighthNoteLines(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            var eighthWidth = viewModel.EighthNoteWidth;
            if (eighthWidth <= 10) return; // ̫�ܼ�ʱ������

            // �˷�������ࣺ1/2�ķ����� = 0.5
            var eighthInterval = 0.5;
            var startEighth = (int)(visibleStartTime / eighthInterval);
            var endEighth = (int)(visibleEndTime / eighthInterval) + 1;

            // ʹ�ö�̬��ȡ�Ļ���
            var pen = EighthNotePen;

            for (int i = startEighth; i <= endEighth; i++)
            {
                // �����������غϵ�λ�ã�ÿ2���˷����� = 1���ķ�������
                if (i % 2 == 0) continue;

                var timeValue = i * eighthInterval; // ���ķ�����Ϊ��λ
                var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                }
            }
        }

        /// <summary>
        /// ��Ⱦ���� - �޸����
        /// </summary>
        private void RenderBeatLines(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            // ���߼�ࣺ1���ķ����� = 1.0
            var beatInterval = 1.0;
            var startBeat = (int)(visibleStartTime / beatInterval);
            var endBeat = (int)(visibleEndTime / beatInterval) + 1;

            // ʹ�ö�̬��ȡ�Ļ���
            var pen = BeatLinePen;

            for (int i = startBeat; i <= endBeat; i++)
            {
                // ������С�����غϵ�λ�ã�ÿBeatsPerMeasure���� = 1��С�ڣ�
                if (i % viewModel.BeatsPerMeasure == 0) continue;

                var timeValue = i * beatInterval; // ���ķ�����Ϊ��λ
                var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                }
            }
        }

        /// <summary>
        /// ��ȾС���� - �޸����
        /// </summary>
        private void RenderMeasureLines(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, 
            double scrollOffset, double visibleStartTime, double visibleEndTime, double startY, double endY)
        {
            // С���߼�ࣺBeatsPerMeasure���ķ�������4/4�� = 4.0��
            var measureInterval = (double)viewModel.BeatsPerMeasure;
            var startMeasure = (int)(visibleStartTime / measureInterval);
            var endMeasure = (int)(visibleEndTime / measureInterval) + 1;

            // ʹ�ö�̬��ȡ�Ļ���
            var pen = MeasureLinePen;

            for (int i = startMeasure; i <= endMeasure; i++)
            {
                var timeValue = i * measureInterval; // ���ķ�����Ϊ��λ
                var x = timeValue * viewModel.BaseQuarterNoteWidth - scrollOffset;
                
                if (x >= 0 && x <= bounds.Width)
                {
                    context.DrawLine(pen, new Point(x, startY), new Point(x, endY));
                }
            }
        }
    }
}