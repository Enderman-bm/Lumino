using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Lumino.ViewModels;
using Lumino.ViewModels.Editor;
using Lumino.ViewModels.Editor.Enums;
using Lumino.Views.Rendering.Utils;
using Lumino.Views.Rendering.Adapters;
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
            Render(context, null, viewModel, calculateNoteRect, viewport);
        }

        /// <summary>
        /// 渲染洋葱皮效果，支持Vulkan适配器
        /// </summary>
        public void Render(DrawingContext context, VulkanDrawingContextAdapter? vulkanAdapter, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect, Rect viewport)
        {
            // 根据洋葱皮模式确定要显示的音轨索引
            var trackIndicesToShow = GetTrackIndicesToShow(viewModel);
            
            // 快速检查：如果没有要显示的音轨，直接返回
            if (trackIndicesToShow.Count == 0)
                return;
            
            double scrollOffset = viewModel.CurrentScrollOffset;
            
            // 计算可视区域范围（像素坐标转换为四分音符位置范围）
            double baseWidth = viewModel.BaseQuarterNoteWidth;
            if (baseWidth <= 0) baseWidth = 40; // 防止除零
            double viewportStartQuarter = scrollOffset / baseWidth;
            double viewportEndQuarter = (scrollOffset + viewport.Width) / baseWidth;

            // 分组音符并计算矩形 - 使用预分配字典避免频繁扩容
            var noteGroups = new Dictionary<Color, List<RoundedRect>>(16);

            // 直接遍历 Notes 集合，避免 ToList() 和 Where() 的额外开销
            foreach (var note in viewModel.Notes)
            {
                // 快速过滤：检查音轨索引
                if (!trackIndicesToShow.Contains(note.TrackIndex))
                    continue;
                
                // 快速过滤：检查音符是否可能在可视区域内（基于四分音符位置）
                double noteStart = note.StartPosition.ToDouble();
                double noteEnd = noteStart + note.Duration.ToDouble();
                if (noteEnd < viewportStartQuarter || noteStart > viewportEndQuarter)
                    continue;
                
                var noteRect = calculateNoteRect(note);

                // 应用滚动偏移
                var adjustedRect = new Rect(
                    noteRect.X - scrollOffset,
                    noteRect.Y,
                    noteRect.Width,
                    noteRect.Height
                );

                // 检查是否在视口内
                if (adjustedRect.Width > 0 && adjustedRect.Height > 0 && adjustedRect.Intersects(viewport))
                {
                    var trackColor = GetTrackColor(note.TrackIndex);

                    if (!noteGroups.TryGetValue(trackColor, out var list))
                    {
                        list = new List<RoundedRect>(64); // 预分配容量
                        noteGroups[trackColor] = list;
                    }

                    list.Add(new RoundedRect(adjustedRect, CORNER_RADIUS));
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
        /// 根据洋葱皮模式获取要显示的音轨索引列表
        /// </summary>
        private HashSet<int> GetTrackIndicesToShow(PianoRollViewModel viewModel)
        {
            var result = new HashSet<int>();
            var currentTrackIndex = viewModel.CurrentTrackIndex;
            
            // 优化：直接获取最大音轨索引，避免多次LINQ操作
            int maxTrackIndex = -1;
            foreach (var note in viewModel.Notes)
            {
                if (note.TrackIndex > maxTrackIndex)
                    maxTrackIndex = note.TrackIndex;
            }
            
            var totalTracks = maxTrackIndex + 1;
            if (totalTracks <= 0)
                return result; // 没有音符，直接返回空集合

            // 首先根据模式确定候选音轨
            var candidateTracks = new HashSet<int>();
            
            switch (viewModel.Configuration.OnionSkinMode)
            {
                case OnionSkinMode.PreviousTrack:
                    // 显示上一个音轨
                    if (currentTrackIndex > 0)
                    {
                        candidateTracks.Add(currentTrackIndex - 1);
                    }
                    break;

                case OnionSkinMode.NextTrack:
                    // 显示下一个音轨
                    if (currentTrackIndex < totalTracks - 1)
                    {
                        candidateTracks.Add(currentTrackIndex + 1);
                    }
                    break;

                case OnionSkinMode.AllTracks:
                    // 显示所有其他音轨 - 优化：直接添加索引范围
                    for (int i = 0; i <= maxTrackIndex; i++)
                    {
                        if (i != currentTrackIndex)
                        {
                            candidateTracks.Add(i);
                        }
                    }
                    break;

                case OnionSkinMode.SpecifiedTracks:
                    // 显示指定的音轨（从配置中读取）
                    foreach (var index in viewModel.Configuration.SelectedOnionTrackIndices)
                    {
                        if (index != currentTrackIndex)
                        {
                            candidateTracks.Add(index);
                        }
                    }
                    break;
            }

            // 然后检查每个候选音轨是否启用了洋葱皮
            if (viewModel.TrackSelector != null)
            {
                foreach (var trackIndex in candidateTracks)
                {
                    // TrackNumber 从 1 开始，所以需要加 1
                    var track = viewModel.TrackSelector.Tracks.FirstOrDefault(t => t.TrackNumber - 1 == trackIndex);
                    if (track != null && track.IsOnionSkinEnabled)
                    {
                        result.Add(trackIndex);
                    }
                }
            }
            else
            {
                // 如果没有 TrackSelector，则使用旧逻辑（显示所有候选音轨）
                result = candidateTracks;
            }

            return result;
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