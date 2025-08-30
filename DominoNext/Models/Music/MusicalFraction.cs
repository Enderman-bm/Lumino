using System;
using System.Collections.Concurrent;
using System.Numerics;

namespace DominoNext.Models.Music
{
    /// <summary>
    /// 音乐分数，用于表示音符时值和位置
    /// 使用传统音乐记谱法：四分音符 = 1/4，全音符 = 1/1
    /// 优化版本：分子为1时使用紧凑存储格式
    /// </summary>
    public readonly struct MusicalFraction : IEquatable<MusicalFraction>, IComparable<MusicalFraction>
    {
        // 使用紧凑存储：当分子为1时，使用负数存储分母
        private readonly int _compactValue;

        // 定义四分音符的标准tick值
        public const int QUARTER_NOTE_TICKS = 96;

        // 优化：仅缓存频繁计算的结果，使用更小的缓存
        private static readonly ConcurrentDictionary<long, double> _ticksCache = new();
        private const int MAX_CACHE_SIZE = 1000; // 限制缓存大小

        public int Numerator 
        { 
            get 
            {
                if (_compactValue == 0) return 0;
                if (_compactValue < 0) return 1; // 紧凑格式：分子为1
                return _compactValue >> 16; // 普通格式：高16位为分子
            } 
        }

        public int Denominator 
        { 
            get 
            {
                if (_compactValue == 0) return 1;
                if (_compactValue < 0) return -_compactValue; // 紧凑格式：负数的绝对值为分母
                return _compactValue & 0xFFFF; // 普通格式：低16位为分母
            } 
        }

        public MusicalFraction(int numerator, int denominator)
        {
            if (denominator <= 0)
                throw new ArgumentException("分母必须大于0", nameof(denominator));

            // 特殊处理：如果分子为0，表示位置0
            if (numerator == 0)
            {
                _compactValue = 0;
                return;
            }

            // 修复 OverflowException: 安全处理 int.MinValue
            if (numerator == int.MinValue)
            {
                // 对于极端情况，使用普通格式
                _compactValue = (numerator >> 1 << 16) | (denominator & 0xFFFF);
                return;
            }

            // 简化分数
            var gcd = GreatestCommonDivisor(Math.Abs(numerator), denominator);
            var simplifiedNumerator = numerator / gcd;
            var simplifiedDenominator = denominator / gcd;

            // 优化：分子为1时使用紧凑存储
            if (simplifiedNumerator == 1)
            {
                _compactValue = -simplifiedDenominator; // 负数表示紧凑格式
            }
            else
            {
                // 普通格式：高16位存分子，低16位存分母
                if (simplifiedNumerator > 0x7FFF || simplifiedDenominator > 0xFFFF)
                {
                    // 超出范围，使用哈希存储（这里简化为直接存储）
                    _compactValue = (simplifiedNumerator << 16) | (simplifiedDenominator & 0xFFFF);
                }
                else
                {
                    _compactValue = (simplifiedNumerator << 16) | simplifiedDenominator;
                }
            }
        }

        /// <summary>
        /// 转换为tick值 - 优化版本，针对分子为1的情况特别优化
        /// </summary>
        public double ToTicks(int quarterNoteTicks = QUARTER_NOTE_TICKS)
        {
            // 快速路径：分子为1的情况（最常见）
            if (_compactValue < 0)
            {
                var denom = -_compactValue;
                return 4.0 * quarterNoteTicks / denom;
            }

            // 特殊情况：0
            if (_compactValue == 0) return 0;

            // 使用位运算组合键以提高性能
            var cacheKey = ((long)_compactValue << 32) | (uint)quarterNoteTicks;
            
            if (_ticksCache.TryGetValue(cacheKey, out var cachedValue))
            {
                return cachedValue;
            }

            // 计算结果
            var numerator = Numerator;
            var denominator = Denominator;
            var result = (double)numerator * 4 / denominator * quarterNoteTicks;
            
            // 限制缓存大小，防止内存泄漏
            if (_ticksCache.Count < MAX_CACHE_SIZE)
            {
                _ticksCache.TryAdd(cacheKey, result);
            }

            return result;
        }

        /// <summary>
        /// 从tick值创建音乐分数 - 优先创建分子为1的分数
        /// </summary>
        public static MusicalFraction FromTicks(double ticks, int quarterNoteTicks = QUARTER_NOTE_TICKS)
        {
            // 快速路径处理
            if (double.IsNaN(ticks) || double.IsInfinity(ticks) || ticks < 0)
                return new MusicalFraction(1, 16);

            if (Math.Abs(ticks) < 1e-10)
                return new MusicalFraction(0, 1);

            var quarterNoteMultiple = ticks / quarterNoteTicks;

            // 使用栈分配的Span提高性能，优先尝试分子为1的分数
            Span<int> commonDenominators = stackalloc int[] { 1, 2, 4, 8, 16, 32, 64, 128, 192, 256, 384 };

            foreach (var denominator in commonDenominators)
            {
                // 优先尝试分子为1的情况
                var testTicks = 4.0 * quarterNoteTicks / denominator;
                if (Math.Abs(testTicks - ticks) < 0.001)
                {
                    return new MusicalFraction(1, denominator); // 这会使用紧凑存储
                }

                // 然后尝试其他分子
                var numerator = Math.Round(quarterNoteMultiple * denominator / 4.0);
                if (numerator >= 2 && numerator <= int.MaxValue)
                {
                    var intNumerator = (int)numerator;
                    var calculatedTicks = (double)intNumerator * 4 / denominator * quarterNoteTicks;
                    
                    if (Math.Abs(calculatedTicks - ticks) < 0.001)
                    {
                        return new MusicalFraction(intNumerator, denominator);
                    }
                }
            }

            // 默认使用64分音符精度
            var bestNumerator = Math.Max(1, Math.Round(quarterNoteMultiple * 64 / 4.0));
            if (bestNumerator <= int.MaxValue)
            {
                return new MusicalFraction((int)bestNumerator, 64);
            }

            return new MusicalFraction(1, 16);
        }

        /// <summary>
        /// 高性能批量量化 - 针对紧凑存储优化
        /// </summary>
        public static void QuantizeToGridBatch(Span<double> positions, MusicalFraction gridUnit, int quarterNoteTicks = QUARTER_NOTE_TICKS)
        {
            var gridSizeInTicks = gridUnit.ToTicks(quarterNoteTicks);
            if (gridSizeInTicks <= 0) return;

            // 预计算倒数以避免除法
            var invGridSize = 1.0 / gridSizeInTicks;
            
            for (int i = 0; i < positions.Length; i++)
            {
                var pos = positions[i];
                if (double.IsNaN(pos) || double.IsInfinity(pos))
                {
                    positions[i] = 0;
                    continue;
                }

                if (Math.Abs(pos) < 1e-10)
                {
                    positions[i] = 0;
                    continue;
                }

                // 优化的量化计算
                var quantized = Math.Round(pos * invGridSize) * gridSizeInTicks;
                if (pos >= 0 && quantized < 0)
                {
                    quantized = 0;
                }
                positions[i] = quantized;
            }
        }

        public double GetGridUnit(int quarterNoteTicks = QUARTER_NOTE_TICKS) => ToTicks(quarterNoteTicks);

        public static double QuantizeToGrid(double positionInTicks, MusicalFraction gridUnit, int quarterNoteTicks = QUARTER_NOTE_TICKS)
        {
            if (double.IsNaN(positionInTicks) || double.IsInfinity(positionInTicks))
                return 0;

            if (Math.Abs(positionInTicks) < 1e-10)
                return 0;

            var gridSizeInTicks = gridUnit.GetGridUnit(quarterNoteTicks);
            if (gridSizeInTicks <= 0)
                return positionInTicks;

            var quantizedPosition = Math.Round(positionInTicks / gridSizeInTicks) * gridSizeInTicks;
            
            if (positionInTicks >= 0 && quantizedPosition < 0)
                quantizedPosition = 0;

            return quantizedPosition;
        }

        public static MusicalFraction CalculateQuantizedDuration(double startTicks, double endTicks, MusicalFraction gridUnit, int quarterNoteTicks = QUARTER_NOTE_TICKS)
        {
            var durationTicks = Math.Max(gridUnit.GetGridUnit(quarterNoteTicks), endTicks - startTicks);
            var gridSizeInTicks = gridUnit.GetGridUnit(quarterNoteTicks);

            if (gridSizeInTicks <= 0)
                return gridUnit;

            var gridUnits = Math.Max(1, Math.Round(durationTicks / gridSizeInTicks));
            var resultNumerator = gridUnits * gridUnit.Numerator;
            
            if (resultNumerator >= int.MinValue && resultNumerator <= int.MaxValue)
                return new MusicalFraction((int)resultNumerator, gridUnit.Denominator);

            return gridUnit;
        }

        // 常用音符时值 - 这些都会使用紧凑存储
        public static MusicalFraction WholeNote => new(1, 1);
        public static MusicalFraction HalfNote => new(1, 2);
        public static MusicalFraction QuarterNote => new(1, 4);
        public static MusicalFraction EighthNote => new(1, 8);
        public static MusicalFraction SixteenthNote => new(1, 16);
        public static MusicalFraction ThirtySecondNote => new(1, 32);
        public static MusicalFraction TripletHalf => new(1, 3);
        public static MusicalFraction TripletQuarter => new(1, 6);
        public static MusicalFraction TripletEighth => new(1, 12);
        public static MusicalFraction TripletSixteenth => new(1, 24);
        public static MusicalFraction DottedHalf => new(3, 4);
        public static MusicalFraction DottedQuarter => new(3, 8);
        public static MusicalFraction DottedEighth => new(3, 16);

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
                (a, b) = (b, a % b);
            return a;
        }

        public bool Equals(MusicalFraction other) => _compactValue == other._compactValue;

        public int CompareTo(MusicalFraction other) => ToTicks().CompareTo(other.ToTicks());

        public override bool Equals(object? obj) => obj is MusicalFraction other && Equals(other);

        public override int GetHashCode() => _compactValue.GetHashCode();

        public override string ToString() => $"{Numerator}/{Denominator}";

        public static bool operator ==(MusicalFraction left, MusicalFraction right) => left.Equals(right);
        public static bool operator !=(MusicalFraction left, MusicalFraction right) => !left.Equals(right);
        public static bool operator <(MusicalFraction left, MusicalFraction right) => left.CompareTo(right) < 0;
        public static bool operator >(MusicalFraction left, MusicalFraction right) => left.CompareTo(right) > 0;

        public static MusicalFraction operator +(MusicalFraction left, MusicalFraction right)
        {
            var numerator = left.Numerator * right.Denominator + right.Numerator * left.Denominator;
            var denominator = left.Denominator * right.Denominator;
            return new MusicalFraction(numerator, denominator);
        }

        public static MusicalFraction operator -(MusicalFraction left, MusicalFraction right)
        {
            var numerator = left.Numerator * right.Denominator - right.Numerator * left.Denominator;
            var denominator = left.Denominator * right.Denominator;
            return new MusicalFraction(numerator, denominator);
        }

        public static MusicalFraction operator *(MusicalFraction left, int multiplier)
        {
            return new MusicalFraction(left.Numerator * multiplier, left.Denominator);
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public static void ClearCache()
        {
            _ticksCache.Clear();
        }

        /// <summary>
        /// 获取存储效率信息（调试用）
        /// </summary>
        public bool IsCompactStorage => _compactValue < 0;
    }
}