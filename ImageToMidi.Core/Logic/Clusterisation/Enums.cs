namespace ImageToMidi.Logic.Clusterisation
{
    public enum PaletteClusterMethod
    {
        OnlyWpf,
        OnlyKMeansPlusPlus,
        KMeans,
        KMeansPlusPlus,
        Popularity,
        Octree,
        VarianceSplit,
        Pca,
        MaxMin,
        NativeKMeans,
        MeanShift,
        DBSCAN,
        GMM,
        Hierarchical,
        Spectral,
        LabKMeans,
        FloydSteinbergDither,
        OrderedDither,
        OPTICS,
        FixedBitPalette,
    }
}
