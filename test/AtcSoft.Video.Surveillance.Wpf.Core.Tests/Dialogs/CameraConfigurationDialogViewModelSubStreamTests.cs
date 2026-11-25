namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

using CameraConfiguration = AtcSoft.Video.Surveillance.Wpf.Core.Models.CameraConfiguration;
using IWpfCoreSettingsService = AtcSoft.Video.Surveillance.Wpf.Core.Services.IApplicationSettingsService;

/// <summary>
/// The camera dialog edits the sub-stream's port in an integer box, where 0 is the main
/// stream's port.
/// </summary>
public sealed class CameraConfigurationDialogViewModelSubStreamTests
{
    [Fact]
    public void Port_Zero_Is_The_Main_Stream_Port()
    {
        var camera = new CameraConfiguration();
        camera.Connection.SubStreamPath = "/stream2";
        var vm = BuildVm(camera);

        vm.SubStreamPortValue.Should().Be(0);

        // The dialog edits its own copy of the camera.
        vm.SubStreamPortValue = 8554;
        vm.Camera.Connection.SubStreamPort.Should().Be(8554);

        vm.SubStreamPortValue = 0;
        vm.Camera.Connection.SubStreamPort.Should().BeNull();
        vm.Camera.Connection.HasSubStream.Should().BeTrue();
    }

    [Fact]
    public void The_Sub_Stream_Path_Tells_Whether_The_Camera_Has_One()
    {
        var camera = new CameraConfiguration();
        var changed = new List<string?>();
        camera.Connection.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        camera.Connection.SubStreamPath = "/stream2";

        camera.Connection.HasSubStream.Should().BeTrue();
        changed.Should().Contain(nameof(camera.Connection.HasSubStream));
    }

    private static CameraConfigurationDialogViewModel BuildVm(
        CameraConfiguration camera)
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings());
        settingsService.Connection.Returns(new ConnectionAppSettings());
        settingsService.Recording.Returns(new RecordingSettings());
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