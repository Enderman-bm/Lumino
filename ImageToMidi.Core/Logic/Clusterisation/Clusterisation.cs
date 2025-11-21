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
        private static List<SKColor> SortPaletteByHsl(IEnumerable<SKColor> colors)
        {
            return colors.OrderBy(c => RgbToHslKey(c.Red, c.Green, c.Blue)).ToList();
        }

        private static double RgbToHslKey(double r, double g, double b)
        {
            // Normalize
            r /= 255.0;
            g /= 255.0;
            b /= 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double h = 0, s, l = (max + min) / 2.0;

            if (max == min)
            {
                h = s = 0; // Gray
            }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

                if (max == r)
                    h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g)
                    h = (b - r) / d + 2;
                else
                    h = (r - g) / d + 4;

                h /= 6.0;
            }
            // Sort by H primarily, then S and L
            return h * 10000 + s * 100 + l;
        }

        public static BitmapPalette ClusteriseByMethod(
            byte[] image,
            ClusteriseOptions options,
            out double lastChange,
            out byte[] ditheredPixels,
            Action<double> progress = null)
        {
            ditheredPixels = null;
            BitmapPalette palette;
            switch (options.Method)
            {
                case PaletteClusterMethod.OnlyWpf:
                    // WPF specific, fallback or throw
                    lastChange = 0;
                    // Fallback to Popularity for now as we don't have WPF
                    palette = PopularityPalette(image, options.ColorCount); 
                    break;

                case PaletteClusterMethod.OnlyKMeansPlusPlus:
                    lastChange = 0;
                    palette = KMeansPlusPlusInit(
                        image,
                        options.ColorCount,
                        options.KMeansPlusPlusMaxSamples,
                        options.KMeansPlusPlusSeed,
                        progress
                    );
                    break;

                case PaletteClusterMethod.KMeans:
                    {
                        // Use KMeansPlusPlusInit as initial palette instead of WPF one
                        var initPalette = KMeansPlusPlusInit(
                            image,
                            options.ColorCount,
                            options.KMeansPlusPlusMaxSamples,
                            options.KMeansPlusPlusSeed
                        );
                        palette = KMeans(
                            initPalette,
                            image,
                            options.KMeansThreshold,
                            out lastChange,
                            options.KMeansMaxIterations,
                            20000,
                            0,
                            progress
                        );
                    }
                    break;

                case PaletteClusterMethod.KMeansPlusPlus:
                    {
                        var kppPalette = KMeansPlusPlusInit(
                            image,
                            options.ColorCount,
                            options.KMeansPlusPlusMaxSamples,
                            options.KMeansPlusPlusSeed
                        );
                        palette = KMeans(
                            kppPalette,
                            image,
                            options.KMeansThreshold,
                            out lastChange,
                            options.KMeansMaxIterations,
                            20000,
                            0,
                            progress
                        );
                    }
                    break;

                case PaletteClusterMethod.Popularity:
                    lastChange = 0;
                    palette = PopularityPalette(image, options.ColorCount);
                    break;

                case PaletteClusterMethod.Octree:
                    palette = OctreePalette.CreatePalette(
                        image,
                        options.ColorCount,
                        options.OctreeMaxLevel,
                        options.OctreeMaxSamples
                    );
                    lastChange = 0;
                    break;

                case PaletteClusterMethod.VarianceSplit:
                    lastChange = 0;
                    palette = VarianceSplitPalette(
                        image,
                        options.ColorCount,
                        options.VarianceSplitMaxSamples
                    );
                    break;

                case PaletteClusterMethod.Pca:
                    lastChange = 0;
                    palette = PcaPalette(
                        image,
                        options.ColorCount,
                        options.PcaPowerIterations,
                        options.PcaMaxSamples
                    );
                    break;

                case PaletteClusterMethod.MaxMin:
                    lastChange = 0;
                    palette = WeightedMaxMinKMeansPalette(
                        image,
                        options.ColorCount,
                        options.WeightedMaxMinIters,
                        options.WeightedMaxMinMaxSamples
                    );
                    break;

                case PaletteClusterMethod.NativeKMeans:
                    lastChange = 0;
                    // Use Popularity as initial palette
                    var nativeInitPalette = PopularityPalette(image, options.ColorCount);
                    palette = NativeKMeansPalette(
                        nativeInitPalette,
                        image,
                        options.NativeKMeansIterations,
                        options.NativeKMeansRate
                    );
                    break;

                case PaletteClusterMethod.MeanShift:
                    lastChange = 0;
                    palette = MeanShiftPalette(
                        image,
                        options.ColorCount,
                        options.MeanShiftBandwidth,
                        options.MeanShiftMaxIter,
                        options.MeanShiftMaxSamples
                    );
                    break;

                case PaletteClusterMethod.DBSCAN:
                    lastChange = 0;
                    palette = DBSCANPalette(
                        image,
                        options.ColorCount,
                        options.DbscanEpsilon,
                        options.DbscanMinPts,
                        options.DbscanMaxSamples
                    );
                    break;

                case PaletteClusterMethod.GMM:
                    lastChange = 0;
                    palette = GMMPalette(
                        image,
                        options.ColorCount,
                        options.GmmMaxIter,
                        options.GmmTol,
                        options.GmmMaxSamples
                    );
                    break;

                case PaletteClusterMethod.Hierarchical:
                    lastChange = 0;
                    palette = HierarchicalPalette(
                        image,
                        options.ColorCount,
                        options.HierarchicalMaxSamples,
                        options.HierarchicalLinkage,
                        options.HierarchicalDistanceType
                    );
                    break;

                case PaletteClusterMethod.Spectral:
                    lastChange = 0;
                    palette = SpectralPalette(
                        image,
                        options.ColorCount,
                        options.SpectralMaxSamples,
                        options.SpectralSigma,
                        options.SpectralKMeansIters
                    );
                    break;

                case PaletteClusterMethod.LabKMeans:
                    lastChange = 0;
                    // Use Popularity as initial palette
                    var labInitPalette = PopularityPalette(image, options.ColorCount);
                    palette = LabKMeans(
                        labInitPalette,
                        image,
                        options.LabKMeansThreshold,
                        out lastChange,
                        options.LabKMeansMaxIterations
                    );
                    break;

                case PaletteClusterMethod.FloydSteinbergDither:
                    lastChange = 0;
                    // Recursive call to get base palette
                    var basePalette = ClusteriseByMethod(
                        image,
                        new ClusteriseOptions
                        {
                            ColorCount = options.ColorCount,
                            Method = options.FloydBaseMethod,
                            ImageWidth = options.ImageWidth,
                            ImageHeight = options.ImageHeight,
                            // Src = options.Src, // Removed
                        }, out _, out _);
                    
                    if (options.ImageWidth == 0 || options.ImageHeight == 0)
                        throw new ArgumentException("FloydSteinbergDither requires ImageWidth and ImageHeight to be set in options.");

                    var dithered = FloydSteinbergDither.Dither(
                        image,
                        options.ImageWidth,
                        options.ImageHeight,
                        basePalette.Colors,
                        options.FloydDitherStrength,
                        options.FloydSerpentine
                    );
                    ditheredPixels = dithered;
                    palette = basePalette;
                    break;

                case PaletteClusterMethod.OrderedDither:
                    lastChange = 0;
                    // Recursive call
                    var basePaletteOrdered = ClusteriseByMethod(
                        image,
                        new ClusteriseOptions
                        {
                            ColorCount = options.ColorCount,
                            Method = options.FloydBaseMethod,
                            ImageWidth = options.ImageWidth,
                            ImageHeight = options.ImageHeight,
                            // Src = options.Src,
                        }, out _, out _);
                    
                    if (options.ImageWidth == 0 || options.ImageHeight == 0)
                        throw new ArgumentException("OrderedDither requires ImageWidth and ImageHeight to be set in options.");

                    var ditheredOrdered = OrderedDither.Dither(
                        image,
                        options.ImageWidth,
                        options.ImageHeight,
                        basePaletteOrdered.Colors,
                        options.OrderedDitherStrength,
                        options.OrderedDitherMatrixSize
                    );
                    ditheredPixels = ditheredOrdered;
                    palette = basePaletteOrdered;
                    break;

                case PaletteClusterMethod.OPTICS:
                    lastChange = 0;
                    palette = OpticsPalette.Cluster(
                        image,
                        options.ColorCount,
                        options.OpticsEpsilon,
                        options.OpticsMinPts,
                        options.OpticsMaxSamples
                    );
                    break;

                case PaletteClusterMethod.FixedBitPalette:
                    palette = FixedBitPalette(
                        image,
                        options,
                        out lastChange,
                        out ditheredPixels,
                        progress
                    );
                    break;

                default:
                    lastChange = 0;
                    throw new ArgumentException("Unknown cluster method");
            }

            // Sort by HSL
            var sortedColors = SortPaletteByHsl(palette.Colors).ToList();
            // Pad
            while (sortedColors.Count < options.ColorCount)
                sortedColors.Add(new SKColor(0, 0, 0));
            if (sortedColors.Count > options.ColorCount)
                sortedColors = sortedColors.Take(options.ColorCount).ToList();
            return new BitmapPalette(sortedColors);
        }

        public static List<int> SamplePixelIndices(byte[] image, int maxSamples, int seed = 0)
        {
            int pixelCount = image.Length / 4;
            Random rand = new Random(seed);
            HashSet<int> selected = new HashSet<int>(maxSamples);
            List<int> result = new List<int>(maxSamples);

            while (result.Count < maxSamples && result.Count < pixelCount)
            {
                int idx = rand.Next(pixelCount);
                if (selected.Add(idx))
                {
                    int offset = idx * 4;
                    if (image[offset + 3] > 128) // Alpha > 128
                        result.Add(offset);
                }
            }

            if (result.Count == 0)
            {
                for (int i = 0; i < pixelCount; i++)
                {
                    int offset = i * 4;
                    if (image[offset + 3] > 128)
                        result.Add(offset);
                    if (result.Count >= maxSamples) break;
                }
            }

            return result;
        }

        // Implementations of algorithms (Popularity, VarianceSplit, etc.)
        // I will add them in subsequent edits or files.
        // For now, I'll add PopularityPalette as it is used in fallback.

        public static BitmapPalette PopularityPalette(byte[] image, int colorCount)
        {
            int length = image.Length;
            int processorCount = Environment.ProcessorCount;
            int blockSize = length / processorCount / 4 * 4;
            if (blockSize == 0) blockSize = length;

            var dicts = new Dictionary<int, int>[processorCount];
            Parallel.For(0, processorCount, t =>
            {
                var dict = new Dictionary<int, int>(4096);
                int start = t * blockSize;
                int end = (t == processorCount - 1) ? length : start + blockSize;
                for (int i = start; i < end; i += 4)
                {
                    if (image[i + 3] < 128) continue;
                    // BGRA
                    int rgb = (image[i + 2] << 16) | (image[i + 1] << 8) | image[i + 0];
                    if (dict.ContainsKey(rgb)) dict[rgb]++;
                    else dict[rgb] = 1;
                }
                dicts[t] = dict;
            });

            var totalDict = new Dictionary<int, int>(4096);
            foreach (var dict in dicts)
            {
                foreach (var kv in dict)
                {
                    if (totalDict.ContainsKey(kv.Key)) totalDict[kv.Key] += kv.Value;
                    else totalDict[kv.Key] = kv.Value;
                }
            }

            var topColors = totalDict.OrderByDescending(p => p.Value).Take(colorCount)
                .Select(p => new SKColor((byte)(p.Key >> 16), (byte)(p.Key >> 8), (byte)p.Key)).ToList();

            while (topColors.Count < colorCount)
                topColors.Add(new SKColor(0, 0, 0));

            return new BitmapPalette(topColors);
        }
    }
}
