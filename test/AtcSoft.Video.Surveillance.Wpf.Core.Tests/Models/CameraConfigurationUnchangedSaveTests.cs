namespace AtcSoft.Video.Surveillance.Wpf.Core.Models;

/// <summary>
/// Saving the camera dialog copies the edited camera onto the one the tile shows. The tile restarts
/// its stream (and its recording) when the connection, the sign-in or the stream settings change, so a
/// save that changed none of them must not say they changed.
/// </summary>
public sealed class CameraConfigurationUnchangedSaveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Copying_An_Unchanged_Camera_Reports_No_Change_That_Restarts_The_Stream(bool usb)
    {
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = "Door";
        camera.Connection.IpAddress = "192.168.1.20";
        camera.Connection.Port = 554;
        camera.Connection.Path = "stream1";
        camera.Authentication.UserName = "admin";
        camera.Authentication.Password = "secret";
        if (usb)
        {
            camera.Connection.Source = AtcSoft.Video.Surveillance.Enums.CameraSource.Usb;
            camera.Connection.Usb = new AtcSoft.Video.Surveillance.Models.Settings.UsbConnectionSettings
            {
                DeviceId = @"\\?\usb#vid_046d&pid_0825",
                FriendlyName = "HD Webcam",
            };
        }

        var edited = camera.Clone();
        var changed = new List<string?>();
        camera.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        camera.CopyFrom(edited);

        changed.Should().NotContain(
            [nameof(CameraConfiguration.Connection), nameof(CameraConfiguration.Authentication), nameof(CameraConfiguration.Stream)],
            "nothing that needs a new stream changed");
    }

    [Fact]
    public void Copying_A_Changed_Address_Reports_The_Connection_Changed()
    {
        var camera = new CameraConfiguration();
        camera.Connection.IpAddress = "192.168.1.20";
        var edited = camera.Clone();
        edited.Connection.IpAddress = "192.168.1.21";
        var changed = new List<string?>();
        camera.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        camera.CopyFrom(edited);

        changed.Should().Contain(nameof(CameraConfiguration.Connection));
    }
}