namespace AtcSoft.Video.Surveillance.Wpf.Core.Helpers;

using CameraConfiguration = AtcSoft.Video.Surveillance.Wpf.Core.Models.CameraConfiguration;

/// <summary>
/// The options of either of a camera's streams are the camera's: only the address differs.
/// </summary>
public sealed class CameraStreamOptionsStreamTests
{
    [Fact]
    public void Both_Streams_Are_Opened_With_The_Camera_Settings_And_Decoded()
    {
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = "Gate";
        camera.Connection.IpAddress = "192.0.2.10";
        camera.Connection.SubStreamPath = "/stream2";

        var main = CameraStreamOptions.For(camera, hardwareAcceleration: true, "Auto", VideoTranscodeCodec.None, CameraStreamRole.Main);
        var sub = CameraStreamOptions.For(camera, hardwareAcceleration: true, "Auto", VideoTranscodeCodec.None, CameraStreamRole.Sub);

        (sub.Source, sub.RtspTransport, sub.HardwareAcceleration).Should().Be((main.Source, main.RtspTransport, main.HardwareAcceleration));
        sub.DecodeVideo.Should().BeTrue("the tile shows and analyses the sub-stream");
        main.DecodeVideo.Should().BeTrue("the pipeline turns decoding off for the main stream it holds");
    }
}