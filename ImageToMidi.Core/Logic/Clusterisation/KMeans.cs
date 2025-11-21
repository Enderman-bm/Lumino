using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using ImageToMidi.Models;
using System.Threading;

namespace ImageToMidi.Logic.Clusterisation
{
    public static partial class Clusterisation
    {
        public static BitmapPalette KMeans(
            BitmapPalette palette,
            byte[] image,
            double threshold,
            out double lastChange,
            int maxIterations,
            int maxSamples = 20000,
            int seed = 0,
            Action<double>? progress = null)
        {
            int clusterCount = palette.Colors.Count;
            double[][] positions = new double[clusterCount][];
            for (int i = 0; i < clusterCount; i++)
                positions[i] = new double[3];

            for (int i = 0; i < clusterCount; i++)
            {
                positions[i][0] = palette.Colors[i].Red;
                positions[i][1] = palette.Colors[i].Green;
                positions[i][2] = palette.Colors[i].Blue;
            }

            List<int> sampleIndices = SamplePixelIndices(image, maxSamples, seed);
            if (sampleIndices.Count == 0)
                throw new Exception("No available pixels for sampling");

            int threadCount = Environment.ProcessorCount;
            double[][,] localSums = new double[threadCount][,];
            int[][] localCounts = new int[threadCount][];
            for (int t = 0; t < threadCount; t++)
            {
                localSums[t] = new double[clusterCount, 3];
                localCounts[t] = new int[clusterCount];
            }
            double[,] sums = new double[clusterCount, 3];
            int[] pointCounts = new int[clusterCount];

            lastChange = 0;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                Array.Clear(sums, 0, sums.Length);
                Array.Clear(pointCounts, 0, pointCounts.Length);
                for (int t = 0; t < threadCount; t++)
                {
                    Array.Clear(localSums[t], 0, localSums[t].Length);
                    Array.Clear(localCounts[t], 0, localCounts[t].Length);
                }

                Parallel.For(0, sampleIndices.Count, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, idx =>
                {
                    int threadId = Thread.CurrentThread.ManagedThreadId % threadCount;
                    int i = sampleIndices[idx];
                    double r = image[i + 2];
                    double g = image[i + 1];
                    double b = image[i + 0];
                    double min = 0;
                    bool first = true;
                    int minid = 0;
                    for (int c = 0; c < clusterCount; c++)
                    {
                        double _r = r - positions[c][0];
                        double _g = g - positions[c][1];
                        double _b = b - positions[c][2];
                        double distsqr = _r * _r + _g * _g + _b * _b;
                        if (distsqr < min || first)
                        {
                            min = distsqr;
                            first = false;
                            minid = c;
                        }
                    }
                    localSums[threadId][minid, 0] += r;
                    localSums[threadId][minid, 1] += g;
                    localSums[threadId][minid, 2] += b;
                    localCounts[threadId][minid]++;
                });

                for (int t = 0; t < threadCount; t++)
                {
                    for (int c = 0; c < clusterCount; c++)
                    {
                        sums[c, 0] += localSums[t][c, 0];
                        sums[c, 1] += localSums[t][c, 1];
                        sums[c, 2] += localSums[t][c, 2];
                        pointCounts[c] += localCounts[t][c];
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
                        int farthestIdx = sampleIndices[0];
                        for (int si = 0; si < sampleIndices.Count; si++)
                        {
                            int idx = sampleIndices[si];
                            double r = image[idx + 2];
                            double g = image[idx + 1];
                            double b = image[idx + 0];

                            double minDist = double.MaxValue;
                            for (int j = 0; j < clusterCount; j++)
                            {
                                if (i == j) continue;
                                double dr = r - positions[j][0];
                                double dg = g - positions[j][1];
                                double db = b - positions[j][2];
                                double dist = dr * dr + dg * dg + db * db;
                                if (dist < minDist)
                                    minDist = dist;
                            }
                            if (minDist > maxDist)
                            {
                                maxDist = minDist;
                                farthestIdx = idx;
                            }
                        }
                        positions[i][0] = image[farthestIdx + 2];
                        positions[i][1] = image[farthestIdx + 1];
                        positions[i][2] = image[farthestIdx + 0];
                    }
                }
                lastChange = maxChange;

                progress?.Invoke((double)(iter + 1) / maxIterations);

                if (maxChange < threshold) break;
            }

            var newcol = new List<SKColor>();
            for (int i = 0; i < clusterCount; i++)
            {
                newcol.Add(new SKColor((byte)positions[i][0], (byte)positions[i][1], (byte)positions[i][2]));
            }
            return new BitmapPalette(newcol);
        }
    }
}
