using Avalonia;
using Avalonia.Media;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Editor.Modules;
using Lumino.Views.Rendering.Tools;
using Lumino.Views.Rendering.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lumino.Views.Rendering.Events
{
    /// <summary>
    /// ��������Ⱦ�� - �����Ż��汾��֧�ֻ��ʸ��úͺ�̨Ԥ����
    /// </summary>
    public class VelocityBarRenderer
    {
        private const double BAR_MARGIN = 1.0;
        private const double MIN_BAR_WIDTH = 2.0;

        // ���������Ⱦ��ʵ��
        private readonly MouseCurveRenderer _curveRenderer = new MouseCurveRenderer();

        #region ���ʻ���ϵͳ

        // ����Ⱦ���ͺ�͸���ȼ��𻺴滭��
        private readonly Dictionary<(VelocityRenderType, double), (IBrush brush, IPen pen)> _styleCache = new();

        // �ı���Ⱦ����
        private readonly Dictionary<(int velocity, bool isPreview), FormattedText> _textCache = new();
        private readonly Dictionary<string, Typeface> _typefaceCache = new();

        // Ԥ�����ʻ���
        private readonly Dictionary<double, (IBrush brush, IPen pen)> _previewStyleCache = new();

        // �����Ż�����
        private bool _enableBackgroundPrecomputation = true;
        private int _precomputationThreshold = 1000; // ����������������ʱ���ú�̨Ԥ����

        #endregion

        #region ��̨Ԥ����ϵͳ

        // Ԥ����������
        private readonly ConcurrentDictionary<string, VelocityBarData> _precomputedBars = new();
        private volatile bool _precomputationInProgress = false;

        /// <summary>
        /// ������Ԥ��������
        /// </summary>
        private class VelocityBarData
        {
            public Rect BarRect { get; set; }
            public double Opacity { get; set; }
            public bool IsVisible { get; set; }
            public string CacheKey { get; set; } = string.Empty;
        }

        #endregion

        /// <summary>
        /// ���ú�̨Ԥ���㹦��
        /// </summary>
        public void SetBackgroundPrecomputationEnabled(bool enabled)
        {
            _enableBackgroundPrecomputation = enabled;
        }

        /// <summary>
        /// ���ú�̨Ԥ���������������ֵ
        /// </summary>
        public void SetPrecomputationThreshold(int threshold)
        {
            _precomputationThreshold = threshold;
        }

        /// <summary>
        /// ��Ҫ�����������Ʒ��� - �����Ż��汾
        /// </summary>
        public void DrawVelocityBar(DrawingContext context, NoteViewModel note, Rect canvasBounds,
            double timeToPixelScale, VelocityRenderType renderType = VelocityRenderType.Normal,
            double scrollOffset = 0)
        {
            // ���ɻ����
            var cacheKey = GenerateCacheKey(note, canvasBounds, timeToPixelScale, scrollOffset);

            // ���Դ�Ԥ���㻺���ȡ����
            if (_precomputedBars.TryGetValue(cacheKey, out var precomputedData))
            {
                if (!precomputedData.IsVisible) return;

                // ʹ��Ԥ��������ݿ�����Ⱦ
                DrawVelocityBarFast(context, precomputedData, note, renderType);
                return;
            }

            // ���˵�������㷽ʽ
            DrawVelocityBarRegular(context, note, canvasBounds, timeToPixelScale, renderType, scrollOffset);
        }

        /// <summary>
        /// ������Ⱦ��ʹ��Ԥ�������ݣ�
        /// </summary>
        private void DrawVelocityBarFast(DrawingContext context, VelocityBarData data, NoteViewModel note, VelocityRenderType renderType)
        {
            var (brush, pen) = GetCachedStyle(renderType, data.Opacity);
            context.DrawRectangle(brush, pen, data.BarRect);

            // ��������㹻�󣬻�������ֵ
            if (data.BarRect.Width > 30 && renderType == VelocityRenderType.Selected)
            {
                DrawVelocityValueCached(context, data.BarRect, note.Velocity);
            }
        }

        /// <summary>
        /// ������Ⱦ��ʽ�������Ա�֤��
        /// </summary>
        private void DrawVelocityBarRegular(DrawingContext context, NoteViewModel note, Rect canvasBounds,
            double timeToPixelScale, VelocityRenderType renderType, double scrollOffset)
        {
            // ����������ʱ�����ϵ�λ�úͿ��ȣ��������꣩
            var absoluteNoteX = note.GetX(timeToPixelScale);
            var noteWidth = note.GetWidth(timeToPixelScale);

            // Ӧ�ù���ƫ�Ƶõ���������
            var noteX = absoluteNoteX - scrollOffset;

            // ȷ�������ڻ�����Χ��
            if (noteX + noteWidth < 0 || noteX > canvasBounds.Width) return;

            // �����������ĳߴ�
            var barWidth = Math.Max(MIN_BAR_WIDTH, noteWidth - BAR_MARGIN * 2);
            var barHeight = CalculateBarHeight(note.Velocity, canvasBounds.Height);

            var barX = noteX + BAR_MARGIN;
            var barY = canvasBounds.Height - barHeight;

            var barRect = new Rect(barX, barY, barWidth, barHeight);
            var opacity = CalculateOpacity(note.Velocity);

            // ʹ�û������ʽ
            var (brush, pen) = GetCachedStyle(renderType, opacity);

            // ����������
            context.DrawRectangle(brush, pen, barRect);

            // ��������㹻�󣬻�������ֵ
            if (barWidth > 30 && renderType == VelocityRenderType.Selected)
            {
                DrawVelocityValueCached(context, barRect, note.Velocity);
            }
        }

        /// <summary>
        /// ����Ԥ�������������ݣ���̨�̣߳�
        /// </summary>
        public async Task PrecomputeVelocityBarsAsync(IEnumerable<NoteViewModel> notes, Rect canvasBounds,
            double timeToPixelScale, double scrollOffset)
        {
            if (!_enableBackgroundPrecomputation || _precomputationInProgress) return;

            var noteList = notes.ToList();
            if (noteList.Count < _precomputationThreshold) return;

            _precomputationInProgress = true;

            try
            {
                await Task.Run(() =>
                {
                    var newCache = new Dictionary<string, VelocityBarData>();

                    foreach (var note in noteList)
                    {
                        var cacheKey = GenerateCacheKey(note, canvasBounds, timeToPixelScale, scrollOffset);

                        // ��������������
                        var absoluteNoteX = note.GetX(timeToPixelScale);
                        var noteWidth = note.GetWidth(timeToPixelScale);
                        var noteX = absoluteNoteX - scrollOffset;

                        var isVisible = !(noteX + noteWidth < 0 || noteX > canvasBounds.Width);

                        if (isVisible)
                        {
                            var barWidth = Math.Max(MIN_BAR_WIDTH, noteWidth - BAR_MARGIN * 2);
                            var barHeight = CalculateBarHeight(note.Velocity, canvasBounds.Height);
                            var barX = noteX + BAR_MARGIN;
                            var barY = canvasBounds.Height - barHeight;

                            newCache[cacheKey] = new VelocityBarData
                            {
                                BarRect = new Rect(barX, barY, barWidth, barHeight),
                                Opacity = CalculateOpacity(note.Velocity),
                                IsVisible = true,
                                CacheKey = cacheKey
                            };
                        }
                        else
                        {
                            newCache[cacheKey] = new VelocityBarData
                            {
                                IsVisible = false,
                                CacheKey = cacheKey
                            };
                        }
                    }

                    // ԭ���Ը��»���
                    foreach (var kvp in newCache)
                    {
                        _precomputedBars.AddOrUpdate(kvp.Key, kvp.Value, (key, oldValue) => kvp.Value);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ԥ����������ʱ����: {ex.Message}");
            }
            finally
            {
                _precomputationInProgress = false;
            }
        }

        /// <summary>
        /// ���Ʊ༭Ԥ�� - �Ż��汾
        /// </summary>
        public void DrawEditingPreview(DrawingContext context, Rect canvasBounds,
            VelocityEditingModule editingModule, double timeToPixelScale, double scrollOffset = 0)
        {
            if (editingModule.EditingPath?.Any() != true) return;

            // ʹ�����������Ⱦ�����Ʊ༭�켣
            var curveStyle = _curveRenderer.CreateEditingPreviewStyle();
            _curveRenderer.DrawMouseTrail(context, editingModule.EditingPath, canvasBounds, scrollOffset, curveStyle);

            // ���Ƶ�ǰ�༭λ�õ�������Ԥ��
            if (editingModule.CurrentEditPosition.HasValue)
            {
                DrawCurrentEditPositionPreviewCached(context, editingModule.CurrentEditPosition.Value,
                    canvasBounds, scrollOffset, curveStyle.Brush);
            }
        }

        #region �����Ż�����

        /// <summary>
        /// ��ȡ�������ʽ
        /// </summary>
        private (IBrush brush, IPen pen) GetCachedStyle(VelocityRenderType renderType, double opacity)
        {
            // ����͸���ȵ�0.1����
            var quantizedOpacity = Math.Round(opacity, 1);
            var cacheKey = (renderType, quantizedOpacity);

            if (!_styleCache.TryGetValue(cacheKey, out var style))
            {
                style = CreateStyle(renderType, quantizedOpacity);
                _styleCache[cacheKey] = style;
            }

            return style;
        }

        /// <summary>
        /// ������ʽ
        /// </summary>
        private (IBrush brush, IPen pen) CreateStyle(VelocityRenderType renderType, double opacity)
        {
            return renderType switch
            {
                VelocityRenderType.Selected => (
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocitySelectedBrush", "#FFFF9800"), opacity),
                    RenderingUtils.GetResourcePen("VelocitySelectedPenBrush", "#FFF57C00", 1)
                ),
                VelocityRenderType.Editing => (
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocityEditingBrush", "#FFFF5722"), opacity),
                    RenderingUtils.GetResourcePen("VelocityEditingPenBrush", "#FFD84315", 2)
                ),
                VelocityRenderType.Dragging => (
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocityDraggingBrush", "#FF2196F3"), opacity),
                    RenderingUtils.GetResourcePen("VelocityDraggingPenBrush", "#FF1976D2", 1)
                ),
                _ => ( // Normal
                    RenderingUtils.CreateBrushWithOpacity(
                        RenderingUtils.GetResourceBrush("VelocityBrush", "#FF4CAF50"), opacity),
                    RenderingUtils.GetResourcePen("VelocityPenBrush", "#FF2E7D32", 1)
                )
            };
        }

        /// <summary>
        /// ��ȡ���������
        /// </summary>
        private Typeface GetCachedTypeface(string fontFamily = "Segoe UI")
        {
            if (!_typefaceCache.TryGetValue(fontFamily, out var typeface))
            {
                typeface = new Typeface(fontFamily, FontStyle.Normal, FontWeight.Normal);
                _typefaceCache[fontFamily] = typeface;
            }
            return typeface;
        }

        /// <summary>
        /// ��������ֵ - �����Ż��汾
        /// </summary>
        private void DrawVelocityValueCached(DrawingContext context, Rect barRect, int velocity, bool isPreview = false)
        {
            var cacheKey = (velocity, isPreview);

            if (!_textCache.TryGetValue(cacheKey, out var formattedText))
            {
                var textBrush = RenderingUtils.GetResourceBrush("VelocityTextBrush", "#FFFFFFFF");
                var typeface = GetCachedTypeface();

                formattedText = new FormattedText(
                    velocity.ToString(),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    isPreview ? 12 : 10,
                    textBrush);

                _textCache[cacheKey] = formattedText;
            }

            var textX = barRect.X + (barRect.Width - formattedText.Width) / 2;
            var textY = isPreview ? barRect.Y - 15 : barRect.Y + 2;

            context.DrawText(formattedText, new Point(textX, textY));
        }

        /// <summary>
        /// ���Ƶ�ǰ�༭λ��Ԥ�� - �����Ż��汾
        /// </summary>
        private void DrawCurrentEditPositionPreviewCached(DrawingContext context, Point worldPosition,
            Rect canvasBounds, double scrollOffset, IBrush previewBrush)
        {
            var screenPos = new Point(worldPosition.X - scrollOffset, worldPosition.Y);

            // ֻ����Ļ��Χ�ڻ���Ԥ��
            if (screenPos.X < -20 || screenPos.X > canvasBounds.Width + 20) return;

            var velocity = CalculateVelocityFromY(screenPos.Y, canvasBounds.Height);
            var previewHeight = CalculateBarHeight(velocity, canvasBounds.Height);
            var previewRect = new Rect(screenPos.X - 8, canvasBounds.Height - previewHeight, 16, previewHeight);

            // ʹ�û����Ԥ����ʽ
            var opacity = 0.7;
            var quantizedOpacity = Math.Round(opacity, 1);

            if (!_previewStyleCache.TryGetValue(quantizedOpacity, out var previewStyle))
            {
                var previewBarBrush = RenderingUtils.CreateBrushWithOpacity(previewBrush, opacity);
                var previewPen = new Pen(previewBrush, 2, new DashStyle(new double[] { 3, 3 }, 0));
                previewStyle = (previewBarBrush, previewPen);
                _previewStyleCache[quantizedOpacity] = previewStyle;
            }

            context.DrawRectangle(previewStyle.brush, previewStyle.pen, previewRect);

            // ��ʾ��ǰ����ֵ
            DrawVelocityValueCached(context, previewRect, velocity, true);
        }

        /// <summary>
        /// ���ɻ����
        /// </summary>
        private string GenerateCacheKey(NoteViewModel note, Rect canvasBounds, double timeToPixelScale, double scrollOffset)
        {
            return $"{note.Id}_{canvasBounds.Width:F0}_{canvasBounds.Height:F0}_{timeToPixelScale:F2}_{scrollOffset:F0}_{note.Velocity}";
        }

        #endregion

        #region ���㷽��

        private double CalculateOpacity(int velocity)
        {
            // ��������ֵ����͸���ȣ�ȷ���ɼ���
            return Math.Max(0.4, velocity / 127.0);
        }

        private double CalculateBarHeight(int velocity, double maxHeight)
        {
            // ��MIDI����ֵ(0-127)ӳ�䵽���θ߶�
            var normalizedVelocity = Math.Max(0, Math.Min(127, velocity)) / 127.0;
            return normalizedVelocity * maxHeight;
        }

        public static int CalculateVelocityFromY(double y, double maxHeight)
        {
            // ��Y���귴������ֵ
            var normalizedY = Math.Max(0, Math.Min(1, (maxHeight - y) / maxHeight));
            var velocity = Math.Max(1, Math.Min(127, (int)Math.Round(normalizedY * 127)));

            return velocity;
        }

        #endregion

        #region �������

        /// <summary>
        /// ������л��棨���������ڴ�ѹ��ʱ���ã�
        /// </summary>
        public void ClearAllCaches()
        {
            _styleCache.Clear();
            _textCache.Clear();
            _typefaceCache.Clear();
            _previewStyleCache.Clear();
            _precomputedBars.Clear();
        }

        /// <summary>
        /// ���Ԥ���㻺�棨����������ʱ���ã�
        /// </summary>
        public void ClearPrecomputedCache()
        {
            _precomputedBars.Clear();
        }

        /// <summary>
        /// ��ȡ����ͳ����Ϣ�������ã�
        /// </summary>
        public string GetCacheStatistics()
        {
            return $"��ʽ����: {_styleCache.Count}, �ı�����: {_textCache.Count}, " +
                   $"Ԥ���㻺��: {_precomputedBars.Count}, Ԥ����ʽ����: {_previewStyleCache.Count}";
        }

        #endregion
    }

    /// <summary>
    /// ��������Ⱦ����
    /// </summary>
    public enum VelocityRenderType
    {
        Normal,
        Selected,
        Editing,
        Dragging
    }
}