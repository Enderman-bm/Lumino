using Avalonia;
using Avalonia.Media;
using Lumino.Views.Rendering.Tools;
using Lumino.Views.Rendering.Utils;
using Lumino.ViewModels.Editor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumino.Views.Rendering.Events
{
    /// <summary>
    /// ������������Ⱦ�� - չʾ����MouseCurveRenderer
    /// ֧�����ȡ�������CC��������������Ⱦ
    /// </summary>
    public class ControllerCurveRenderer
    {
        private readonly MouseCurveRenderer _curveRenderer = new MouseCurveRenderer();

        /// <summary>
        /// �����¼����ͻ�����Ӧ������
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="eventType">�¼�����</param>
        /// <param name="curveData">��������</param>
        /// <param name="canvasBounds">�����߽�</param>
        /// <param name="scrollOffset">����ƫ����</param>
        /// <param name="ccNumber">CC�������ţ������¼�����ΪControlChangeʱʹ�ã�</param>
        public void DrawEventCurve(DrawingContext context, EventType eventType, 
            IEnumerable<Point> curveData, Rect canvasBounds, double scrollOffset = 0, int ccNumber = 1)
        {
            if (!curveData?.Any() == true) return;

            MouseCurveRenderer.CurveStyle style = eventType switch
            {
                EventType.Velocity => CreateVelocityStyle(),
                EventType.PitchBend => CreatePitchBendStyle(),
                EventType.ControlChange => CreateControlChangeStyle(ccNumber),
                _ => CreateDefaultStyle()
            };

            _curveRenderer.DrawCurve(context, curveData, canvasBounds, scrollOffset, style);
        }

        /// <summary>
        /// �������߱༭Ԥ��
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="pitchEditingPath">���߱༭·��</param>
        /// <param name="canvasBounds">�����߽�</param>
        /// <param name="scrollOffset">����ƫ����</param>
        public void DrawPitchEditingPreview(DrawingContext context, IEnumerable<Point> pitchEditingPath, 
            Rect canvasBounds, double scrollOffset = 0)
        {
            if (!pitchEditingPath?.Any() == true) return;

            // �������߱༭ר����ʽ
            var pitchStyle = CreatePitchEditingStyle();
            
            // ʹ��ͨ�õ�������Ⱦ��
            _curveRenderer.DrawMouseTrail(context, pitchEditingPath, canvasBounds, scrollOffset, pitchStyle);
        }

        /// <summary>
        /// ����������������
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="bendCurve">��������</param>
        /// <param name="canvasBounds">�����߽�</param>
        /// <param name="scrollOffset">����ƫ����</param>
        public void DrawPitchBendCurve(DrawingContext context, IEnumerable<Point> bendCurve, 
            Rect canvasBounds, double scrollOffset = 0)
        {
            if (!bendCurve?.Any() == true) return;

            var bendStyle = CreatePitchBendStyle();
            
            // ��������ͨ������Ҫ��ʾ�ؼ���
            _curveRenderer.DrawCurve(context, bendCurve, canvasBounds, scrollOffset, bendStyle);
        }

        /// <summary>
        /// ����CC����������
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="ccCurve">CC����������</param>
        /// <param name="ccNumber">CC��������</param>
        /// <param name="canvasBounds">�����߽�</param>
        /// <param name="scrollOffset">����ƫ����</param>
        public void DrawControlChangeCurve(DrawingContext context, IEnumerable<Point> ccCurve, 
            int ccNumber, Rect canvasBounds, double scrollOffset = 0)
        {
            if (!ccCurve?.Any() == true) return;

            var ccStyle = CreateControlChangeStyle(ccNumber);
            
            _curveRenderer.DrawCurve(context, ccCurve, canvasBounds, scrollOffset, ccStyle);
        }

        /// <summary>
        /// ����������ʽ
        /// </summary>
        private MouseCurveRenderer.CurveStyle CreateVelocityStyle()
        {
            var brush = RenderingUtils.GetResourceBrush("VelocityBrush", "#FF4CAF50"); // ��ɫ
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 2),
                ShowDots = true,
                DotSize = 3.0,
                MaxDotsToShow = 20,
                UseSmoothCurve = false, // ����ͨ��ʹ��ֱ������
                BrushOpacity = 0.8
            };
        }

        /// <summary>
        /// �������߱༭��ʽ
        /// </summary>
        private MouseCurveRenderer.CurveStyle CreatePitchEditingStyle()
        {
            var brush = RenderingUtils.GetResourceBrush("PitchEditingBrush", "#FF9C27B0"); // ��ɫ
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 2, new DashStyle(new double[] { 5, 3 }, 0)),
                ShowDots = true,
                DotSize = 4.0,
                MaxDotsToShow = 15,
                UseSmoothCurve = true,
                BrushOpacity = 0.9
            };
        }

        /// <summary>
        /// ��������������ʽ
        /// </summary>
        private MouseCurveRenderer.CurveStyle CreatePitchBendStyle()
        {
            var brush = RenderingUtils.GetResourceBrush("PitchBendBrush", "#FF3F51B5"); // ��ɫ
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 3),
                ShowDots = false, // �������߲���ʾ��
                UseSmoothCurve = true,
                BrushOpacity = 0.8
            };
        }

        /// <summary>
        /// ����CC��������ʽ
        /// </summary>
        /// <param name="ccNumber">CC��������</param>
        private MouseCurveRenderer.CurveStyle CreateControlChangeStyle(int ccNumber)
        {
            // ����CC�����ɲ�ͬ����ɫ
            var colorHue = (ccNumber * 360 / 128) % 360;
            var color = ColorFromHsv(colorHue, 0.7, 0.8);
            var brush = new SolidColorBrush(color);
            
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 2),
                ShowDots = true,
                DotSize = 3.0,
                MaxDotsToShow = 25,
                UseSmoothCurve = true, // CC������ʹ��ƽ������
                BrushOpacity = 0.8
            };
        }

        /// <summary>
        /// ����Ĭ����ʽ
        /// </summary>
        private MouseCurveRenderer.CurveStyle CreateDefaultStyle()
        {
            var brush = RenderingUtils.GetResourceBrush("DefaultEventBrush", "#FF757575"); // ��ɫ
            return new MouseCurveRenderer.CurveStyle
            {
                Brush = brush,
                Pen = new Pen(brush, 2),
                ShowDots = true,
                DotSize = 3.0,
                MaxDotsToShow = 20,
                UseSmoothCurve = true,
                BrushOpacity = 0.7
            };
        }

        /// <summary>
        /// ��HSV��ɫ�ռ䴴��Color
        /// </summary>
        /// <param name="h">ɫ�� (0-360)</param>
        /// <param name="s">���Ͷ� (0-1)</param>
        /// <param name="v">���� (0-1)</param>
        /// <returns>Color����</returns>
        private static Color ColorFromHsv(double h, double s, double v)
        {
            h = h % 360;
            var c = v * s;
            var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            var m = v - c;
            
            double r = 0, g = 0, b = 0;
            
            if (h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }
            
            return Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }
    }
}