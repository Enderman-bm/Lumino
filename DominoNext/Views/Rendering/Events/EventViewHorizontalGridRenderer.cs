using System;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Views.Rendering.Events
{
    /// <summary>
    /// �¼���ͼˮƽ��������Ⱦ�� - �����¼���ͼ��ˮƽ�ָ���
    /// ���¼���ͼ�߶ȷ�Ϊ4�ȷݣ���1/4��1/2��3/4�����ƺ���
    /// �Ż����ԣ��ڲ����������������ִ�л�����ȷ���ȶ���
    /// </summary>
    public class EventViewHorizontalGridRenderer
    {
        // �����ϴ���Ⱦ�Ĳ����������Ż�����
        private double _lastBoundsHeight = double.NaN;
        private double _lastBoundsWidth = double.NaN;

        // ���������
        private double[] _cachedHorizontalLinePositions = new double[3];
        private bool _cacheValid = false;

        // ʹ�ö�̬���ʻ�ȡ��ȷ��������״̬ͬ��
        private IPen HorizontalLinePen => RenderingUtils.GetResourcePen("GridLineBrush", "#FFBAD2F2", 1);

        /// <summary>
        /// ��Ⱦ�¼���ͼˮƽ�����ߣ��ȶ��汾 - ���ǻ��ƣ��ڲ��Ż����㣩
        /// </summary>
        public void RenderEventViewHorizontalGrid(DrawingContext context, PianoRollViewModel viewModel, Rect bounds)
        {
            // ����Ƿ���Ҫ���¼������λ��
            bool needsRecalculation = !_cacheValid ||
                !AreEqual(_lastBoundsHeight, bounds.Height) ||
                !AreEqual(_lastBoundsWidth, bounds.Width);

            double[] horizontalLinePositions;

            if (needsRecalculation)
            {
                // ���¼������λ��
                var quarterHeight = bounds.Height / 4.0;
                
                for (int i = 0; i < 3; i++)
                {
                    _cachedHorizontalLinePositions[i] = (i + 1) * quarterHeight;
                }

                // ���»���
                _lastBoundsHeight = bounds.Height;
                _lastBoundsWidth = bounds.Width;
                _cacheValid = true;
                
                horizontalLinePositions = _cachedHorizontalLinePositions;
            }
            else
            {
                // ʹ�û���ֵ
                horizontalLinePositions = _cachedHorizontalLinePositions;
            }

            // ����ִ�л��ƣ�ȷ����ʾ�ȶ�
            // ʹ�ö�̬��ȡ�Ļ��ʣ�ȷ��������ͬ��
            var pen = HorizontalLinePen;
            
            for (int i = 0; i < 3; i++)
            {
                var y = horizontalLinePositions[i];
                context.DrawLine(pen,
                    new Point(0, y), 
                    new Point(bounds.Width, y));
            }
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
    }
}