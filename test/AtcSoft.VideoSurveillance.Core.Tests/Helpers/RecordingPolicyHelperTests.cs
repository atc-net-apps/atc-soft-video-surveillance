namespace AtcSoft.VideoSurveillance.Helpers;

public class RecordingPolicyHelperTests
{
    [Fact]
    public void ShouldRecordOnConnect_Override_True_AppDefault_False_Returns_True()
    {
        // Arrange
        var camera = new CameraConfiguration();
        camera.Overrides.Recording.EnableRecordingOnConnect = true;

        // Act
        var result = RecordingPolicyHelper.ShouldRecordOnConnect(camera, appDefault: false);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRecordOnConnect_Override_False_AppDefault_True_Returns_False()
    {
        // Arrange
        var camera = new CameraConfiguration();
        camera.Overrides.Recording.EnableRecordingOnConnect = false;

        // Act
        var result = RecordingPolicyHelper.ShouldRecordOnConnect(camera, appDefault: true);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRecordOnConnect_Override_Null_AppDefault_True_Returns_True()
    {
        // Arrange
        var camera = new CameraConfiguration();

        // Act
        var result = RecordingPolicyHelper.ShouldRecordOnConnect(camera, appDefault: true);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRecordOnConnect_Override_Null_AppDefault_False_Returns_False()
    {
        // Arrange
        var camera = new CameraConfiguration();

        // Act
        var result = RecordingPolicyHelper.ShouldRecordOnConnect(camera, appDefault: false);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRecordOnConnect_Null_Camera_Throws()
    {
        // Act
        var act = () => RecordingPolicyHelper.ShouldRecordOnConnect(null!, appDefault: true);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResolveTranscodeCodec_Override_H264_AppDefault_None_Returns_H264()
    {
        // Arrange
        var camera = new CameraConfiguration();
        camera.Overrides.Recording.TranscodeVideoCodec = VideoTranscodeCodec.H264;

        // Act
        var result = RecordingPolicyHelper.ResolveTranscodeCodec(camera, appDefault: VideoTranscodeCodec.None);

        // Assert
        result.Should().Be(VideoTranscodeCodec.H264);
    }

    [Fact]
    public void ResolveTranscodeCodec_Override_None_AppDefault_H264_Returns_None()
    {
        // Arrange
        var camera = new CameraConfiguration();
        camera.Overrides.Recording.TranscodeVideoCodec = VideoTranscodeCodec.None;

        // Act
        var result = RecordingPolicyHelper.ResolveTranscodeCodec(camera, appDefault: VideoTranscodeCodec.H264);

        // Assert
        result.Should().Be(VideoTranscodeCodec.None);
    }

    [Fact]
    public void ResolveTranscodeCodec_Override_Null_AppDefault_H264_Returns_H264()
    {
        // Arrange
        var camera = new CameraConfiguration();

        // Act
        var result = RecordingPolicyHelper.ResolveTranscodeCodec(camera, appDefault: VideoTranscodeCodec.H264);

        // Assert
        result.Should().Be(VideoTranscodeCodec.H264);
    }

    [Fact]
    public void ResolveTranscodeCodec_Override_Null_AppDefault_None_Returns_None()
    {
        // Arrange
        var camera = new CameraConfiguration();

        // Act
        var result = RecordingPolicyHelper.ResolveTranscodeCodec(camera, appDefault: VideoTranscodeCodec.None);

        // Assert
        result.Should().Be(VideoTranscodeCodec.None);
    }

    [Fact]
    public void ResolveTranscodeCodec_Null_Camera_Throws()
    {
        // Act
        var act = () => RecordingPolicyHelper.ResolveTranscodeCodec(null!, appDefault: VideoTranscodeCodec.None);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}