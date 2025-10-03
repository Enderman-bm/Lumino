using System;

namespace Lumino.Models.Music
{
    /// <summary>
    /// 音乐分数，用于表示音符时值和位置
    /// 使用传统音乐记谱法：四分音符 = 1/4，全音符 = 1/1
    /// 重要：为了匹配新的坐标系统，ToDouble()方法返回以四分音符为单位的值
    /// </summary>
    public readonly struct MusicalFraction : IEquatable<MusicalFraction>, IComparable<MusicalFraction>
    {
        public int Numerator { get; }
        public int Denominator { get; }

        public MusicalFraction(int numerator, int denominator)
        {
            if (denominator <= 0)
                throw new ArgumentException("分母必须大于0", nameof(denominator));

            // 特殊处理：如果分子为0，表示位置0，直接设置为0/1
            if (numerator == 0)
            {
                Numerator = 0;
                Denominator = 1;
                return;
            }

            // 修复 OverflowException: 安全处理 int.MinValue
            if (numerator == int.MinValue)
            {
                // 对于极端情况，不进行简化
                Numerator = numerator;
                Denominator = denominator;
                return;
            }

            // 简化分数
            var gcd = GreatestCommonDivisor(Math.Abs(numerator), denominator);
            Numerator = numerator / gcd;
            Denominator = denominator / gcd;
        }

        /// <summary>
        /// 转换为以四分音符为单位的浮点数值
        /// 例如：全音符(1/1) = 4.0，四分音符(1/4) = 1.0，八分音符(1/8) = 0.5
        /// </summary>
        /// <returns>以四分音符为单位的时间值</returns>
        public double ToDouble()
        {
            // 传统音乐记谱法转换到以四分音符为单位：
            // 分数值 × 4 = 以四分音符为单位的值
            // 1/1 (全音符) × 4 = 4.0 (4个四分音符)
            // 1/2 (二分音符) × 4 = 2.0 (2个四分音符)  
            // 1/4 (四分音符) × 4 = 1.0 (1个四分音符)
            // 1/8 (八分音符) × 4 = 0.5 (0.5个四分音符)
            // 1/16 (十六分音符) × 4 = 0.25 (0.25个四分音符)
            return ((double)Numerator / Denominator) * 4.0;
        }

        /// <summary>
        /// 从以四分音符为单位的浮点数创建音乐分数
        /// </summary>
        /// <param name="quarterNoteUnits">以四分音符为单位的时间值</param>
        /// <returns>最接近的音乐分数</returns>
        public static MusicalFraction FromDouble(double quarterNoteUnits)
        {
            // 添加安全检查
            if (double.IsNaN(quarterNoteUnits) || double.IsInfinity(quarterNoteUnits) || quarterNoteUnits < 0)
            {
                return new MusicalFraction(1, 16); // 默认十六分音符
            }

            // 修复：如果值为0或接近0，直接返回0
            if (Math.Abs(quarterNoteUnits) < 1e-10)
            {
                return new MusicalFraction(0, 1); // 0时间位置
            }

            // 将四分音符单位转换回传统分数表示
            // 四分音符单位 ÷ 4 = 传统分数值
            var traditionalValue = quarterNoteUnits / 4.0;

            // 不再限制常见分母，使用连分数算法来找到最佳的分数表示
            // 这样可以支持任意精度和任意分子分母的音符时值
            return DecimalToFraction(traditionalValue);
        }

        /// <summary>
        /// 使用连分数算法将小数转换为最佳分数表示
        /// 支持任意精度的音符时值，不限制分子分母的值
        /// </summary>
        /// <param name="value">要转换的小数值</param>
        /// <param name="maxDenominator">允许的最大分母（默认为100万，确保高精度）</param>
        /// <returns>最接近的分数表示</returns>
        private static MusicalFraction DecimalToFraction(double value, int maxDenominator = 1000000)
        {
            if (Math.Abs(value) < 1e-15)
                return new MusicalFraction(0, 1);

            bool isNegative = value < 0;
            value = Math.Abs(value);

            // 使用连分数算法
            var h = new int[3] { 0, 1, 0 };
            var k = new int[3] { 1, 0, 0 };

            double x = value;
            int maxIterations = 64; // 防止无限循环

            for (int i = 0; i < maxIterations; i++)
            {
                int a = (int)Math.Floor(x);
                
                h[2] = a * h[1] + h[0];
                k[2] = a * k[1] + k[0];

                // 检查分母是否超过限制
                if (k[2] > maxDenominator)
                {
                    // 使用前一个更好的近似
                    break;
                }

                // 检查精度是否足够
                if (k[1] != 0)
                {
                    double fraction = (double)h[1] / k[1];
                    if (Math.Abs(fraction - value) < 1e-15)
                    {
                        return new MusicalFraction(isNegative ? -h[1] : h[1], k[1]);
                    }
                }

                // 如果x是整数，我们完成了
                if (Math.Abs(x - a) < 1e-15)
                {
                    return new MusicalFraction(isNegative ? -h[2] : h[2], k[2]);
                }

                // 继续连分数
                x = 1.0 / (x - a);

                // 为下次迭代准备
                h[0] = h[1]; h[1] = h[2];
                k[0] = k[1]; k[1] = k[2];
            }

            // 返回最后的有效近似
            if (k[1] > 0 && k[1] <= maxDenominator)
            {
                return new MusicalFraction(isNegative ? -h[1] : h[1], k[1]);
            }
            else if (k[0] > 0)
            {
                return new MusicalFraction(isNegative ? -h[0] : h[0], k[0]);
            }

            // 如果连分数算法失败，使用简单的十进制近似
            return SimpleDecimalToFraction(isNegative ? -value : value, maxDenominator);
        }

        /// <summary>
        /// 简单的小数到分数转换算法（备用方法）
        /// </summary>
        private static MusicalFraction SimpleDecimalToFraction(double value, int maxDenominator = 1000000)
        {
            bool isNegative = value < 0;
            value = Math.Abs(value);

            int bestNumerator = 1;
            int bestDenominator = 1;
            double bestError = Math.Abs(value - 1.0);

            // 搜索最佳分数近似
            for (int denominator = 1; denominator <= maxDenominator; denominator++)
            {
                int numerator = (int)Math.Round(value * denominator);
                if (numerator == 0) continue;

                double testValue = (double)numerator / denominator;
                double error = Math.Abs(value - testValue);

                if (error < bestError)
                {
                    bestNumerator = numerator;
                    bestDenominator = denominator;
                    bestError = error;

                    // 如果误差足够小，就停止搜索
                    if (error < 1e-15)
                        break;
                }
            }

            return new MusicalFraction(isNegative ? -bestNumerator : bestNumerator, bestDenominator);
        }

        /// <summary>
        /// 对位置进行网格量化
        /// </summary>
        /// <param name="position">要量化的位置（分数）</param>
        /// <param name="gridUnit">网格单位（此MusicalFraction）</param>
        /// <returns>量化后的位置（分数）</returns>
        public static MusicalFraction QuantizeToGrid(MusicalFraction position, MusicalFraction gridUnit)
        {
            // 添加安全检查
            if (gridUnit.Numerator <= 0 || gridUnit.Denominator <= 0)
            {
                return position; // 如果网格单位无效，返回原值
            }

            // 修复：如果位置已经是0，直接返回0
            if (position.Numerator == 0)
            {
                return new MusicalFraction(0, 1);
            }

            // 计算位置相对于网格单位的倍数（都以四分音符为单位）
            var positionValue = position.ToDouble();
            var gridValue = gridUnit.ToDouble();
            
            if (gridValue <= 0)
            {
                return position; // 如果网格大小无效，返回原值
            }

            // 量化到最近的网格点
            var quantizedMultiple = Math.Round(positionValue / gridValue);
            
            // 确保量化后的位置不为负数（除非原位置就是负数）
            if (positionValue >= 0 && quantizedMultiple < 0)
            {
                quantizedMultiple = 0;
            }

            // 计算量化后的分数
            var resultValue = quantizedMultiple * gridValue;
            return FromDouble(resultValue);
        }

        /// <summary>
        /// 计算从起始位置到结束位置的量化长度
        /// </summary>
        /// <param name="start">起始位置</param>
        /// <param name="end">结束位置</param>
        /// <param name="gridUnit">网格单位</param>
        /// <returns>量化后的长度分数</returns>
        public static MusicalFraction CalculateQuantizedDuration(MusicalFraction start, MusicalFraction end, MusicalFraction gridUnit)
        {
            var duration = end - start;
            var gridValue = gridUnit.ToDouble();

            if (gridValue <= 0)
            {
                return gridUnit; // 返回默认网格单位
            }

            var durationValue = Math.Max(gridValue, duration.ToDouble());
            var gridUnits = Math.Max(1, Math.Round(durationValue / gridValue));

            // 计算结果值（以四分音符为单位）
            var resultValue = gridUnits * gridValue;
            return FromDouble(resultValue);
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }
            return a;
        }

        public bool Equals(MusicalFraction other) =>
            Numerator == other.Numerator && Denominator == other.Denominator;

        public int CompareTo(MusicalFraction other) =>
            ToDouble().CompareTo(other.ToDouble());

        public override bool Equals(object? obj) =>
            obj is MusicalFraction other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Numerator, Denominator);

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

        public static MusicalFraction operator *(MusicalFraction left, double multiplier)
        {
            var result = left.ToDouble() * multiplier;
            return FromDouble(result);
        }

        public static MusicalFraction operator /(MusicalFraction left, int divisor)
        {
            if (divisor == 0)
                throw new DivideByZeroException();
            return new MusicalFraction(left.Numerator, left.Denominator * divisor);
        }
    }
}