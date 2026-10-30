namespace AtcSoft.VideoSurveillance.Models.Settings;

public class RecordingSettingsTests
{
    [Fact]
    public void New_Instance_Has_Expected_Defaults()
    {
        // Act
        var settings = new RecordingSettings();

        // Assert
        settings.RecordingFormat.Should().Be("mkv");
        settings.TranscodeVideoCodec.Should().Be(VideoTranscodeCodec.None);
        settings.EnableRecordingOnMotion.Should().BeFalse();
        settings.EnableRecordingOnConnect.Should().BeFalse();
        settings.Cleanup.Should().NotBeNull();
        settings.PlaybackOverlay.Should().NotBeNull();
        settings.EnableHourlySegmentation.Should().BeTrue();
        settings.MaxRecordingDurationMinutes.Should().Be(60);
        settings.ThumbnailTileCount.Should().Be(4);
        settings.UseDailySubfolders.Should().BeTrue();
        settings.SeparateThumbnailFolder.Should().BeTrue();
        settings.EnableTimelapse.Should().BeFalse();
        settings.TimelapseInterval.Should().Be("5m");
    }
}