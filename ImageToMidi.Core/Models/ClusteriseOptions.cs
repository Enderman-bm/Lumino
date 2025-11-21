using SkiaSharp;
using ImageToMidi.Logic.Clusterisation;

namespace ImageToMidi.Models
{
    public class ClusteriseOptions
    {
        public int ColorCount { get; set; }
        public PaletteClusterMethod Method { get; set; }
        // public BitmapSource Src { get; set; } // Removed WPF dependency, use byte[] or SKBitmap if needed
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        // KMeans
        public double KMeansThreshold { get; set; } = 1.0;
        public int KMeansMaxIterations { get; set; } = 100;

        // KMeans++
        public int KMeansPlusPlusMaxSamples { get; set; } = 20000;
        public int KMeansPlusPlusSeed { get; set; } = 0;

        // Octree
        public int OctreeMaxLevel { get; set; } = 8;
        public int OctreeMaxSamples { get; set; } = 20000;

        // VarianceSplit
        public int VarianceSplitMaxSamples { get; set; } = 20000;

        // PCA
        public int PcaPowerIterations { get; set; } = 20;
        public int PcaMaxSamples { get; set; } = 20000;

        // MaxMin
        public int WeightedMaxMinIters { get; set; } = 3;
        public int WeightedMaxMinMaxSamples { get; set; } = 20000;

        // NativeKMeans
        public int NativeKMeansIterations { get; set; } = 10;
        public double NativeKMeansRate { get; set; } = 0.3;

        // MeanShift
        public double MeanShiftBandwidth { get; set; } = 32;
        public int MeanShiftMaxIter { get; set; } = 7;
        public int MeanShiftMaxSamples { get; set; } = 10000;

        // DBSCAN
        public double? DbscanEpsilon { get; set; } = null;
        public int DbscanMinPts { get; set; } = 4;
        public int DbscanMaxSamples { get; set; } = 2000;

        // GMM
        public int GmmMaxIter { get; set; } = 30;
        public double GmmTol { get; set; } = 1.0;
        public int GmmMaxSamples { get; set; } = 2000;

        // Hierarchical
        public int HierarchicalMaxSamples { get; set; } = 2000;
        public HierarchicalLinkage HierarchicalLinkage { get; set; } = HierarchicalLinkage.Single;
        public HierarchicalDistanceType HierarchicalDistanceType { get; set; } = HierarchicalDistanceType.Euclidean;

        // Spectral
        public int SpectralMaxSamples { get; set; } = 2000;
        public double SpectralSigma { get; set; } = 32.0;
        public int SpectralKMeansIters { get; set; } = 10;

        // LabKMeans
        public double LabKMeansThreshold { get; set; } = 1.0;
        public int LabKMeansMaxIterations { get; set; } = 100;

        // Floyd–Steinberg
        public PaletteClusterMethod FloydBaseMethod { get; set; } = PaletteClusterMethod.OnlyWpf; // Rename to something generic?
        public double FloydDitherStrength { get; set; } = 1.0;
        public bool FloydSerpentine { get; set; } = true;

        // OrderedDither
        public BayerMatrixSize OrderedDitherMatrixSize { get; set; } = BayerMatrixSize.Size4x4;
        public double OrderedDitherStrength { get; set; } = 1.0;

        // OPTICS
        public double? OpticsEpsilon { get; set; } = null;
        public int OpticsMinPts { get; set; } = 4;
        public int OpticsMaxSamples { get; set; } = 2000;

        // FixedBitPalette
        public int BitDepth { get; set; }
        public bool UseGrayFixedPalette { get; set; } = false;
    }

    public enum HierarchicalLinkage
    {
        Single,
        Complete,
        Average
    }

    public enum HierarchicalDistanceType
    {
        Euclidean,
        Manhattan
    }
}
