using SkiaSharp;
using System.Collections.Generic;

namespace ImageToMidi.Models
{
    public class BitmapPalette
    {
        public List<SKColor> Colors { get; }

        public BitmapPalette(List<SKColor> colors)
        {
            Colors = colors ?? new List<SKColor>();
        }

        public BitmapPalette(IEnumerable<SKColor> colors)
        {
            Colors = new List<SKColor>(colors);
        }
    }
}
