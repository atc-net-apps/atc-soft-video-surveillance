namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// Closing CameraWall stops its recording: it asks first while any camera records or is set to
/// record (on motion or on connect), saying how many of the cameras that is, and closes without a
/// question when recording is off for every camera.
/// </summary>
public sealed class CloseConfirmationTests
{
    [Fact]
    public void With_Recording_Off_For_Every_Camera_It_Closes_Without_A_Question()
        => CloseConfirmation.Question(cameras: 4, active: 0, recordingNow: 0).Should().BeNull();

    [Fact]
    public void It_Asks_With_How_Many_Of_The_Cameras_Record_Or_Wait_For_Motion()
        => CloseConfirmation.Question(cameras: 4, active: 2, recordingNow: 1).Should()
            .Contain("2 of 4 cameras")
            .And.Contain("1 recording now");

    [Fact]
    public void A_Camera_Waiting_For_Motion_Counts_Though_None_Records_Now()
        => CloseConfirmation.Question(cameras: 3, active: 1, recordingNow: 0).Should().Contain("1 of 3 cameras");

    [Fact]
    public void Cameras_Count_When_They_Record_Or_Are_Set_To_Record()
    {
        // Four cameras: two with recording off, one set to record on motion that does not record
        // now, and one that motion has just started recording.
        var off1 = Camera(onMotion: false);
        var off2 = Camera(onMotion: false);
        var waiting = Camera(onMotion: true);
        var recording = Camera(onMotion: true);
        var settings = new RecordingSettings { EnableRecordingOnMotion = false, EnableRecordingOnConnect = false };

        var (now, active) = CloseConfirmation.Count([off1, off2, waiting, recording], [recording.Id], settings);

        now.Should().Be(1);
        active.Should().Be(2);
    }

    [Fact]
    public void A_Camera_Without_Its_Own_Setting_Follows_The_App()
    {
        var camera = new CameraConfiguration { Id = Guid.NewGuid() };

        CloseConfirmation.Count([camera], [], new RecordingSettings { EnableRecordingOnMotion = true }).Active.Should().Be(1);
        CloseConfirmation.Count([camera], [], new RecordingSettings { EnableRecordingOnConnect = true }).Active.Should().Be(1);
        CloseConfirmation.Count([camera], [], new RecordingSettings()).Active.Should().Be(0);
    }

    private static CameraConfiguration Camera(bool onMotion)
    {
        var camera = new CameraConfiguration { Id = Guid.NewGuid() };
        camera.Overrides.Recording.EnableRecordingOnMotion = onMotion;
        return camera;
    }
}