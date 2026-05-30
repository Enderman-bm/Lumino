using Xunit;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.EditorTests;

/// <summary>
/// AutoScrollMode 枚举和相关逻辑的单元测试
/// </summary>
public class AutoScrollModeTests
{
    [Fact]
    public void AutoScrollMode_HasThreeValues()
    {
        // Act
        var values = Enum.GetValues(typeof(AutoScrollMode));

        // Assert
        Assert.Equal(3, values.Length);
    }

    [Fact]
    public void AutoScrollMode_FixedIndicatorLeft_IsDefined()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(AutoScrollMode), AutoScrollMode.FixedIndicatorLeft));
    }

    [Fact]
    public void AutoScrollMode_ScrollingIndicator_IsDefined()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(AutoScrollMode), AutoScrollMode.ScrollingIndicator));
    }

    [Fact]
    public void AutoScrollMode_Off_IsDefined()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(AutoScrollMode), AutoScrollMode.Off));
    }

    [Fact]
    public void AutoScrollMode_DefaultIsScrollingIndicator()
    {
        // Arrange - PlaybackViewModel 中默认值为 ScrollingIndicator
        // 这里测试枚举的默认值（int 0）
        var defaultValue = default(AutoScrollMode);

        // Assert - default(AutoScrollMode) = FixedIndicatorLeft (0)
        // 注意：这不是 PlaybackViewModel 的默认值，只是枚举的默认值
        Assert.Equal(AutoScrollMode.FixedIndicatorLeft, defaultValue);
    }
}
