namespace AtcSoft.VideoSurveillance.Services;

public class NullUsbCameraEnumeratorTests
{
    [Fact]
    public void EnumerateDevices_Returns_Empty_List()
    {
        var enumerator = new NullUsbCameraEnumerator();

        enumerator.EnumerateDevices(TestContext.Current.CancellationToken).Should().BeEmpty();
    }

    [Fact]
    public void FindByDeviceId_Returns_Null()
        => new NullUsbCameraEnumerator().FindByDeviceId("anything").Should().BeNull();

    [Fact]
    public void FindByFriendlyName_Returns_Null()
        => new NullUsbCameraEnumerator().FindByFriendlyName("anything").Should().BeNull();

    [Fact]
    public void Singleton_Is_Reusable()
    {
        NullUsbCameraEnumerator.Instance.Should().BeSameAs(NullUsbCameraEnumerator.Instance);
        NullUsbCameraEnumerator.Instance
            .EnumerateDevices(TestContext.Current.CancellationToken)
            .Should()
            .BeEmpty();
    }
}