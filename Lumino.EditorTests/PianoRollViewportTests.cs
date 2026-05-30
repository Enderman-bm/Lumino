using Xunit;
using Lumino.ViewModels.Editor.Components;
using Lumino.ViewModels.Editor.Enums;

namespace Lumino.EditorTests;

/// <summary>
/// PianoRollViewport 单元测试 - 覆盖视口管理和滚动逻辑
/// </summary>
public class PianoRollViewportTests
{
    #region 默认值测试

    [Fact]
    public void DefaultValues_InitializedCorrectly()
    {
        // Arrange & Act
        var viewport = new PianoRollViewport();

        // Assert
        Assert.Equal(0.0, viewport.CurrentScrollOffset);
        Assert.Equal(0.0, viewport.VerticalScrollOffset);
        Assert.Equal(0.0, viewport.TimelinePosition);
        Assert.Equal(800.0, viewport.ViewportWidth);
        Assert.Equal(400.0, viewport.ViewportHeight);
        Assert.Equal(400.0, viewport.VerticalViewportSize);
        Assert.Equal(5000.0, viewport.MaxScrollExtent);
    }

    #endregion

    #region SetViewportSize 测试

    [Fact]
    public void SetViewportSize_UpdatesDimensions()
    {
        // Arrange
        var viewport = new PianoRollViewport();

        // Act
        viewport.SetViewportSize(1024, 768);

        // Assert
        Assert.Equal(1024.0, viewport.ViewportWidth);
        Assert.Equal(768.0, viewport.ViewportHeight);
        Assert.Equal(768.0, viewport.VerticalViewportSize);
    }

    [Fact]
    public void SetViewportSize_ClampsScrollOffsets()
    {
        // Arrange
        var viewport = new PianoRollViewport();
        viewport.SetViewportSize(800, 400);
        viewport.CurrentScrollOffset = 10000; // 超出范围

        // Act
        viewport.SetViewportSize(800, 400);

        // Assert - 滚动偏移应该被限制在有效范围内
        Assert.True(viewport.CurrentScrollOffset <= viewport.MaxScrollExtent);
    }

    #endregion

    #region 滚动偏移量测试

    [Fact]
    public void CurrentScrollOffset_NegativeValue_NotAutoClamped()
    {
        // Arrange
        var viewport = new PianoRollViewport();
        viewport.SetViewportSize(800, 400);

        // Act - ObservableProperty 不自动钳位，需要调用 ValidateAndClampScrollOffsets
        viewport.CurrentScrollOffset = -100;

        // Assert - 直接设置不会自动钳位
        Assert.Equal(-100.0, viewport.CurrentScrollOffset);

        // 调用验证方法后才会钳位
        viewport.ValidateAndClampScrollOffsets();
        Assert.Equal(0.0, viewport.CurrentScrollOffset);
    }

    [Fact]
    public void VerticalScrollOffset_NegativeValue_NotAutoClamped()
    {
        // Arrange
        var viewport = new PianoRollViewport();

        // Act
        viewport.VerticalScrollOffset = -50;

        // Assert - 直接设置不会自动钳位
        Assert.Equal(-50.0, viewport.VerticalScrollOffset);

        // 调用验证方法后才会钳位
        viewport.ValidateAndClampScrollOffsets();
        Assert.Equal(0.0, viewport.VerticalScrollOffset);
    }

    #endregion

    #region MaxScrollExtent 测试

    [Fact]
    public void UpdateMaxScrollExtent_UpdatesMaxScroll()
    {
        // Arrange
        var viewport = new PianoRollViewport();

        // Act
        viewport.UpdateMaxScrollExtent(10000);

        // Assert
        Assert.Equal(10000.0, viewport.ContentWidth);
        Assert.True(viewport.MaxScrollExtent >= 10000);
    }

    [Fact]
    public void MaxScrollExtent_MinimumIsViewportWidth()
    {
        // Arrange
        var viewport = new PianoRollViewport();
        viewport.SetViewportSize(800, 400);

        // Act - 内容宽度小于视口宽度
        viewport.UpdateMaxScrollExtent(200);

        // Assert - 最大滚动范围至少等于视口宽度
        Assert.True(viewport.MaxScrollExtent >= 800);
    }

    #endregion

    #region TimelinePosition 测试

    [Fact]
    public void TimelinePosition_DefaultIsZero()
    {
        // Arrange & Act
        var viewport = new PianoRollViewport();

        // Assert
        Assert.Equal(0.0, viewport.TimelinePosition);
    }

    [Fact]
    public void TimelinePosition_CanBeUpdated()
    {
        // Arrange
        var viewport = new PianoRollViewport();

        // Act
        viewport.TimelinePosition = 4.5;

        // Assert
        Assert.Equal(4.5, viewport.TimelinePosition);
    }

    #endregion

    #region GetScrollPercentage 测试

    [Fact]
    public void GetScrollPercentage_AtStart_ReturnsZero()
    {
        // Arrange
        var viewport = new PianoRollViewport();
        viewport.SetViewportSize(800, 400);
        viewport.UpdateMaxScrollExtent(5000);

        // Act
        var percentage = viewport.GetScrollPercentage();

        // Assert
        Assert.Equal(0.0, percentage, 2);
    }

    [Fact]
    public void GetScrollPercentage_AtEnd_ReturnsOne()
    {
        // Arrange
        var viewport = new PianoRollViewport();
        viewport.SetViewportSize(800, 400);
        viewport.UpdateMaxScrollExtent(5000);
        viewport.CurrentScrollOffset = viewport.MaxScrollExtent - viewport.ViewportWidth;

        // Act
        var percentage = viewport.GetScrollPercentage();

        // Assert
        Assert.Equal(1.0, percentage, 2);
    }

    #endregion

    #region UpdateViewportForEventView 测试

    [Fact]
    public void UpdateViewportForEventView_Visible_ReducesHeight()
    {
        // Arrange
        var viewport = new PianoRollViewport();
        viewport.SetViewportSize(800, 800);

        // Act
        viewport.UpdateViewportForEventView(true);

        // Assert - 事件视图可见时，垂直视口大小应该减小
        Assert.True(viewport.VerticalViewportSize < 800);
    }

    [Fact]
    public void UpdateViewportForEventView_Hidden_RestoresHeight()
    {
        // Arrange
        var viewport = new PianoRollViewport();
        viewport.SetViewportSize(800, 800);
        viewport.UpdateViewportForEventView(true);

        // Act
        viewport.UpdateViewportForEventView(false);

        // Assert - 事件视图隐藏时，垂直视口大小应该恢复
        Assert.Equal(800.0, viewport.VerticalViewportSize);
    }

    #endregion
}
