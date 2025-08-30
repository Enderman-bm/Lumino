using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoNext.Renderers
{
    /// <summary>
    /// 洋葱皮渲染器 - 通过绘制其他轨道的半透明音符提供视觉辅助
    /// 修复：确保与主音符渲染器使用相同的缩放计算逻辑
    /// </summary>
    public class OnionSkinRenderer
    {
        private const double DefaultOpacity = 0.3;
        private const double MinOpacity = 0.1;
        private const double MaxOpacity = 0.8;

        /// <summary>
        /// 渲染洋葱皮效果
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="pianoRoll">钢琴卷帘ViewModel</param>
        /// <param name="bounds">绘制边界</param>
        public void Render(DrawingContext context, PianoRollViewModel pianoRoll, Rect bounds)
        {
            if (!pianoRoll.IsOnionSkinEnabled || pianoRoll.SelectedTrack == null)
                return;

            var selectedTrack = pianoRoll.SelectedTrack;
            // 修复：正确获取需要显示洋葱皮效果的轨道（不包括当前选中的轨道）
            var otherTracks = pianoRoll.Tracks.Where(t => t != selectedTrack && t.IsOnionSkinSelected).ToList();

            if (!otherTracks.Any())
                return;

            foreach (var track in otherTracks)
            {
                RenderTrackOnionSkin(context, pianoRoll, track, bounds);
            }
        }

        /// <summary>
        /// 渲染单个轨道的洋葱皮效果
        /// </summary>
        private void RenderTrackOnionSkin(DrawingContext context, PianoRollViewModel pianoRoll, TrackViewModel track, Rect bounds)
        {
            var opacity = Math.Max(MinOpacity, Math.Min(MaxOpacity, pianoRoll.OnionSkinOpacity));
            
            // 根据轨道索引和颜色选择不同颜色
            var baseColor = GetTrackColor(track, pianoRoll.Tracks.IndexOf(track));
            var onionSkinColor = Color.FromArgb((byte)(255 * opacity), baseColor.R, baseColor.G, baseColor.B);
            var brush = new SolidColorBrush(onionSkinColor);
            var pen = new Pen(brush, 1.0);

            foreach (var note in track.Notes)
            {
                // 修复：使用与主音符渲染器相同的计算方法
                var noteRect = GetNoteRect(note, pianoRoll);

                // 只绘制在可视区域内的音符
                if (noteRect.Intersects(bounds))
                {
                    // 绘制半透明的音符矩形
                    context.DrawRectangle(brush, pen, noteRect);
                    
                    // 在选中的音符上绘制标识字符（轨道名称的首字母）
                    if (noteRect.Width > 20 && noteRect.Height > 8)
                    {
                        DrawTrackIdentifier(context, track, noteRect, onionSkinColor);
                    }
                }
            }
        }

        /// <summary>
        /// 获取轨道颜色
        /// </summary>
        private Color GetTrackColor(TrackViewModel track, int trackIndex)
        {
            // 为不同轨道提供不同颜色
            var colors = new[]
            {
                Colors.CornflowerBlue,
                Colors.LightGreen,
                Colors.Orange,
                Colors.Purple,
                Colors.Red,
                Colors.Yellow,
                Colors.Cyan,
                Colors.Pink,
                Colors.Brown,
                Colors.Gray
            };

            return colors[trackIndex % colors.Length];
        }

        /// <summary>
        /// 获取音符的绝对位置 - 修复：使用与主音符渲染器相同的缩放计算逻辑
        /// </summary>
        private Rect GetNoteRect(NoteViewModel note, PianoRollViewModel pianoRoll)
        {
            // 修复：使用音符自身的缓存计算方法，确保与主音符渲染器一致
            var x = note.GetX(pianoRoll.Zoom, pianoRoll.PixelsPerTick);
            var y = note.GetY(pianoRoll.KeyHeight);
            var width = note.GetWidth(pianoRoll.Zoom, pianoRoll.PixelsPerTick);
            var height = note.GetHeight(pianoRoll.KeyHeight);

            return new Rect(x, y, Math.Max(1, width), height);
        }

        /// <summary>
        /// 绘制轨道标识符
        /// </summary>
        private void DrawTrackIdentifier(DrawingContext context, TrackViewModel track, Rect noteRect, Color color)
        {
            var identifier = !string.IsNullOrEmpty(track.Name) ? track.Name.Substring(0, 1).ToUpper() : "T";
            var textColor = GetContrastColor(color);
            var textBrush = new SolidColorBrush(textColor);
            
            var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
            var fontSize = Math.Min(noteRect.Height * 0.6, 10);
            
            var formattedText = new FormattedText(
                identifier,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                textBrush);

            var textPosition = new Point(
                noteRect.X + (noteRect.Width - formattedText.Width) / 2,
                noteRect.Y + (noteRect.Height - formattedText.Height) / 2);

            context.DrawText(formattedText, textPosition);
        }

        /// <summary>
        /// 获取对比色以确保文本可读性
        /// </summary>
        private Color GetContrastColor(Color color)
        {
            // 计算亮度
            var brightness = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return brightness > 0.5 ? Colors.Black : Colors.White;
        }

        /// <summary>
        /// 渲染洋葱皮帧指示器（在时间轴上的标记线）
        /// </summary>
        public void RenderFrameIndicators(DrawingContext context, PianoRollViewModel pianoRoll, Rect bounds)
        {
            if (!pianoRoll.IsOnionSkinEnabled || pianoRoll.SelectedTrack == null)
                return;

            var currentPosition = pianoRoll.TimelinePosition;
            var frameIndicatorBrush = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));
            var pen = new Pen(frameIndicatorBrush, 1.0, new DashStyle(new double[] { 2, 2 }, 0));

            // 绘制过去帧指示器
            for (int i = 1; i <= pianoRoll.OnionSkinPreviousFrames; i++)
            {
                var x = currentPosition - (i * pianoRoll.BeatWidth);
                if (x >= bounds.Left && x <= bounds.Right)
                {
                    context.DrawLine(pen, new Point(x, bounds.Top), new Point(x, bounds.Bottom));
                }
            }

            // 绘制未来帧指示器
            for (int i = 1; i <= pianoRoll.OnionSkinNextFrames; i++)
            {
                var x = currentPosition + (i * pianoRoll.BeatWidth);
                if (x >= bounds.Left && x <= bounds.Right)
                {
                    context.DrawLine(pen, new Point(x, bounds.Top), new Point(x, bounds.Bottom));
                }
            }
        }
    }
}