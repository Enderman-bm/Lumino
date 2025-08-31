using Avalonia;
using Avalonia.Media;
using DominoNext.ViewModels.Editor;
using System;

namespace DominoNext.Renderers
{
    /// <summary>
    /// 横向网格渲染器 - 负责绘制钢琴键背景和横向网格线
    /// </summary>
    public class HorizontalGridRenderer
    {
        /// <summary>
        /// 渲染横向网格线和钢琴键背景
        /// </summary>
        /// <param name="context">绘制上下文</param>
        /// <param name="viewModel">钢琴卷帘ViewModel</param>
        /// <param name="bounds">绘制边界</param>
        /// <param name="viewport">可视区域</param>
        public void Render(DrawingContext context, PianoRollViewModel viewModel, Rect bounds, Rect viewport)
        {
            if (viewModel == null) return;

            var keyHeight = viewModel.KeyHeight;
            var startKey = Math.Max(0, (int)(viewport.Y / keyHeight));
            var endKey = Math.Min(127, (int)((viewport.Y + viewport.Height) / keyHeight) + 1);

            for (int i = startKey; i <= endKey; i++)
            {
                var midiNote = 127 - i;
                var y = i * keyHeight;
                var isBlackKey = viewModel.IsBlackKey(midiNote);

                // 绘制键背景
                var rowRect = new Rect(0, y, bounds.Width, keyHeight);
                var rowBrush = isBlackKey ? GetBlackKeyRowBrush(viewModel) : GetWhiteKeyRowBrush();
                context.DrawRectangle(rowBrush, null, rowRect);

                // 绘制分隔线
                var isOctaveBoundary = midiNote % 12 == 0;
                var pen = isOctaveBoundary 
                    ? GetResourcePen("BorderLineBlackBrush", "#FF000000", 1.5)
                    : GetResourcePen("GridLineBrush", "#FFbad2f2", 0.5);
                
                context.DrawLine(pen, new Point(0, y + keyHeight), new Point(bounds.Width, y + keyHeight));
            }
        }

        private IBrush GetWhiteKeyRowBrush()
        {
            return GetResourceBrush("KeyWhiteBrush", "#FFFFFFFF");
        }

        private IBrush GetBlackKeyRowBrush(PianoRollViewModel viewModel)
        {
            var mainBg = GetResourceBrush("MainCanvasBackgroundBrush", "#FFFFFFFF");
            
            if (mainBg is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                var brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255.0;
                
                if (brightness < 0.5)
                {
                    return new SolidColorBrush(Color.FromArgb(255,
                        (byte)Math.Min(255, color.R + 15),
                        (byte)Math.Min(255, color.G + 15),
                        (byte)Math.Min(255, color.B + 15)));
                }
                else
                {
                    return new SolidColorBrush(Color.FromArgb(255,
                        (byte)Math.Max(0, color.R - 25),
                        (byte)Math.Max(0, color.G - 25),
                        (byte)Math.Max(0, color.B - 25)));
                }
            }
            
            return GetResourceBrush("AppBackgroundBrush", "#FFedf3fe");
        }

        private IBrush GetResourceBrush(string key, string fallbackHex)
        {
            try
            {
                if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var obj) == true && obj is IBrush brush)
                    return brush;
            }
            catch { }

            return new SolidColorBrush(Color.Parse(fallbackHex));
        }

        private IPen GetResourcePen(string brushKey, string fallbackHex, double thickness = 1, DashStyle? dashStyle = null)
        {
            var brush = GetResourceBrush(brushKey, fallbackHex);
            var pen = new Pen(brush, thickness);
            if (dashStyle != null)
                pen.DashStyle = dashStyle;
            return pen;
        }
    }
}