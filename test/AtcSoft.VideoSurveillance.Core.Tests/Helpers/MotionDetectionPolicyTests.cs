namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// What a camera's motion detection runs with: the application's settings,
/// with whatever the camera itself overrides.
/// </summary>
public sealed class MotionDetectionPolicyTests
{
    [Theory]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void A_Camera_Records_On_Motion_When_It_Or_The_Application_Says_So(
        bool? cameraSetting,
        bool applicationSetting,
        bool expected)
    {
        var camera = new CameraConfiguration();
        if (cameraSetting.HasValue)
        {
            camera.Overrides = new CameraOverrides();
            camera.Overrides.Recording.EnableRecordingOnMotion = cameraSetting;
        }

        MotionDetectionPolicy.ShouldRecordOnMotion(camera, applicationSetting).Should().Be(expected);
    }

    [Fact]
    public void A_Camera_Without_Overrides_Runs_With_The_Application_Settings()
    {
        var application = new MotionDetectionSettings { Sensitivity = 42, MinimumChangePercent = 1.5, PostMotionDurationSeconds = 7 };

        var effective = MotionDetectionPolicy.Resolve(new CameraConfiguration(), application);

        effective.Sensitivity.Should().Be(42);
        effective.MinimumChangePercent.Should().Be(1.5);
        effective.PostMotionDurationSeconds.Should().Be(7);
        effective.Should().NotBeSameAs(application, "a change to the copy must not change the application's settings");
    }

    [Fact]
    public void What_A_Camera_Overrides_Wins_And_The_Rest_Comes_From_The_Application()
    {
        var application = new MotionDetectionSettings { Sensitivity = 42, AnalysisWidth = 800, AnalysisHeight = 600, CooldownSeconds = 5 };
        application.BoundingBox.MinArea = 10;
        application.BoundingBox.Padding = 4;
        var camera = new CameraConfiguration { Overrides = new CameraOverrides() };
        camera.Overrides.MotionDetection.Sensitivity = 80;
        camera.Overrides.MotionDetection.AnalysisWidth = 320;
        camera.Overrides.MotionDetection.AnalysisHeight = 240;
        camera.Overrides.MotionDetection.BoundingBox.MinArea = 500;

        var effective = MotionDetectionPolicy.Resolve(camera, application);

        effective.Sensitivity.Should().Be(80);
        effective.AnalysisWidth.Should().Be(320);
        effective.AnalysisHeight.Should().Be(240);
        effective.CooldownSeconds.Should().Be(5);
        effective.BoundingBox.MinArea.Should().Be(500);
        effective.BoundingBox.Padding.Should().Be(4);
    }

    [Fact]
    public void A_Camera_Can_Have_Its_Own_Smallest_Object_Stopped_Time_And_Lighting_Choice()
    {
        var application = new MotionDetectionSettings { SmallestObjectPercent = 0.05, StoppedObjectSeconds = 90, IgnoreLightingChanges = true };
        var camera = new CameraConfiguration { Overrides = new CameraOverrides() };

        var inherited = MotionDetectionPolicy.Resolve(camera, application);
        camera.Overrides.MotionDetection.SmallestObjectPercent = 1;
        camera.Overrides.MotionDetection.StoppedObjectSeconds = 300;
        camera.Overrides.MotionDetection.IgnoreLightingChanges = false;
        var own = MotionDetectionPolicy.Resolve(camera, application);

        (inherited.SmallestObjectPercent, inherited.StoppedObjectSeconds, inherited.IgnoreLightingChanges).Should().Be((0.05, 90, true));
        (own.SmallestObjectPercent, own.StoppedObjectSeconds, own.IgnoreLightingChanges).Should().Be((1d, 300, false));
        camera.Overrides.MotionDetection.HasAnyOverride().Should().BeTrue();
        camera.Overrides.MotionDetection.Clone().ValueEquals(camera.Overrides.MotionDetection).Should().BeTrue();
        new MotionDetectionOverrides { IgnoreLightingChanges = true }.ValueEquals(camera.Overrides.MotionDetection).Should().BeFalse();
    }
}