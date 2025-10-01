using System;
using System.Collections.Generic;
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

        /// <summary>
        /// 渲染洋葱皮效果 - 显示其他音轨的音符
        /// </summary>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Func<NoteViewModel, Rect> calculateNoteRect)
        {
            // 遍历所有音符，渲染非当前音轨的音符作为洋葱皮
            foreach (var note in viewModel.Notes)
            {
                // 只渲染非当前音轨的音符
                if (note.TrackIndex != viewModel.CurrentTrackIndex)
                {
                    var noteRect = calculateNoteRect(note);
                    if (noteRect.Width > 0 && noteRect.Height > 0)
                    {
                        // 根据音轨索引选择颜色，确保不同音轨使用不同颜色
                        var trackColor = GetTrackColor(note.TrackIndex);
                        var brush = new SolidColorBrush(trackColor, ONION_SKIN_OPACITY);
                        
                        var roundedRect = new RoundedRect(noteRect, CORNER_RADIUS);
                        context.DrawRectangle(brush, null, roundedRect);
                    }
                }
            }
        }
        
        /// <summary>
        /// 根据音轨索引获取颜色
        /// </summary>
        private Color GetTrackColor(int trackIndex)
        {
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
            return colors[trackIndex % colors.Length];
        }
    }
}