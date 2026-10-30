namespace AtcSoft.VideoEngine.Windows.Interop;

/// <summary>
/// The probe against the real Media Foundation. A machine without cameras
/// answers with an empty list; what it must never do is fail.
/// </summary>
public sealed class MediaFoundationDeviceProbeTests
{
    [Fact]
    public void The_Source_Type_Key_Is_The_One_Media_Foundation_Defines()
    {
        // mfidl.h: DEFINE_GUID(MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
        //   0xc60ac5fe, 0x252a, 0x478f, 0xa0, 0xef, 0xbc, 0x8f, 0xa5, 0xf7, 0xca, 0xd3).
        // With any other key the enumeration finds no source type and fails
        // with MF_E_ATTRIBUTENOTFOUND.
        MediaFoundationInterop.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE
            .Should().Be(new Guid(0xc60ac5fe, 0x252a, 0x478f, 0xa0, 0xef, 0xbc, 0x8f, 0xa5, 0xf7, 0xca, 0xd3));
    }

    [Fact]
    public void Listing_The_Cameras_Of_This_Machine_Does_Not_Fail()
    {
        Assert.SkipUnless(HasMediaFoundation(), "Media Foundation is not installed on this machine.");

        var probe = new MediaFoundationDeviceProbe();

        var devices = probe.EnumerateVideoCaptureDevices();

        devices.Should().OnlyContain(device => !string.IsNullOrWhiteSpace(device.SymbolicLink));
    }

    private static bool HasMediaFoundation()
        => File.Exists(Path.Combine(Environment.SystemDirectory, "mf.dll")) &&
           File.Exists(Path.Combine(Environment.SystemDirectory, "mfplat.dll"));
}