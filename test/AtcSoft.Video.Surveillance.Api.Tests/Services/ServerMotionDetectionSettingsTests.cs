namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// Motion settings changed while the server runs (PUT /settings, a camera's overrides) reach the
/// running detection. Before, a camera kept the settings it was first watched with until it dropped
/// or the server restarted.
/// </summary>
public sealed class ServerMotionDetectionSettingsTests
{
    [Fact]
    public void New_Settings_Make_A_New_Detector_And_The_Same_Settings_Do_Not()
    {
        var made = new List<MotionDetectionSettings>();
        using var sut = new ServerMotionDetectionService(
            NullLogger<ServerMotionDetectionService>.Instance,
            Substitute.For<IMotionFrameReader>(),
            TimeProvider.System,
            runLoops: false,
            createDetector: s =>
            {
                made.Add(s);
                return new FrameDifferenceMotionDetector(s);
            });
        var cameraId = Guid.NewGuid();
        sut.StartDetection(cameraId, Substitute.For<IMediaPipeline>(), new MotionDetectionSettings { Sensitivity = 30, AnalysisWidth = 320 });

        sut.UseSettings(cameraId, new MotionDetectionSettings { Sensitivity = 30, AnalysisWidth = 320 });
        made.Should().HaveCount(1, "the same settings change nothing");

        sut.UseSettings(cameraId, new MotionDetectionSettings { Sensitivity = 70, AnalysisWidth = 320 });
        made.Should().HaveCount(2);
        made[^1].Sensitivity.Should().Be(70, "the new detector has the new settings");
    }
}