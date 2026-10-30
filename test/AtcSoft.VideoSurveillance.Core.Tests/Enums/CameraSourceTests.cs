namespace AtcSoft.VideoSurveillance.Enums;

public class CameraSourceTests
{
    [Theory]
    [InlineData(CameraSource.Network)]
    [InlineData(CameraSource.Usb)]
    public void Enum_HasExpectedValues(CameraSource source)
    {
        Enum.IsDefined(typeof(CameraSource), source)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Enum_Network_Is_Default_Zero_Value()
    {
        // Default(CameraSource) is the value used by every camera that pre-dates
        // USB support, so it must remain `Network` to keep stored data correct.
        default(CameraSource).Should().Be(CameraSource.Network);
        ((int)CameraSource.Network).Should().Be(0);
    }

    [Fact]
    public void Enum_Roundtrips_Through_Json_As_Number()
    {
        var json = JsonSerializer.Serialize(CameraSource.Usb);
        var parsed = JsonSerializer.Deserialize<CameraSource>(json);

        parsed.Should().Be(CameraSource.Usb);
    }
}