using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Lumino.ViewModels.Editor;
using Lumino.Views.Rendering.Utils;
using EnderDebugger;

namespace Lumino.Views.Rendering.Notes
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

        /// <summary>
        /// 渲染洋葱皮效果 - 显示其他音轨的音符
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect, Rect viewport)
        {
            // 在 UI 线程上提取数据（假设调用者已经在 UI 线程）
            var notes = viewModel.Notes.Where(note => note.TrackIndex != viewModel.CurrentTrackIndex).ToList();
            double scrollOffset = viewModel.CurrentScrollOffset;

            // 分组音符并计算矩形
            var noteGroups = new Dictionary<Color, List<RoundedRect>>();

            foreach (var note in notes)
            {
                var noteRect = calculateNoteRect(note);

                // 应用滚动偏移
                var adjustedRect = new Rect(
                    noteRect.X - scrollOffset,
                    noteRect.Y,
                    noteRect.Width,
                    noteRect.Height
                );

                // 检查是否在视口内
                if (adjustedRect.Intersects(viewport) && adjustedRect.Width > 0 && adjustedRect.Height > 0)
                {
                    var trackColor = GetTrackColor(note.TrackIndex);

                    if (!noteGroups.ContainsKey(trackColor))
                    {
                        noteGroups[trackColor] = new List<RoundedRect>();
                    }

                    noteGroups[trackColor].Add(new RoundedRect(adjustedRect, CORNER_RADIUS));
                }
            }

            // 在渲染线程上绘制
            foreach (var group in noteGroups)
            {
                var brush = GetCachedBrush(group.Key);
                foreach (var rect in group.Value)
                {
                    context.DrawRectangle(brush, null, rect);
                }
            }
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

            // 循环使用颜色，确保索引为非负数
            var colorIndex = trackIndex % colors.Length;
            if (colorIndex < 0)
                colorIndex += colors.Length;
            var color = colors[colorIndex];

            // 缓存颜色
            _colorCache[trackIndex] = color;

            return color;
        }

        /// <summary>
        /// 清除缓存（在需要时调用）
        /// </summary>
        public void ClearCache()
        {
            _colorCache.Clear();
            _brushCache.Clear();
        }
    }
}