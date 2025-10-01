using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using DominoNext.Views.Rendering.Utils;

namespace DominoNext.Views.Rendering.Notes
{
    /// <summary>
    /// 洋葱皮渲染器 - 显示其他音轨的音符
    /// </summary>
    public class OnionSkinRenderer
    {
        // 圆角半径
        private const double CORNER_RADIUS = 2.0;
        
        // 洋葱皮音符透明度
        private const double ONION_SKIN_OPACITY = 0.4;
        
        // 颜色缓存，避免重复创建相同颜色
        private readonly Dictionary<int, Color> _colorCache = new Dictionary<int, Color>();
        
        // 画刷缓存，避免重复创建相同画刷
        private readonly Dictionary<Color, IBrush> _brushCache = new Dictionary<Color, IBrush>();
        
        // 用于分组音符的字典：颜色 -> 音符矩形列表
        private readonly Dictionary<Color, List<RoundedRect>> _noteGroups = new Dictionary<Color, List<RoundedRect>>();

        /// <summary>
        /// 渲染洋葱皮效果 - 显示其他音轨的音符
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect, Rect viewport)
        {
            // 清空缓存和分组数据
            ClearBrushCache();
            ClearGroups();
            
            // 收集并分组需要绘制的音符
            CollectAndGroupNotes(viewModel, calculateNoteRect, viewport);
            
            // 批量绘制每个颜色组的音符
            foreach (var group in _noteGroups)
            {
                var color = group.Key;
                var rects = group.Value;
                
                if (rects.Count > 0)
                {
                    // 获取缓存的画刷并批量绘制
                    var brush = GetCachedBrush(color);
                    foreach (var rect in rects)
                    {
                        context.DrawRectangle(brush, null, rect);
                    }
                }
            }
        }
        
        /// <summary>
        /// 收集并分组需要绘制的音符
        /// </summary>
        private void CollectAndGroupNotes(PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect, Rect viewport)
        {
            // 为了避免线程访问问题，我们预先计算所有音符的矩形
            var noteRectMap = new Dictionary<NoteViewModel, Rect>();
            foreach (var note in viewModel.Notes)
            {
                noteRectMap[note] = calculateNoteRect(note);
            }

            // 现在可以安全地并行处理
            Parallel.ForEach(viewModel.Notes, note =>
            {
                // 只处理非当前音轨的音符
                if (note.TrackIndex != viewModel.CurrentTrackIndex)
                {
                    var noteRect = noteRectMap[note];
                    
                    // 只处理视口内的音符（视口裁剪优化）
                    if (noteRect.Intersects(viewport) && noteRect.Width > 0 && noteRect.Height > 0)
                    {
                        // 根据音轨索引获取颜色
                        var trackColor = GetTrackColor(note.TrackIndex);
                        
                        // 将音符添加到对应颜色组（需要线程安全操作）
                        lock (_noteGroups)
                        {
                            if (!_noteGroups.TryGetValue(trackColor, out var rects))
                            {
                                rects = new List<RoundedRect>();
                                _noteGroups[trackColor] = rects;
                            }
                            
                            rects.Add(new RoundedRect(noteRect, CORNER_RADIUS));
                        }
                    }
                }
            });
        }
        
        /// <summary>
        /// 获取缓存的画刷
        /// </summary>
        private IBrush GetCachedBrush(Color color)
        {
            if (!_brushCache.TryGetValue(color, out var brush))
            {
                brush = new SolidColorBrush(color, ONION_SKIN_OPACITY);
                _brushCache[color] = brush;
            }
            return brush;
        }
        
        /// <summary>
        /// 清空画刷缓存
        /// </summary>
        private void ClearBrushCache()
        {
            _brushCache.Clear();
        }
        
        /// <summary>
        /// 清空分组数据
        /// </summary>
        private void ClearGroups()
        {
            foreach (var group in _noteGroups)
            {
                group.Value.Clear();
            }
            _noteGroups.Clear();
        }
        
        /// <summary>
        /// 根据音轨索引获取颜色
        /// </summary>
        private Color GetTrackColor(int trackIndex)
        {
            // 检查颜色缓存
            if (_colorCache.TryGetValue(trackIndex, out var cachedColor))
            {
                return cachedColor;
            }
            
            // 使用更深、更鲜明的颜色列表，为不同音轨分配不同颜色
            var colors = new[]
            {
                Color.FromRgb(235, 64, 52),   // 深红色
                Color.FromRgb(245, 136, 67),  // 深橙色
                Color.FromRgb(230, 184, 0),   // 深黄色
                Color.FromRgb(106, 184, 79),  // 深绿色
                Color.FromRgb(65, 170, 184),  // 深青色
                Color.FromRgb(65, 105, 225),  // 皇家蓝
                Color.FromRgb(123, 50, 178),  // 深紫色
                Color.FromRgb(184, 67, 123),  // 深粉色
            };
            
            // 循环使用颜色
            var color = colors[trackIndex % colors.Length];
            
            // 缓存颜色
            _colorCache[trackIndex] = color;
            
            return color;
        }
        
        /// <summary>
        /// 清除颜色缓存（在需要时调用）
        /// </summary>
        public void ClearCache()
        {
            _colorCache.Clear();
        }
    }
}