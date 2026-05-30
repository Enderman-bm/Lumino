using Xunit;
using Lumino.Services.Implementation;

namespace Lumino.EditorTests;

/// <summary>
/// PlaybackService 单元测试 - 覆盖播放控制和时间管理逻辑
/// </summary>
public class PlaybackServiceTests
{
    #region 初始状态测试

    [Fact]
    public void DefaultState_IsStopped()
    {
        // Arrange & Act
        var service = new PlaybackService();

        // Assert
        Assert.Equal(PlaybackState.Stopped, service.State);
        Assert.Equal(0.0, service.CurrentTime);
        Assert.Equal(0.0, service.TotalDuration);
        Assert.Equal(1.0, service.PlaybackSpeed);
        Assert.False(service.IsPlaying);
        Assert.False(service.IsPaused);
    }

    #endregion

    #region Seek 测试

    [Fact]
    public void Seek_UpdatesCurrentTime()
    {
        // Arrange
        var service = new PlaybackService();
        service.TotalDuration = 10.0;

        // Act
        service.Seek(5.0);

        // Assert
        Assert.Equal(5.0, service.CurrentTime);
    }

    [Fact]
    public void Seek_ClampsToZero()
    {
        // Arrange
        var service = new PlaybackService();
        service.TotalDuration = 10.0;

        // Act
        service.Seek(-1.0);

        // Assert
        Assert.Equal(0.0, service.CurrentTime);
    }

    [Fact]
    public void Seek_ClampsToTotalDuration()
    {
        // Arrange
        var service = new PlaybackService();
        service.TotalDuration = 10.0;

        // Act
        service.Seek(15.0);

        // Assert
        Assert.Equal(10.0, service.CurrentTime);
    }

    [Fact]
    public void Seek_FiresPlaybackTimeChanged()
    {
        // Arrange
        var service = new PlaybackService();
        service.TotalDuration = 10.0;
        var eventFired = false;
        service.PlaybackTimeChanged += (_, _) => eventFired = true;

        // Act
        service.Seek(5.0);

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void Seek_SameTime_DoesNotFireEvent()
    {
        // Arrange
        var service = new PlaybackService();
        service.TotalDuration = 10.0;
        service.Seek(5.0); // 先设置到5.0
        var eventFired = false;
        service.PlaybackTimeChanged += (_, _) => eventFired = true;

        // Act - 再次设置到相同的值（精度阈值内）
        service.Seek(5.0001);

        // Assert - 应该不触发事件（差异小于0.001阈值）
        Assert.False(eventFired);
    }

    #endregion

    #region Play/Pause/Stop 测试

    [Fact]
    public void Play_SetsStateToPlaying()
    {
        // Arrange
        var service = new PlaybackService();

        // Act
        service.Play();

        // Assert
        Assert.Equal(PlaybackState.Playing, service.State);
        Assert.True(service.IsPlaying);

        // Cleanup
        service.Stop();
    }

    [Fact]
    public void Pause_SetsStateToPaused()
    {
        // Arrange
        var service = new PlaybackService();
        service.Play();

        // Act
        service.Pause();

        // Assert
        Assert.Equal(PlaybackState.Paused, service.State);
        Assert.True(service.IsPaused);
        Assert.False(service.IsPlaying);
    }

    [Fact]
    public void Stop_SetsStateToStopped()
    {
        // Arrange
        var service = new PlaybackService();
        service.Play();

        // Act
        service.Stop();

        // Assert
        Assert.Equal(PlaybackState.Stopped, service.State);
        Assert.False(service.IsPlaying);
        Assert.False(service.IsPaused);
    }

    [Fact]
    public void Play_FiresPlaybackStateChanged()
    {
        // Arrange
        var service = new PlaybackService();
        PlaybackState? reportedState = null;
        service.PlaybackStateChanged += (_, e) => reportedState = e.State;

        // Act
        service.Play();

        // Assert
        Assert.Equal(PlaybackState.Playing, reportedState);

        // Cleanup
        service.Stop();
    }

    #endregion

    #region PlaybackSpeed 测试

    [Fact]
    public void PlaybackSpeed_DefaultIsOne()
    {
        // Arrange & Act
        var service = new PlaybackService();

        // Assert
        Assert.Equal(1.0, service.PlaybackSpeed);
    }

    [Fact]
    public void PlaybackSpeed_ClampedToMin()
    {
        // Arrange
        var service = new PlaybackService();

        // Act
        service.PlaybackSpeed = 0.01;

        // Assert
        Assert.Equal(0.1, service.PlaybackSpeed);
    }

    [Fact]
    public void PlaybackSpeed_ClampedToMax()
    {
        // Arrange
        var service = new PlaybackService();

        // Act
        service.PlaybackSpeed = 5.0;

        // Assert
        Assert.Equal(2.0, service.PlaybackSpeed);
    }

    #endregion

    #region Progress 测试

    [Fact]
    public void Progress_AtStart_ReturnsZero()
    {
        // Arrange
        var service = new PlaybackService();
        service.TotalDuration = 10.0;

        // Assert
        Assert.Equal(0.0, service.Progress);
    }

    [Fact]
    public void Progress_AtEnd_ReturnsOne()
    {
        // Arrange
        var service = new PlaybackService();
        service.TotalDuration = 10.0;
        service.Seek(10.0);

        // Assert
        Assert.Equal(1.0, service.Progress);
    }

    [Fact]
    public void Progress_AtMiddle_ReturnsHalf()
    {
        // Arrange
        var service = new PlaybackService();
        service.TotalDuration = 10.0;
        service.Seek(5.0);

        // Assert
        Assert.Equal(0.5, service.Progress);
    }

    #endregion

    #region TotalDuration 测试

    [Fact]
    public void TotalDuration_CanBeSet()
    {
        // Arrange
        var service = new PlaybackService();

        // Act
        service.TotalDuration = 120.0;

        // Assert
        Assert.Equal(120.0, service.TotalDuration);
    }

    [Fact]
    public void TotalDuration_NegativeValue_ClampedToZero()
    {
        // Arrange
        var service = new PlaybackService();

        // Act
        service.TotalDuration = -10.0;

        // Assert
        Assert.Equal(0.0, service.TotalDuration);
    }

    #endregion
}
