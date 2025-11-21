using System;
using System.Collections.Generic;
using SkiaSharp;

namespace ImageToMidi.Logic.Clusterisation
{
    public static class FloydSteinbergDither
    {
        /// <summary>
        /// Perform Floyd-Steinberg dithering on BGRA image data.
        /// </summary>
        /// <param name="src">Original BGRA byte array</param>
        /// <param name="width">Image width</param>
        /// <param name="height">Image height</param>
        /// <param name="palette">Target color palette</param>
        /// <param name="strength">Dithering strength</param>
        /// <param name="serpentine">Use serpentine scanning</param>
        /// <returns>Dithered BGRA byte array</returns>
        public static byte[] Dither(
            byte[] src,
            int width,
            int height,
            List<SKColor> palette,
            double strength = 1.0,
            bool serpentine = true
            )
        {
            int stride = width * 4;
            byte[] dst = new byte[src.Length];
            Buffer.BlockCopy(src, 0, dst, 0, src.Length);

            // Error diffusion buffers
            float[] errorCurr = new float[width * 3];
            float[] errorNext = new float[width * 3];

            // Pre-cache palette as float arrays for faster access
            int palLen = palette.Count;
            float[][] pal = new float[palLen][];
            for (int i = 0; i < palLen; i++)
            {
                var c = palette[i];
                pal[i] = new float[] { c.Red, c.Green, c.Blue };
            }

            // Cache for color mapping
            var colorCache = new Dictionary<int, int>();

            for (int y = 0; y < height; y++)
            {
                bool leftToRight = serpentine ? (y % 2 == 0) : true;
                int x0 = leftToRight ? 0 : width - 1;
                int x1 = leftToRight ? width : -1;
                int dx = leftToRight ? 1 : -1;

                for (int x = x0; x != x1; x += dx)
                {
                    int idx = (y * width + x) * 4;
                    // Skip transparent pixels (assuming alpha < 128 is transparent)
                    if (dst[idx + 3] < 128) continue;

                    // Current pixel + error
                    float r = dst[idx + 2] + errorCurr[x * 3 + 0];
                    float g = dst[idx + 1] + errorCurr[x * 3 + 1];
                    float b = dst[idx + 0] + errorCurr[x * 3 + 2];

                    // Clamp to 0-255
                    r = Math.Min(255, Math.Max(0, r));
                    g = Math.Min(255, Math.Max(0, g));
                    b = Math.Min(255, Math.Max(0, b));

                    // Find nearest color
                    int rgbKey = ((int)r << 16) | ((int)g << 8) | (int)b;
                    int best;
                    if (!colorCache.TryGetValue(rgbKey, out best))
                    {
                        float minDist = float.MaxValue;
                        best = 0;
                        for (int i = 0; i < palLen; i++)
                        {
                            float dr = r - pal[i][0];
                            float dg = g - pal[i][1];
                            float db = b - pal[i][2];
                            float dist = dr * dr + dg * dg + db * db;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                best = i;
                            }
                        }
                        colorCache[rgbKey] = best;
                    }

                    byte nr = (byte)pal[best][0];
                    byte ng = (byte)pal[best][1];
                    byte nb = (byte)pal[best][2];

                    // Write back
                    dst[idx + 2] = nr;
                    dst[idx + 1] = ng;
                    dst[idx + 0] = nb;

                    // Calculate error
                    float errR = (float)((r - nr) * strength);
                    float errG = (float)((g - ng) * strength);
                    float errB = (float)((b - nb) * strength);

                    // Distribute error
                    // Right
                    int xr = x + dx;
                    if (xr >= 0 && xr < width)
                    {
                        errorCurr[xr * 3 + 0] += errR * 7 / 16f;
                        errorCurr[xr * 3 + 1] += errG * 7 / 16f;
                        errorCurr[xr * 3 + 2] += errB * 7 / 16f;
                    }
                    // Next line
                    if (y + 1 < height)
                    {
                        // Bottom-Left (relative to scan direction)
                        int xl = x - dx;
                        if (xl >= 0 && xl < width)
                        {
                            errorNext[xl * 3 + 0] += errR * 3 / 16f;
                            errorNext[xl * 3 + 1] += errG * 3 / 16f;
                            errorNext[xl * 3 + 2] += errB * 3 / 16f;
                        }
                        // Bottom
                        errorNext[x * 3 + 0] += errR * 5 / 16f;
                        errorNext[x * 3 + 1] += errG * 5 / 16f;
                        errorNext[x * 3 + 2] += errB * 5 / 16f;
                        // Bottom-Right (relative to scan direction)
                        if (xr >= 0 && xr < width)
                        {
                            errorNext[xr * 3 + 0] += errR * 1 / 16f;
                            errorNext[xr * 3 + 1] += errG * 1 / 16f;
                            errorNext[xr * 3 + 2] += errB * 1 / 16f;
                        }
                    }
                }
                // Swap buffers
                var tmp = errorCurr;
                errorCurr = errorNext;
                errorNext = tmp;
                Array.Clear(errorNext, 0, errorNext.Length);
            }
            return dst;
        }
    }
}
