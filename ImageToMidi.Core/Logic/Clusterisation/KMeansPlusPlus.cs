using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using ImageToMidi.Models;
using System.Threading;
using System.Collections.Concurrent;

namespace ImageToMidi.Logic.Clusterisation
{
    public static partial class Clusterisation
    {
        public static BitmapPalette KMeansPlusPlusInit(
            byte[] image,
            int clusterCount,
            int maxSamples = 20000,
            int seed = 0,
            Action<double>? progress = null)
        {
            List<int> sampleIndices = SamplePixelIndices(image, maxSamples, seed);
            if (sampleIndices.Count == 0)
                throw new Exception("No available pixels for sampling");

            Random rand = new Random(seed);
            List<double[]> centers = new List<double[]>();

            int firstIdx = sampleIndices[rand.Next(sampleIndices.Count)];
            centers.Add(new double[] { image[firstIdx + 2], image[firstIdx + 1], image[firstIdx + 0] });

            double[] minDistSq = new double[sampleIndices.Count];

            Parallel.For(0, sampleIndices.Count, i =>
            {
                int idx = sampleIndices[i];
                double r = image[idx + 2];
                double g = image[idx + 1];
                double b = image[idx + 0];
                double dr = r - centers[0][0];
                double dg = g - centers[0][1];
                double db = b - centers[0][2];
                minDistSq[i] = dr * dr + dg * dg + db * db;
            });

            for (int k = 1; k < clusterCount; k++)
            {
                double total = 0;
                object totalLock = new object();
                Parallel.ForEach(
                    Partitioner.Create(0, minDistSq.Length),
                    () => 0.0,
                    (range, state, localSum) =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                            localSum += minDistSq[i];
                        return localSum;
                    },
                    localSum =>
                    {
                        lock (totalLock) { total += localSum; }
                    });

                double pick = rand.NextDouble() * total;
                double acc = 0;
                int chosen = 0;
                for (int i = 0; i < minDistSq.Length; i++)
                {
                    acc += minDistSq[i];
                    if (acc >= pick)
                    {
                        chosen = i;
                        break;
                    }
                }
                int idx2 = sampleIndices[chosen];
                var newCenter = new double[] { image[idx2 + 2], image[idx2 + 1], image[idx2 + 0] };
                centers.Add(newCenter);

                Parallel.For(0, sampleIndices.Count, i =>
                {
                    int idx = sampleIndices[i];
                    double r = image[idx + 2];
                    double g = image[idx + 1];
                    double b = image[idx + 0];
                    double dr = r - newCenter[0];
                    double dg = g - newCenter[1];
                    double db = b - newCenter[2];
                    double dist = dr * dr + dg * dg + db * db;
                    if (dist < minDistSq[i]) minDistSq[i] = dist;
                });

                progress?.Invoke((double)(k + 1) / clusterCount);
            }

            var colorList = new List<SKColor>(clusterCount);
            foreach (var c in centers)
                colorList.Add(new SKColor((byte)c[0], (byte)c[1], (byte)c[2]));
            return new BitmapPalette(colorList);
        }
    }
}
