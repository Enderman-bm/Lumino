using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Lumino.Views.Rendering.Utils
{
    /// <summary>
    /// �����ı���Ⱦ�� - �ṩͳһ���ı����ƹ���
    /// ����MVVM�淶������̬���߷����������������ViewModel
    /// ���������Ż��Ļ������
    /// </summary>
    public static class NoteTextRenderer
    {
        // �ı���Ⱦ���棬��������
        private static readonly Dictionary<string, FormattedText> _textCache = new();
        private static readonly object _cacheLock = new();
        
        // Ԥ�����壬�����ظ�����
        private static readonly Typeface _defaultTypeface = new(FontFamily.Default);
        private static readonly Typeface _chineseTypeface = new(new FontFamily("Microsoft YaHei"));
        
        // �����С���ƣ���ֹ�ڴ����
        private const int MaxCacheSize = 100;

        /// <summary>
        /// Ԥ�õ�MIDI�������ƻ���
        /// </summary>
        private static readonly string[] _precomputedNoteNames = new string[128];
        
        static NoteTextRenderer()
        {
            InitializeNoteNames();
        }

        /// <summary>
        /// ��ʼ������MIDI��������
        /// </summary>
        private static void InitializeNoteNames()
        {
            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            
            for (int pitch = 0; pitch < 128; pitch++)
            {
                var octave = pitch / 12 - 1;
                var noteIndex = pitch % 12;
                _precomputedNoteNames[pitch] = $"{noteNames[noteIndex]}{octave}";
            }
        }

        /// <summary>
        /// ���ƻ����ı����ޱ�����
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="text">Ҫ���Ƶ��ı�</param>
        /// <param name="position">�ı�λ��</param>
        /// <param name="fontSize">�����С</param>
        /// <param name="textBrush">�ı���ˢ</param>
        /// <param name="useChineseFont">�Ƿ�ʹ����������</param>
        public static void DrawText(DrawingContext context, string text, Point position, 
            double fontSize, IBrush textBrush, bool useChineseFont = false)
        {
            var formattedText = GetCachedFormattedText(text, fontSize, textBrush, useChineseFont);
            context.DrawText(formattedText, position);
        }

        /// <summary>
        /// ���ƴ��������ı�
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="text">Ҫ���Ƶ��ı�</param>
        /// <param name="position">�ı�λ��</param>
        /// <param name="fontSize">�����С</param>
        /// <param name="textBrush">�ı���ˢ</param>
        /// <param name="backgroundBrush">������ˢ</param>
        /// <param name="padding">�����߾�</param>
        /// <param name="useChineseFont">�Ƿ�ʹ����������</param>
        public static void DrawTextWithBackground(DrawingContext context, string text, Point position,
            double fontSize, IBrush textBrush, IBrush backgroundBrush, 
            double padding = 2, bool useChineseFont = false)
        {
            var formattedText = GetCachedFormattedText(text, fontSize, textBrush, useChineseFont);
            
            // ���Ʊ���
            var backgroundRect = new Rect(
                position.X - padding,
                position.Y - padding / 2,
                formattedText.Width + padding * 2,
                formattedText.Height + padding);
            
            context.DrawRectangle(backgroundBrush, null, backgroundRect);
            
            // �����ı�
            context.DrawText(formattedText, position);
        }

        /// <summary>
        /// ���������ı������ж��룬��������
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="text">Ҫ���Ƶ��ı�</param>
        /// <param name="noteRect">������������</param>
        /// <param name="fontSize">�����С</param>
        /// <param name="textBrush">�ı���ˢ�����Ϊnull��ʹ��Ĭ����ɫ</param>
        /// <param name="backgroundBrush">������ˢ�����Ϊnull��ʹ��Ĭ����ɫ</param>
        /// <param name="useChineseFont">�Ƿ�ʹ����������</param>
        public static void DrawNoteText(DrawingContext context, string text, Rect noteRect,
            double fontSize, IBrush? textBrush = null, IBrush? backgroundBrush = null, 
            bool useChineseFont = false)
        {
            // ʹ��Ĭ�ϻ�ˢ���δ�ṩ
            textBrush ??= RenderingUtils.GetResourceBrush("MeasureTextBrush", "#FF000000");
            backgroundBrush ??= RenderingUtils.CreateBrushWithOpacity(
                RenderingUtils.GetResourceBrush("AppBackgroundBrush", "#FFFFFFFF"), 0.8);

            var formattedText = GetCachedFormattedText(text, fontSize, textBrush, useChineseFont);

            // �������λ��
            var textPosition = new Point(
                noteRect.X + (noteRect.Width - formattedText.Width) / 2,
                noteRect.Y + (noteRect.Height - formattedText.Height) / 2);

            // ���Ʊ���
            var textBounds = new Rect(
                textPosition.X - 2,
                textPosition.Y - 1,
                formattedText.Width + 4,
                formattedText.Height + 2);
            
            context.DrawRectangle(backgroundBrush, null, textBounds);

            // �����ı�
            context.DrawText(formattedText, textPosition);
        }

        /// <summary>
        /// ���ٻ������������ı���ʹ��Ԥ�õ��������ƣ�
        /// </summary>
        /// <param name="context">����������</param>
        /// <param name="pitch">MIDI����ֵ��0-127��</param>
        /// <param name="noteRect">������������</param>
        /// <param name="fontSize">�����С</param>
        /// <param name="textBrush">�ı���ˢ�����Ϊnull��ʹ��Ĭ����ɫ</param>
        /// <param name="backgroundBrush">������ˢ�����Ϊnull��ʹ��Ĭ����ɫ</param>
        public static void DrawNotePitchText(DrawingContext context, int pitch, Rect noteRect,
            double fontSize = 9, IBrush? textBrush = null, IBrush? backgroundBrush = null)
        {
            if (pitch < 0 || pitch > 127) return;

            var noteName = _precomputedNoteNames[pitch];
            DrawNoteText(context, noteName, noteRect, fontSize, textBrush, backgroundBrush);
        }

        /// <summary>
        /// ��ȡ����ĸ�ʽ���ı�����������
        /// </summary>
        private static FormattedText GetCachedFormattedText(string text, double fontSize, 
            IBrush textBrush, bool useChineseFont)
        {
            var cacheKey = $"{text}_{fontSize}_{useChineseFont}_{textBrush.GetHashCode()}";
            
            lock (_cacheLock)
            {
                if (_textCache.TryGetValue(cacheKey, out var cachedText))
                {
                    return cachedText;
                }

                // �����µĸ�ʽ���ı�
                var typeface = useChineseFont ? _chineseTypeface : _defaultTypeface;
                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    textBrush);

                // ���ӵ����棨���ƻ����С��
                if (_textCache.Count < MaxCacheSize)
                {
                    _textCache[cacheKey] = formattedText;
                }
                else
                {
                    // ��������������һ�뻺��
                    var keysToRemove = new List<string>();
                    int count = 0;
                    foreach (var key in _textCache.Keys)
                    {
                        if (count++ >= MaxCacheSize / 2) break;
                        keysToRemove.Add(key);
                    }
                    
                    foreach (var key in keysToRemove)
                    {
                        _textCache.Remove(key);
                    }
                    
                    _textCache[cacheKey] = formattedText;
                }

                return formattedText;
            }
        }

        /// <summary>
        /// �����ı����棨��ѡ�����ܹ���������
        /// </summary>
        public static void ClearTextCache()
        {
            lock (_cacheLock)
            {
                _textCache.Clear();
            }
        }
    }
}