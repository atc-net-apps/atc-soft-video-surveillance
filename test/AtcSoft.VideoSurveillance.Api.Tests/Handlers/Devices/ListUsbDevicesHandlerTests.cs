namespace AtcSoft.VideoSurveillance.Api.Handlers.Devices;

using ApiUsbDeviceDescriptor = global::VideoSurveillance.Generated.Devices.Models.UsbDeviceDescriptor;
using CoreUsbDeviceDescriptor = global::AtcSoft.VideoSurveillance.Models.UsbDeviceDescriptor;

public class ListUsbDevicesHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_NullEnumerator_Returns503()
    {
        var handler = new ListUsbDevicesHandler(NullUsbCameraEnumerator.Instance);

        var result = await handler.ExecuteAsync(CancellationToken.None);

        result.Result.Should().BeOfType<ProblemHttpResult>();
    }

    [Fact]
    public async Task ExecuteAsync_LiveEnumerator_ReturnsMappedDescriptors()
    {
        var stub = Substitute.For<IUsbCameraEnumerator>();
        stub.EnumerateDevices(Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new CoreUsbDeviceDescriptor(
                    deviceId: "abc",
                    friendlyName: "Logitech BRIO",
                    vendorId: "046d",
                    productId: "085e"),
            });
        var handler = new ListUsbDevicesHandler(stub);

        var result = await handler.ExecuteAsync(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<List<ApiUsbDeviceDescriptor>>>().Subject;
        ok.Value.Should().HaveCount(1);
        ok.Value![0].DeviceId.Should().Be("abc");
        ok.Value[0].FriendlyName.Should().Be("Logitech BRIO");
        ok.Value[0].VendorId.Should().Be("046d");
        ok.Value[0].ProductId.Should().Be("085e");
        ok.Value[0].IsPresent.Should().BeTrue();
    }
}