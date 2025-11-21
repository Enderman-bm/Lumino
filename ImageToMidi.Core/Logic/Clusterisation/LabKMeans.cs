using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using ImageToMidi.Models;

namespace ImageToMidi.Logic.Clusterisation
{
    public static partial class Clusterisation
    {
        public static BitmapPalette LabKMeans(BitmapPalette palette, byte[] image, double threshold, out double lastChange, int maxIterations)
        {
            int clusterCount = palette.Colors.Count;
            var positions = new double[clusterCount][];
            for (int i = 0; i < clusterCount; i++)
            {
                var c = palette.Colors[i];
                positions[i] = RgbToLab(c.Red, c.Green, c.Blue);
            }

            int sampleStep = image.Length > 1024 * 1024 ? 8 : 4;
            int sampleCountEstimate = image.Length / (4 * sampleStep);
            var sampleIndices = new List<int>(sampleCountEstimate);
            for (int i = 0; i < image.Length; i += 4 * sampleStep)
            {
                if (image[i + 3] > 128)
                    sampleIndices.Add(i);
            }
            int sampleCount = sampleIndices.Count;

            int threadCount = Environment.ProcessorCount;
            var localSums = new double[threadCount][,];
            var localCounts = new int[threadCount][];
            for (int t = 0; t < threadCount; t++)
            {
                localSums[t] = new double[clusterCount, 3];
                localCounts[t] = new int[clusterCount];
            }
            var sums = new double[clusterCount, 3];
            var pointCounts = new int[clusterCount];

            lastChange = 0;
            var threadPartition = new int[threadCount + 1];
            int chunk = sampleCount / threadCount;
            for (int t = 0; t < threadCount; t++)
                threadPartition[t] = t * chunk;
            threadPartition[threadCount] = sampleCount;

            var labCache = new double[sampleCount][];
            for (int i = 0; i < sampleCount; i++)
            {
                int idx = sampleIndices[i];
                labCache[i] = RgbToLab(image[idx + 2], image[idx + 1], image[idx + 0]);
            }

            for (int iter = 0; iter < maxIterations; iter++)
            {
                Array.Clear(sums, 0, sums.Length);
                Array.Clear(pointCounts, 0, pointCounts.Length);
                for (int t = 0; t < threadCount; t++)
                {
                    Array.Clear(localSums[t], 0, localSums[t].Length);
                    Array.Clear(localCounts[t], 0, localCounts[t].Length);
                }

                Parallel.For(0, threadCount, t =>
                {
                    var localSum = localSums[t];
                    var localCount = localCounts[t];
                    int start = threadPartition[t], end = threadPartition[t + 1];
                    for (int idx = start; idx < end; idx++)
                    {
                        var lab = labCache[idx];
                        int minid = 0;
                        double min = double.MaxValue;
                        for (int c = 0; c < clusterCount; c++)
                        {
                            double dl = lab[0] - positions[c][0];
                            double da = lab[1] - positions[c][1];
                            double db = lab[2] - positions[c][2];
                            double distsqr = dl * dl + da * da + db * db;
                            if (distsqr < min)
                            {
                                min = distsqr;
                                minid = c;
                            }
                        }
                        localSum[minid, 0] += lab[0];
                        localSum[minid, 1] += lab[1];
                        localSum[minid, 2] += lab[2];
                        localCount[minid]++;
                    }
                });

                for (int t = 0; t < threadCount; t++)
                {
                    var localSum = localSums[t];
                    var localCount = localCounts[t];
                    for (int c = 0; c < clusterCount; c++)
                    {
                        sums[c, 0] += localSum[c, 0];
                        sums[c, 1] += localSum[c, 1];
                        sums[c, 2] += localSum[c, 2];
                        pointCounts[c] += localCount[c];
                    }
                }

                double maxChange = 0;
                for (int i = 0; i < clusterCount; i++)
                {
                    if (pointCounts[i] > 0)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            double mean = sums[i, c] / pointCounts[i];
                            double change = Math.Abs(mean - positions[i][c]);
                            if (change > maxChange) maxChange = change;
                            positions[i][c] = mean;
                        }
                    }
                    else
                    {
                        double maxDist = -1;
                        int farthestIdx = 0;
                        for (int si = 0; si < sampleCount; si++)
                        {
                            var lab = labCache[si];
                            double minDist = double.MaxValue;
                            for (int j = 0; j < clusterCount; j++)
                            {
                                if (i == j) continue;
                                double dl = lab[0] - positions[j][0];
                                double da = lab[1] - positions[j][1];
                                double db = lab[2] - positions[j][2];
                                double dist = dl * dl + da * da + db * db;
                                if (dist < minDist)
                                    minDist = dist;
                            }
                            if (minDist > maxDist)
                            {
                                maxDist = minDist;
                                farthestIdx = si;
                            }
                        }
                        positions[i][0] = labCache[farthestIdx][0];
                        positions[i][1] = labCache[farthestIdx][1];
                        positions[i][2] = labCache[farthestIdx][2];
                    }
                }
                lastChange = maxChange;
                if (maxChange < threshold) break;
            }

            var newcol = new List<SKColor>(clusterCount);
            for (int i = 0; i < clusterCount; i++)
            {
                var rgb = LabToRgb(positions[i][0], positions[i][1], positions[i][2]);
                newcol.Add(new SKColor(rgb[0], rgb[1], rgb[2]));
            }
            return new BitmapPalette(newcol);
        }

        private static double[] RgbToLab(int r, int g, int b)
        {
            double sr = r / 255.0, sg = g / 255.0, sb = b / 255.0;
            sr = sr > 0.04045 ? Math.Pow((sr + 0.055) / 1.055, 2.4) : sr / 12.92;
            sg = sg > 0.04045 ? Math.Pow((sg + 0.055) / 1.055, 2.4) : sg / 12.92;
            sb = sb > 0.04045 ? Math.Pow((sb + 0.055) / 1.055, 2.4) : sb / 12.92;
            double x = sr * 0.4124 + sg * 0.3576 + sb * 0.1805;
            double y = sr * 0.2126 + sg * 0.7152 + sb * 0.0722;
            double z = sr * 0.0193 + sg * 0.1192 + sb * 0.9505;
            double xr = x / 0.95047, yr = y / 1.00000, zr = z / 1.08883;
            double fx = xr > 0.008856 ? Math.Pow(xr, 1.0 / 3) : (7.787 * xr) + 16.0 / 116;
            double fy = yr > 0.008856 ? Math.Pow(yr, 1.0 / 3) : (7.787 * yr) + 16.0 / 116;
            double fz = zr > 0.008856 ? Math.Pow(zr, 1.0 / 3) : (7.787 * zr) + 16.0 / 116;
            double l = 116 * fy - 16;
            double a = 500 * (fx - fy);
            double b2 = 200 * (fy - fz);
            return new double[] { l, a, b2 };
        }

        private static byte[] LabToRgb(double l, double a, double b)
        {
            double fy = (l + 16) / 116.0;
            double fx = a / 500.0 + fy;
            double fz = fy - b / 200.0;
            double xr = fx * fx * fx > 0.008856 ? fx * fx * fx : (fx - 16.0 / 116) / 7.787;
            double yr = l > (903.3 * 0.008856) ? Math.Pow((l + 16) / 116.0, 3) : l / 903.3;
            double zr = fz * fz * fz > 0.008856 ? fz * fz * fz : (fz - 16.0 / 116) / 7.787;
            double x = xr * 0.95047, y = yr * 1.00000, z = zr * 1.08883;
            double r = x * 3.2406 + y * -1.5372 + z * -0.4986;
            double g = x * -0.9689 + y * 1.8758 + z * 0.0415;
            double bb = x * 0.0557 + y * -0.2040 + z * 1.0570;
            r = r > 0.0031308 ? 1.055 * Math.Pow(r, 1 / 2.4) - 0.055 : 12.92 * r;
            g = g > 0.0031308 ? 1.055 * Math.Pow(g, 1 / 2.4) - 0.055 : 12.92 * g;
            bb = bb > 0.0031308 ? 1.055 * Math.Pow(bb, 1 / 2.4) - 0.055 : 12.92 * bb;
            return new byte[]
            {
                (byte)Math.Max(0, Math.Min(255, (int)Math.Round(r * 255))),
                (byte)Math.Max(0, Math.Min(255, (int)Math.Round(g * 255))),
                (byte)Math.Max(0, Math.Min(255, (int)Math.Round(bb * 255)))
            };
        }
    }
}
