using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace ImageToMidi.Models
{
    /// <summary>
    /// 像素颜色数据
    /// </summary>
    public readonly struct PixelColor
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public PixelColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public PixelColor(int r, int g, int b, int a = 255)
            : this((byte)r, (byte)g, (byte)b, (byte)a) { }

        public uint ToArgb() => (uint)((A << 24) | (R << 16) | (G << 8) | B);

        public override bool Equals(object? obj) => obj is PixelColor other && Equals(other);

        public bool Equals(PixelColor other) => R == other.R && G == other.G && B == other.B && A == other.A;

#if NET48
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + R;
                hash = hash * 31 + G;
                hash = hash * 31 + B;
                hash = hash * 31 + A;
                return hash;
            }
        }
#else
        public override int GetHashCode() => HashCode.Combine(R, G, B, A);
#endif

        public override string ToString() => $"RGBA({R}, {G}, {B}, {A})";
    }

    /// <summary>
    /// 调色板颜色
    /// </summary>
    public readonly struct PaletteColor
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public uint OriginalArgb { get; }

        public PaletteColor(byte r, byte g, byte b, uint originalArgb)
        {
            R = r;
            G = g;
            B = b;
            OriginalArgb = originalArgb;
        }

        public PaletteColor(int r, int g, int b, uint originalArgb)
            : this((byte)r, (byte)g, (byte)b, originalArgb) { }

        public override bool Equals(object? obj) => obj is PaletteColor other && Equals(other);

        public bool Equals(PaletteColor other) => R == other.R && G == other.G && B == other.B;

#if NET48
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + R;
                hash = hash * 31 + G;
                hash = hash * 31 + B;
                return hash;
            }
        }
#else
        public override int GetHashCode() => HashCode.Combine(R, G, B);
#endif

        public override string ToString() => $"RGB({R}, {G}, {B})";
    }

    /// <summary>
    /// LAB颜色空间表示
    /// </summary>
    public readonly struct LabColor
    {
        public double L { get; }
        public double A { get; }
        public double B { get; }

        public LabColor(double l, double a, double b)
        {
            L = l;
            A = a;
            B = b;
        }
    }

    /// <summary>
    /// 颜色转换工具
    /// </summary>
    public static class ColorConverter
    {
        private const double Epsilon = 216.0 / 24389.0;
        private const double Kappa = 24389.0 / 27.0;

        /// <summary>
        /// RGB到LAB颜色空间转换
        /// </summary>
        public static LabColor RgbToLab(int r, int g, int b)
        {
            // 步骤1: RGB到XYZ
            double rLinear = r / 255.0;
            double gLinear = g / 255.0;
            double bLinear = b / 255.0;

            rLinear = (rLinear > 0.04045) ? Math.Pow((rLinear + 0.055) / 1.055, 2.4) : rLinear / 12.92;
            gLinear = (gLinear > 0.04045) ? Math.Pow((gLinear + 0.055) / 1.055, 2.4) : gLinear / 12.92;
            bLinear = (bLinear > 0.04045) ? Math.Pow((bLinear + 0.055) / 1.055, 2.4) : bLinear / 12.92;

            rLinear *= 100.0;
            gLinear *= 100.0;
            bLinear *= 100.0;

            double x = rLinear * 0.4124 + gLinear * 0.3576 + bLinear * 0.1805;
            double y = rLinear * 0.2126 + gLinear * 0.7152 + bLinear * 0.0722;
            double z = rLinear * 0.0193 + gLinear * 0.1192 + bLinear * 0.9505;

            // 步骤2: XYZ到LAB
            x = PivotXyz(x / 95.047);
            y = PivotXyz(y / 100.000);
            z = PivotXyz(z / 108.883);

            double l = 116 * y - 16;
            double a = 500 * (x - y);
            double bColor = 200 * (y - z);

            return new LabColor(l, a, bColor);
        }

        private static double PivotXyz(double t)
        {
            return t > Epsilon ? Math.Pow(t, 1.0 / 3.0) : (16.0 * t + Kappa) / Kappa;
        }

        /// <summary>
        /// 计算LAB空间中的颜色距离
        /// </summary>
        public static double DeltaE(LabColor c1, LabColor c2)
        {
            double deltaL = c1.L - c2.L;
            double deltaA = c1.A - c2.A;
            double deltaB = c1.B - c2.B;

            return Math.Sqrt(deltaL * deltaL + deltaA * deltaA + deltaB * deltaB);
        }

        /// <summary>
        /// RGB到HSL颜色空间转换
        /// </summary>
        public static (double H, double S, double L) RgbToHsl(byte r, byte g, byte b)
        {
            double rd = r / 255.0;
            double gd = g / 255.0;
            double bd = b / 255.0;

            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double h = 0.0, s = 0.0, l = (max + min) / 2.0;

            if (max != min)
            {
                double d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

                if (max == rd)
                    h = (gd - bd) / d + (gd < bd ? 6.0 : 0.0);
                else if (max == gd)
                    h = (bd - rd) / d + 2.0;
                else
                    h = (rd - gd) / d + 4.0;

                h /= 6.0;
            }

            return (h, s, l);
        }

        /// <summary>
        /// 计算RGB颜色距离
        /// </summary>
        public static int ColorDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            int dr = r1 - r2;
            int dg = g1 - g2;
            int db = b1 - b2;
            return dr * dr + dg * dg + db * db;
        }
    }

    /// <summary>
    /// 抖动算法
    /// </summary>
    public enum DitherAlgorithm
    {
        None = 0,
        FloydSteinberg = 1,
        Bayer2x2 = 2,
        Bayer4x4 = 3,
        Bayer8x8 = 4,
        SierraLite = 5
    }

    /// <summary>
    /// 贝叶斯矩阵大小
    /// </summary>
    public enum BayerMatrixSize
    {
        Size2x2 = 2,
        Size4x4 = 4,
        Size8x8 = 8
    }
}
