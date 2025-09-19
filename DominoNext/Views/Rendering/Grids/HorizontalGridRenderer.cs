using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;

namespace Lumino.Views.Rendering.Grids
{
    /// <summary>
    /// ˮƽ��������Ⱦ�� - �����Ż��汾��֧�ֻ��ʸ���
    /// ֧�ֲ�ͬ������ģʽ��128����256���л���
    /// �Ż����ܣ��ڲ��������������ִ���л�����ȷ���ȶ���
    /// </summary>
    public class HorizontalGridRenderer
    {
        // �����ϴ���Ⱦ�Ĳ����������Ż�����
        private double _lastVerticalScrollOffset = double.NaN;
        private double _lastVerticalZoom = double.NaN;
        private double _lastKeyHeight = double.NaN;
        private double _lastBoundsWidth = double.NaN;

        // ����������
        private int _cachedVisibleStartKey;
        private int _cachedVisibleEndKey;
        private bool _cacheValid = false;

        // ���ʻ��� - ����Pen����
        private Pen? _cachedOctaveBoundaryPen;
        private Pen? _cachedKeyDividerPen;
        private readonly Dictionary<bool, IBrush> _keyRowBrushCache = new(); // �ڰ׼��������ʻ���

        /// <summary>
        /// ��ȡ����İ˶ȷֽ��߻���
        /// </summary>
        private Pen GetOctaveBoundaryPen()
        {
            return _cachedOctaveBoundaryPen ??= new Pen(RenderingUtils.GetResourceBrush("BorderLineBlackBrush", "#FF000000"), 1.5);
        }

        /// <summary>
        /// ��ȡ����ļ��ָ��߻���
        /// </summary>
        private Pen GetKeyDividerPen()
        {
            return _cachedKeyDividerPen ??= new Pen(RenderingUtils.GetResourceBrush("GridLineBrush", "#FFbad2f2"), 0.5);
        }

        /// <summary>
        /// ��Ⱦˮƽ�����ߣ��ȶ��汾 - �����Ż����ڲ��Ż����㣩
        /// </summary>
        public void RenderHorizontalGrid(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, double verticalScrollOffset)
        {
            var keyHeight = viewModel.KeyHeight;
            var verticalZoom = viewModel.VerticalZoom;

            // ����Ƿ���Ҫ���¼���ɼ���Χ
            bool needsRecalculation = !_cacheValid ||
                !AreEqual(_lastVerticalScrollOffset, verticalScrollOffset) ||
                !AreEqual(_lastVerticalZoom, verticalZoom) ||
                !AreEqual(_lastKeyHeight, keyHeight) ||
                !AreEqual(_lastBoundsWidth, bounds.Width);

            int visibleStartKey, visibleEndKey;

            if (needsRecalculation)
            {
                // ���¼���ɼ��ļ��̷�Χ
                visibleStartKey = (int)(verticalScrollOffset / keyHeight);
                visibleEndKey = (int)((verticalScrollOffset + bounds.Height) / keyHeight) + 1;

                visibleStartKey = Math.Max(0, visibleStartKey);
                visibleEndKey = Math.Min(128, visibleEndKey); // Ĭ��128����������չΪ������

                // ���»���
                _cachedVisibleStartKey = visibleStartKey;
                _cachedVisibleEndKey = visibleEndKey;
                _lastVerticalScrollOffset = verticalScrollOffset;
                _lastVerticalZoom = verticalZoom;
                _lastKeyHeight = keyHeight;
                _lastBoundsWidth = bounds.Width;
                _cacheValid = true;
            }
            else
            {
                // ʹ�û���ֵ
                visibleStartKey = _cachedVisibleStartKey;
                visibleEndKey = _cachedVisibleEndKey;
            }

            // �ȶ�ִ�л��ƣ�ȷ����ʾ�ȶ�
            for (int i = visibleStartKey; i < visibleEndKey; i++)
            {
                var midiNote = 127 - i; // MIDI������
                var y = i * keyHeight - verticalScrollOffset;
                
                RenderKeyRow(context, bounds, midiNote, y, keyHeight, viewModel);
                RenderKeyDivider(context, bounds, midiNote, y, keyHeight);
            }
        }

        /// <summary>
        /// �Ƚ�����doubleֵ�Ƿ���ȣ������������⣩
        /// </summary>
        private static bool AreEqual(double a, double b, double tolerance = 1e-10)
        {
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            if (double.IsNaN(a) || double.IsNaN(b)) return false;
            return Math.Abs(a - b) < tolerance;
        }

        /// <summary>
        /// ��Ⱦ������
        /// </summary>
        private void RenderKeyRow(DrawingContext context, Rect bounds, int midiNote, double y, double keyHeight, PianoRollViewModel viewModel)
        {
            var isBlackKey = viewModel.IsBlackKey(midiNote);
            var rowRect = new Rect(0, y, bounds.Width, keyHeight);
            
            var rowBrush = GetCachedKeyRowBrush(isBlackKey, viewModel);
            context.DrawRectangle(rowBrush, null, rowRect);
        }

        /// <summary>
        /// ��Ⱦ���ָ���
        /// </summary>
        private void RenderKeyDivider(DrawingContext context, Rect bounds, int midiNote, double y, double keyHeight)
        {
            // �ж��Ƿ��ǰ˶ȷֽ��ߣ�B��C֮�䣩
            var isOctaveBoundary = midiNote % 12 == 0;

            // ����ˮƽ�ֽ��� - ʹ�û���Ļ���
            var pen = isOctaveBoundary ? GetOctaveBoundaryPen() : GetKeyDividerPen();
            
            context.DrawLine(pen, new Point(0, y + keyHeight), new Point(bounds.Width, y + keyHeight));
        }

        /// <summary>
        /// ��ȡ����ļ���������
        /// </summary>
        private IBrush GetCachedKeyRowBrush(bool isBlackKey, PianoRollViewModel viewModel)
        {
            if (!_keyRowBrushCache.TryGetValue(isBlackKey, out var brush))
            {
                brush = isBlackKey ? CreateBlackKeyRowBrush(viewModel) : GetWhiteKeyRowBrush(viewModel);
                _keyRowBrushCache[isBlackKey] = brush;
            }
            return brush;
        }

        /// <summary>
        /// ��ȡ�׼���������
        /// </summary>
        private IBrush GetWhiteKeyRowBrush(PianoRollViewModel viewModel)
        {
            return RenderingUtils.GetResourceBrush("KeyWhiteBrush", "#FFFFFFFF");
        }

        /// <summary>
        /// �����ڼ��������ʣ���̬���㣩
        /// </summary>
        private IBrush CreateBlackKeyRowBrush(PianoRollViewModel viewModel)
        {
            // ����������ɫ��̬���㣬�ڼ��е���ɫ
            var mainBg = RenderingUtils.GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");
            
            if (mainBg is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                var brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255.0;
                
                if (brightness < 0.5) // ��ɫ����
                {
                    // ��ɫ���⣺ʹ��������΢��һЩ
                    return new SolidColorBrush(Color.FromArgb(
                        255,
                        (byte)Math.Min(255, color.R + 15),
                        (byte)Math.Min(255, color.G + 15),
                        (byte)Math.Min(255, color.B + 15)
                    ));
                }
                else // ǳɫ����
                {
                    // ǳɫ���⣺ʹ��������΢��һЩ
                    return new SolidColorBrush(Color.FromArgb(
                        255,
                        (byte)Math.Max(0, color.R - 25),
                        (byte)Math.Max(0, color.G - 25),
                        (byte)Math.Max(0, color.B - 25)
                    ));
                }
            }
            
            // Ĭ�ϵ�Ԥ����ɫ
            return RenderingUtils.GetResourceBrush("AppBackgroundBrush", "#FFedf3fe");
        }

        /// <summary>
        /// ������棨������ʱ���ã�
        /// </summary>
        public void ClearCache()
        {
            _cacheValid = false;
            _cachedOctaveBoundaryPen = null;
            _cachedKeyDividerPen = null;
            _keyRowBrushCache.Clear();
        }
    }
}