using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace DominoNext.Views.Rendering.Utils
{
    /// <summary>
    /// 音符文本渲染器 - 提供统一的文本绘制功能
    /// 符合MVVM规范，纯静态工具方法，不依赖具体的ViewModel
    /// 包含性能优化的缓存机制
    /// </summary>
    public static class NoteTextRenderer
    {
        // 文本渲染缓存，提升性能
        private static readonly Dictionary<string, FormattedText> _textCache = new();
        private static readonly object _cacheLock = new();
        
        // 预置字体，避免重复创建
        private static readonly Typeface _defaultTypeface = new(FontFamily.Default);
        private static readonly Typeface _chineseTypeface = new(new FontFamily("Microsoft YaHei"));
        
        // 缓存大小限制，防止内存溢出
        private const int MaxCacheSize = 100;

        /// <summary>
        /// 预置的MIDI音符名称缓存
        /// </summary>
        private static readonly string[] _precomputedNoteNames = new string[128];
        
        static NoteTextRenderer()
        {
            InitializeNoteNames();
        }

        /// <summary>
        /// 初始化所有MIDI音符名称
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
        /// 绘制基础文本（无背景）
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="text">要绘制的文本</param>
        /// <param name="position">文本位置</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="textBrush">文本画刷</param>
        /// <param name="useChineseFont">是否使用中文字体</param>
        public static void DrawText(DrawingContext context, string text, Point position, 
            double fontSize, IBrush textBrush, bool useChineseFont = false)
        {
            var formattedText = GetCachedFormattedText(text, fontSize, textBrush, useChineseFont);
            context.DrawText(formattedText, position);
        }

        /// <summary>
        /// 绘制带背景的文本
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="text">要绘制的文本</param>
        /// <param name="position">文本位置</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="textBrush">文本画刷</param>
        /// <param name="backgroundBrush">背景画刷</param>
        /// <param name="padding">背景边距</param>
        /// <param name="useChineseFont">是否使用中文字体</param>
        public static void DrawTextWithBackground(DrawingContext context, string text, Point position,
            double fontSize, IBrush textBrush, IBrush backgroundBrush, 
            double padding = 2, bool useChineseFont = false)
        {
            var formattedText = GetCachedFormattedText(text, fontSize, textBrush, useChineseFont);
            
            // 绘制背景
            var backgroundRect = new Rect(
                position.X - padding,
                position.Y - padding / 2,
                formattedText.Width + padding * 2,
                formattedText.Height + padding);
            
            context.DrawRectangle(backgroundBrush, null, backgroundRect);
            
            // 绘制文本
            context.DrawText(formattedText, position);
        }

        /// <summary>
        /// 绘制音符文本（居中对齐，带背景）
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="text">要绘制的文本</param>
        /// <param name="noteRect">音符矩形区域</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="textBrush">文本画刷，如果为null则使用默认颜色</param>
        /// <param name="backgroundBrush">背景画刷，如果为null则使用默认颜色</param>
        /// <param name="useChineseFont">是否使用中文字体</param>
        public static void DrawNoteText(DrawingContext context, string text, Rect noteRect,
            double fontSize, IBrush? textBrush = null, IBrush? backgroundBrush = null, 
            bool useChineseFont = false)
        {
            // 使用默认画刷如果未提供
            textBrush ??= RenderingUtils.GetResourceBrush("MeasureTextBrush", "#FF000000");
            backgroundBrush ??= RenderingUtils.CreateBrushWithOpacity(
                RenderingUtils.GetResourceBrush("AppBackgroundBrush", "#FFFFFFFF"), 0.8);

            var formattedText = GetCachedFormattedText(text, fontSize, textBrush, useChineseFont);

            // 计算居中位置
            var textPosition = new Point(
                noteRect.X + (noteRect.Width - formattedText.Width) / 2,
                noteRect.Y + (noteRect.Height - formattedText.Height) / 2);

            // 绘制背景
            var textBounds = new Rect(
                textPosition.X - 2,
                textPosition.Y - 1,
                formattedText.Width + 4,
                formattedText.Height + 2);
            
            context.DrawRectangle(backgroundBrush, null, textBounds);

            // 绘制文本
            context.DrawText(formattedText, textPosition);
        }

        /// <summary>
        /// 快速绘制音符音高文本（使用预置的音符名称）
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="pitch">MIDI音高值（0-127）</param>
        /// <param name="noteRect">音符矩形区域</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="textBrush">文本画刷，如果为null则使用默认颜色</param>
        /// <param name="backgroundBrush">背景画刷，如果为null则使用默认颜色</param>
        public static void DrawNotePitchText(DrawingContext context, int pitch, Rect noteRect,
            double fontSize = 9, IBrush? textBrush = null, IBrush? backgroundBrush = null)
        {
            if (pitch < 0 || pitch > 127) return;

            var noteName = _precomputedNoteNames[pitch];
            DrawNoteText(context, noteName, noteRect, fontSize, textBrush, backgroundBrush);
        }

        /// <summary>
        /// 获取缓存的格式化文本，提升性能
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

                // 创建新的格式化文本
                var typeface = useChineseFont ? _chineseTypeface : _defaultTypeface;
                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    textBrush);

                // 添加到缓存（限制缓存大小）
                if (_textCache.Count < MaxCacheSize)
                {
                    _textCache[cacheKey] = formattedText;
                }
                else
                {
                    // 缓存已满，清理一半缓存
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
        /// 清理文本缓存（可选的性能管理方法）
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