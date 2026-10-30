namespace AtcSoft.VideoSurveillance.Wpf.Extensions;

using ApiMotionZone = global::VideoSurveillance.Generated.Cameras.Models.MotionZone;
using ApiMotionZoneKind = global::VideoSurveillance.Generated.Cameras.Models.MotionZoneKind;
using ApiZonePoint = global::VideoSurveillance.Generated.Cameras.Models.ZonePoint;

/// <summary>
/// A camera sent to the server keeps what the client did not change.
/// </summary>
public sealed class CameraModelMappingExtensionsTests
{
    [Fact]
    public void A_Motion_Zone_Goes_To_Core_And_Back_Unchanged()
    {
        var zone = new ApiMotionZone("Gate", ApiMotionZoneKind.Watch, [new ApiZonePoint(0.1, 0.2), new ApiZonePoint(0.4, 0.2), new ApiZonePoint(0.4, 0.6)], "Green", Sensitivity: 80, SmallestObjectPercent: 0.1);

        var core = zone.ToCoreZone();
        var back = core.ToApiZone();

        core.Kind.Should().Be(AtcSoft.VideoSurveillance.Enums.MotionZoneKind.Watch);
        core.Points[2].Should().Be(new ZonePoint(0.4, 0.6));
        back.Name.Should().Be("Gate");
        back.Kind.Should().Be(ApiMotionZoneKind.Watch);
        back.Points.Should().Equal(zone.Points);
        back.Color.Should().Be("Green");
        (core.Sensitivity, core.SmallestObjectPercent).Should().Be((80, 0.1));
        (back.Sensitivity, back.SmallestObjectPercent).Should().Be((80, 0.1));
        (TestCameras.Network() with { MotionZones = [zone] }).ToCameraConfiguration().MotionZones
            .Should().ContainSingle().Which.Name.Should().Be("Gate", "the tile's camera keeps the zones");
    }

    [Fact]
    public void A_Zone_Kind_The_Client_Does_Not_Know_Is_Read_As_Ignore()
        => new ApiMotionZone("Gate", new ApiMotionZoneKind("countOnly"), [new ApiZonePoint(0, 0), new ApiZonePoint(1, 0), new ApiZonePoint(1, 1)], Color: null)
            .ToCoreZone().Kind.Should().Be(AtcSoft.VideoSurveillance.Enums.MotionZoneKind.Ignore);

    [Fact]
    public void ToUpdateCameraRequest_Sends_The_Stream_Path()
    {
        var config = TestCameras.Network().ToCameraConfiguration();

        config.ToUpdateCameraRequest().Path.Should().Be("/stream1");
    }

    [Fact]
    public void ToCreateCameraRequest_Sends_The_Stream_Path()
    {
        var config = TestCameras.Network().ToCameraConfiguration();

        config.ToCreateCameraRequest().Path.Should().Be("/stream1");
    }

    [Fact]
    public void ToUpdateCameraRequest_Without_A_Password_Leaves_The_Password_Out()
    {
        var config = TestCameras.Network().ToCameraConfiguration();

        config.ToUpdateCameraRequest().Password.Should().BeNull();
    }

    [Fact]
    public void ToUpdateCameraRequest_Leaves_Out_The_Source_And_The_Usb_Settings()
    {
        var request = TestCameras.Network().ToCameraConfiguration().ToUpdateCameraRequest();

        request.Source.Should().BeNull();
        request.UsbDeviceId.Should().BeNull();
        request.UsbCaptureAudio.Should().BeNull();
    }
}