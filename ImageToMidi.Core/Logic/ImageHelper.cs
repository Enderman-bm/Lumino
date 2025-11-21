using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace ImageToMidi.Core.Logic
{
    public static class ImageHelper
    {
        public static SKBitmap LoadImage(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Image file not found.", filePath);

            // SkiaSharp supports many formats: png, jpg, gif, webp, bmp, etc.
            // For SVG, we might need Svg.Skia or similar, but for now let's stick to raster.
            // If the user needs SVG support in Core, we'd need to add a dependency.
            
            using (var stream = File.OpenRead(filePath))
            {
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap == null)
                    throw new InvalidOperationException($"Failed to decode image: {filePath}");
                
                // Ensure BGRA 8888 for consistency with our processing logic
                if (bitmap.ColorType != SKColorType.Bgra8888)
                {
                    var newBitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using (var canvas = new SKCanvas(newBitmap))
                    {
                        canvas.DrawBitmap(bitmap, 0, 0);
                    }
                    bitmap.Dispose();
                    return newBitmap;
                }

                return bitmap;
            }
        }

        public static byte[] GetPixels(SKBitmap bitmap)
        {
            // SKBitmap.Bytes returns a copy of the pixels.
            // However, we might want to ensure the format is what we expect (BGRA).
            // We already ensured BGRA8888 in LoadImage.
            return bitmap.Bytes;
        }

        public static SKBitmap Rotate(SKBitmap bitmap, int angle)
        {
            if (angle % 90 != 0)
                throw new ArgumentException("Angle must be a multiple of 90.");

            if (angle == 0) return bitmap.Copy();

            var rotated = new SKBitmap(angle % 180 == 0 ? bitmap.Width : bitmap.Height, 
                                       angle % 180 == 0 ? bitmap.Height : bitmap.Width, 
                                       bitmap.ColorType, bitmap.AlphaType);

            using (var canvas = new SKCanvas(rotated))
            {
                canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
                canvas.RotateDegrees(angle);
                canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
                canvas.DrawBitmap(bitmap, 0, 0);
            }
            return rotated;
        }

        public static SKBitmap FlipHorizontal(SKBitmap bitmap)
        {
            var flipped = new SKBitmap(bitmap.Width, bitmap.Height, bitmap.ColorType, bitmap.AlphaType);
            using (var canvas = new SKCanvas(flipped))
            {
                canvas.Scale(-1, 1, bitmap.Width / 2f, bitmap.Height / 2f);
                canvas.DrawBitmap(bitmap, 0, 0);
            }
            return flipped;
        }

        public static SKBitmap ToGrayScale(SKBitmap bitmap)
        {
            var gray = new SKBitmap(bitmap.Width, bitmap.Height, bitmap.ColorType, bitmap.AlphaType);
            using (var canvas = new SKCanvas(gray))
            {
                using (var paint = new SKPaint())
                {
                    paint.ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                    {
                        0.21f, 0.72f, 0.07f, 0, 0,
                        0.21f, 0.72f, 0.07f, 0, 0,
                        0.21f, 0.72f, 0.07f, 0, 0,
                        0,     0,     0,     1, 0
                    });
                    canvas.DrawBitmap(bitmap, 0, 0, paint);
                }
            }
            return gray;
        }

        public static SKBitmap[] LoadFrames(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Image file not found.", filePath);

            using (var stream = File.OpenRead(filePath))
            using (var codec = SKCodec.Create(stream))
            {
                if (codec == null)
                    throw new InvalidOperationException($"Failed to create codec for: {filePath}");

                int frameCount = codec.FrameCount;
                if (frameCount <= 1)
                {
                    return new[] { LoadImage(filePath) };
                }

                var frames = new SKBitmap[frameCount];
                var info = codec.Info;

                for (int i = 0; i < frameCount; i++)
                {
                    var bitmap = new SKBitmap(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    var options = new SKCodecOptions(i);
                    
                    // Note: Real GIF decoding might require handling disposal methods and previous frames.
                    // SKCodec does not automatically compose frames.
                    // For a simple implementation, we just decode the frame.
                    // A full implementation would need a canvas to draw previous frames onto.
                    
                    var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels(), options);
                    if (result != SKCodecResult.Success)
                    {
                        bitmap.Dispose();
                        // If one frame fails, we might want to stop or continue.
                        // For now, let's just return what we have or throw.
                        continue; 
                    }
                    frames[i] = bitmap;
                }
                
                // Filter out nulls if any frame failed
                return System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(frames, f => f != null));
            }
        }
    }
}
