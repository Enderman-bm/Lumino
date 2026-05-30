using Xunit;
using Lumino.Models.Music;

namespace Lumino.EditorTests;

/// <summary>
/// Note 模型单元测试 - 覆盖音符数据模型的基本行为
/// </summary>
public class NoteTests
{
    #region 默认值测试

    [Fact]
    public void DefaultValues_InitializedCorrectly()
    {
        // Arrange & Act
        var note = new Note();

        // Assert
        Assert.Equal(0, note.Pitch);
        Assert.Equal(100, note.Velocity); // 默认力度为100
        Assert.Equal(0, note.TrackIndex);
        Assert.Equal(0, note.MidiChannel);
    }

    #endregion

    #region 属性设置测试

    [Fact]
    public void Pitch_CanBeSet()
    {
        // Arrange
        var note = new Note();

        // Act
        note.Pitch = 60;

        // Assert
        Assert.Equal(60, note.Pitch);
    }

    [Fact]
    public void Velocity_CanBeSet()
    {
        // Arrange
        var note = new Note();

        // Act
        note.Velocity = 100;

        // Assert
        Assert.Equal(100, note.Velocity);
    }

    [Fact]
    public void TrackIndex_CanBeSet()
    {
        // Arrange
        var note = new Note();

        // Act
        note.TrackIndex = 2;

        // Assert
        Assert.Equal(2, note.TrackIndex);
    }

    [Fact]
    public void StartPosition_CanBeSet()
    {
        // Arrange
        var note = new Note();
        var position = new MusicalFraction(1, 4);

        // Act
        note.StartPosition = position;

        // Assert
        Assert.Equal(position, note.StartPosition);
    }

    [Fact]
    public void Duration_CanBeSet()
    {
        // Arrange
        var note = new Note();
        var duration = new MusicalFraction(1, 8);

        // Act
        note.Duration = duration;

        // Assert
        Assert.Equal(duration, note.Duration);
    }

    #endregion

    #region Lyric 测试

    [Fact]
    public void Lyric_DefaultIsNull()
    {
        // Arrange & Act
        var note = new Note();

        // Assert
        Assert.Null(note.Lyric);
    }

    [Fact]
    public void Lyric_CanBeSet()
    {
        // Arrange
        var note = new Note();

        // Act
        note.Lyric = "do";

        // Assert
        Assert.Equal("do", note.Lyric);
    }

    #endregion
}
