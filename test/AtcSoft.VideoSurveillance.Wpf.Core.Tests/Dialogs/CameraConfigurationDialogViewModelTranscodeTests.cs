namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using CameraConfiguration = AtcSoft.VideoSurveillance.Wpf.Core.Models.CameraConfiguration;
using IWpfCoreSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService;

/// <summary>
/// One camera can be recorded as H.264 while the others are kept as they
/// are sent (or the other way round). The camera dialog is where that is
/// chosen.
/// </summary>
public sealed class CameraConfigurationDialogViewModelTranscodeTests
{
    [Fact]
    public void A_Camera_Follows_The_Application_Setting_Until_It_Gets_Its_Own()
    {
        var vm = BuildVm(new CameraConfiguration(), VideoTranscodeCodec.H264);

        vm.UseDefaultTranscodeVideoCodec.Should().BeTrue();
        vm.OverrideTranscodeVideoCodec.Should().Be("H264", "the field shows what the camera will do");
        vm.TranscodeVideoCodecItems.Keys.Should().Contain(["None", "H264"]);
    }

    [Fact]
    public void Switching_The_Default_Off_Gives_The_Camera_Its_Own_Value_Starting_At_The_Application_Setting()
    {
        var vm = BuildVm(new CameraConfiguration(), VideoTranscodeCodec.H264);

        vm.UseDefaultTranscodeVideoCodec = false;

        vm.Camera.Overrides!.Recording.TranscodeVideoCodec.Should().Be(VideoTranscodeCodec.H264);
        vm.UseDefaultTranscodeVideoCodec.Should().BeFalse();
    }

    [Fact]
    public void A_Camera_Can_Be_Recorded_As_H264_While_The_Application_Keeps_What_Cameras_Send()
    {
        var vm = BuildVm(new CameraConfiguration(), VideoTranscodeCodec.None);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.UseDefaultTranscodeVideoCodec = false;
        vm.OverrideTranscodeVideoCodec = "H264";

        vm.Camera.Overrides!.Recording.TranscodeVideoCodec.Should().Be(VideoTranscodeCodec.H264);
        vm.OverrideTranscodeVideoCodec.Should().Be("H264");
        changed.Should().Contain(nameof(CameraConfigurationDialogViewModel.OverrideTranscodeVideoCodec));
    }

    [Fact]
    public void Switching_Back_To_The_Default_Removes_The_Cameras_Own_Value()
    {
        var camera = new CameraConfiguration { Overrides = new CameraOverrides() };
        camera.Overrides.Recording.TranscodeVideoCodec = VideoTranscodeCodec.H264;
        var vm = BuildVm(camera, VideoTranscodeCodec.None);

        vm.UseDefaultTranscodeVideoCodec.Should().BeFalse();

        vm.UseDefaultTranscodeVideoCodec = true;

        vm.Camera.Overrides!.Recording.TranscodeVideoCodec.Should().BeNull();
        vm.OverrideTranscodeVideoCodec.Should().Be("None", "the field shows the application setting again");
    }

    [Fact]
    public void A_Value_That_Is_Not_A_Choice_Leaves_The_Camera_As_It_Was()
    {
        var vm = BuildVm(new CameraConfiguration(), VideoTranscodeCodec.None);
        vm.UseDefaultTranscodeVideoCodec = false;
        vm.OverrideTranscodeVideoCodec = "H264";

        vm.OverrideTranscodeVideoCodec = "Vp9";

        vm.Camera.Overrides!.Recording.TranscodeVideoCodec.Should().Be(VideoTranscodeCodec.H264);
    }

    private static CameraConfigurationDialogViewModel BuildVm(
        CameraConfiguration camera,
        VideoTranscodeCodec applicationSetting)
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings());
        settingsService.Connection.Returns(new ConnectionAppSettings());
        settingsService.Recording.Returns(new RecordingSettings { TranscodeVideoCodec = applicationSetting });
        settingsService.Performance.Returns(new PerformanceSettings());
        settingsService.MotionDetection.Returns(new MotionDetectionSettings());

        return new CameraConfigurationDialogViewModel(
            camera,
            isNew: true,
            [],
            settingsService,
            Substitute.For<IVideoPlayerFactory>(),
            NullUsbCameraEnumerator.Instance);
    }
}