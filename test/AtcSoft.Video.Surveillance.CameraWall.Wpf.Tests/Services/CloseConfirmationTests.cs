namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// Closing CameraWall stops its recordings: it asks first while any camera records, and closes
/// without a question when it only shows the cameras.
/// </summary>
public sealed class CloseConfirmationTests
{
    [Fact]
    public void Only_Watching_It_Closes_Without_A_Question()
        => CloseConfirmation.Question(activeRecordings: 0).Should().BeNull();

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void While_Cameras_Record_It_Asks_And_Says_How_Many(int recordings)
        => CloseConfirmation.Question(recordings).Should()
            .Contain(recordings.ToString(CultureInfo.InvariantCulture))
            .And.Contain("stops their recordings");
}