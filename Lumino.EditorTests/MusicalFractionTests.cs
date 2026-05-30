using Xunit;
using Lumino.Models.Music;

namespace Lumino.EditorTests;

/// <summary>
/// MusicalFraction 单元测试 - 覆盖量化、转换和构造逻辑
/// </summary>
public class MusicalFractionTests
{
    #region QuantizeToGrid 测试

    [Fact]
    public void QuantizeToGrid_PositionZero_ReturnsZero()
    {
        // Arrange
        var position = new MusicalFraction(0, 1);
        var gridUnit = new MusicalFraction(1, 4); // 四分音符

        // Act
        var result = MusicalFraction.QuantizeToGrid(position, gridUnit);

        // Assert
        Assert.Equal(0, result.Numerator);
        Assert.Equal(1, result.Denominator);
    }

    [Fact]
    public void QuantizeToGrid_ExactGridPoint_ReturnsSamePosition()
    {
        // Arrange - 位置正好在网格点上（1.0 = 四分音符位置）
        var position = new MusicalFraction(1, 4); // 1/4 = 1.0 四分音符单位
        var gridUnit = new MusicalFraction(1, 4); // 网格单位 = 1.0

        // Act
        var result = MusicalFraction.QuantizeToGrid(position, gridUnit);

        // Assert
        Assert.Equal(1.0, result.ToDouble(), 2);
    }

    [Fact]
    public void QuantizeToGrid_BetweenGridPoints_RoundsToNearest()
    {
        // Arrange - 位置在两个网格点之间（0.75 四分音符单位，网格 = 0.5）
        var position = MusicalFraction.FromDouble(0.75);
        var gridUnit = new MusicalFraction(1, 8); // 八分音符 = 0.5 四分音符单位

        // Act
        var result = MusicalFraction.QuantizeToGrid(position, gridUnit);

        // Assert - 应该四舍五入到 1.0（最近的 0.5 的倍数）
        Assert.Equal(1.0, result.ToDouble(), 2);
    }

    [Fact]
    public void QuantizeToGrid_SixteenthNoteGrid_QuantizesCorrectly()
    {
        // Arrange - 十六分音符网格（0.25 四分音符单位）
        var gridUnit = new MusicalFraction(1, 16); // 0.25 四分音符单位
        var position = MusicalFraction.FromDouble(0.3); // 接近 0.25

        // Act
        var result = MusicalFraction.QuantizeToGrid(position, gridUnit);

        // Assert - 应该量化到 0.25
        Assert.Equal(0.25, result.ToDouble(), 2);
    }

    [Fact]
    public void QuantizeToGrid_EighthNoteGrid_QuantizesCorrectly()
    {
        // Arrange - 八分音符网格（0.5 四分音符单位）
        var gridUnit = new MusicalFraction(1, 8); // 0.5 四分音符单位
        var position = MusicalFraction.FromDouble(0.8); // 接近 1.0

        // Act
        var result = MusicalFraction.QuantizeToGrid(position, gridUnit);

        // Assert - 应该量化到 1.0
        Assert.Equal(1.0, result.ToDouble(), 2);
    }

    [Fact]
    public void QuantizeToGrid_ZeroGridUnit_ReturnsOriginalPosition()
    {
        // Arrange - 使用分子为0的网格单位（无效但不抛异常）
        var position = MusicalFraction.FromDouble(1.5);
        var gridUnit = new MusicalFraction(0, 1); // 分子为0

        // Act
        var result = MusicalFraction.QuantizeToGrid(position, gridUnit);

        // Assert - 应该返回原值
        Assert.Equal(1.5, result.ToDouble(), 2);
    }

    [Fact]
    public void QuantizeToGrid_NegativePosition_HandledCorrectly()
    {
        // Arrange - 负数位置
        var position = MusicalFraction.FromDouble(-0.3);
        var gridUnit = new MusicalFraction(1, 8); // 0.5 四分音符单位

        // Act
        var result = MusicalFraction.QuantizeToGrid(position, gridUnit);

        // Assert - 负数位置应该保持负数（量化到最近的网格点）
        Assert.True(result.ToDouble() <= 0);
    }

    [Fact]
    public void QuantizeToGrid_LargePosition_QuantizesCorrectly()
    {
        // Arrange - 大数值位置（32.75 四分音符单位）
        var position = MusicalFraction.FromDouble(32.75);
        var gridUnit = new MusicalFraction(1, 4); // 四分音符 = 1.0

        // Act
        var result = MusicalFraction.QuantizeToGrid(position, gridUnit);

        // Assert - 应该量化到 33.0
        Assert.Equal(33.0, result.ToDouble(), 2);
    }

    #endregion

    #region ToDouble / FromDouble 测试

    [Fact]
    public void ToDouble_WholeNote_ReturnsFourQuarterNotes()
    {
        // Arrange - 全音符 (1/1)
        var fraction = new MusicalFraction(1, 1);

        // Act
        var result = fraction.ToDouble();

        // Assert - 全音符 = 4 个四分音符
        Assert.Equal(4.0, result, 2);
    }

    [Fact]
    public void ToDouble_HalfNote_ReturnsTwoQuarterNotes()
    {
        // Arrange - 二分音符 (1/2)
        var fraction = new MusicalFraction(1, 2);

        // Act
        var result = fraction.ToDouble();

        // Assert - 二分音符 = 2 个四分音符
        Assert.Equal(2.0, result, 2);
    }

    [Fact]
    public void ToDouble_QuarterNote_ReturnsOne()
    {
        // Arrange - 四分音符 (1/4)
        var fraction = new MusicalFraction(1, 4);

        // Act
        var result = fraction.ToDouble();

        // Assert - 四分音符 = 1.0
        Assert.Equal(1.0, result, 2);
    }

    [Fact]
    public void ToDouble_EighthNote_ReturnsHalfQuarterNote()
    {
        // Arrange - 八分音符 (1/8)
        var fraction = new MusicalFraction(1, 8);

        // Act
        var result = fraction.ToDouble();

        // Assert - 八分音符 = 0.5
        Assert.Equal(0.5, result, 2);
    }

    [Fact]
    public void ToDouble_SixteenthNote_ReturnsQuarterOfQuarterNote()
    {
        // Arrange - 十六分音符 (1/16)
        var fraction = new MusicalFraction(1, 16);

        // Act
        var result = fraction.ToDouble();

        // Assert - 十六分音符 = 0.25
        Assert.Equal(0.25, result, 2);
    }

    [Fact]
    public void FromDouble_Zero_ReturnsZeroFraction()
    {
        // Act
        var result = MusicalFraction.FromDouble(0);

        // Assert
        Assert.Equal(0, result.Numerator);
        Assert.Equal(1, result.Denominator);
    }

    [Fact]
    public void FromDouble_OneQuarterNote_ReturnsQuarterNote()
    {
        // Act
        var result = MusicalFraction.FromDouble(1.0);

        // Assert - 1.0 四分音符单位应该转换回 1/4
        Assert.Equal(1.0, result.ToDouble(), 2);
    }

    [Fact]
    public void FromDouble_TwoQuarterNotes_ReturnsHalfNote()
    {
        // Act
        var result = MusicalFraction.FromDouble(2.0);

        // Assert - 2.0 四分音符单位应该转换回 1/2
        Assert.Equal(2.0, result.ToDouble(), 2);
    }

    [Fact]
    public void FromDouble_NaN_ReturnsDefaultFraction()
    {
        // Act
        var result = MusicalFraction.FromDouble(double.NaN);

        // Assert - NaN 应该返回默认值（十六分音符）
        Assert.Equal(0.25, result.ToDouble(), 2);
    }

    [Fact]
    public void FromDouble_Negative_ReturnsDefaultFraction()
    {
        // Act
        var result = MusicalFraction.FromDouble(-1.0);

        // Assert - 负数应该返回默认值
        Assert.Equal(0.25, result.ToDouble(), 2);
    }

    #endregion

    #region 构造函数测试

    [Fact]
    public void Constructor_ZeroNumerator_SetsToZeroOverOne()
    {
        // Act
        var fraction = new MusicalFraction(0, 4);

        // Assert
        Assert.Equal(0, fraction.Numerator);
        Assert.Equal(1, fraction.Denominator);
    }

    [Fact]
    public void Constructor_SimplifiesFraction()
    {
        // Act
        var fraction = new MusicalFraction(2, 8);

        // Assert - 应该简化为 1/4
        Assert.Equal(1, fraction.Numerator);
        Assert.Equal(4, fraction.Denominator);
    }

    [Fact]
    public void Constructor_InvalidDenominator_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MusicalFraction(1, 0));
    }

    #endregion

    #region 运算符测试

    [Fact]
    public void Equality_SameFractions_AreEqual()
    {
        // Arrange
        var a = new MusicalFraction(1, 4);
        var b = new MusicalFraction(1, 4);

        // Assert
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentFractions_AreNotEqual()
    {
        // Arrange
        var a = new MusicalFraction(1, 4);
        var b = new MusicalFraction(1, 8);

        // Assert
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Addition_TwoFractions_ReturnsCorrectSum()
    {
        // Arrange
        var a = new MusicalFraction(1, 8); // 0.5
        var b = new MusicalFraction(1, 8); // 0.5

        // Act
        var result = a + b;

        // Assert - 0.5 + 0.5 = 1.0
        Assert.Equal(1.0, result.ToDouble(), 2);
    }

    #endregion
}
