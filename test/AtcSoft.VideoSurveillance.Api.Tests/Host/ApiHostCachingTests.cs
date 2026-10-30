namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// The USB device list is kept a few seconds (x-cache-type: output), so a
/// second read right after the first is answered from the cache.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostCachingTests(ApiHostFixture host)
{
    [Fact]
    public async Task A_Second_Read_Of_The_Usb_Devices_Comes_From_The_Cache()
    {
        using var client = host.CreateClient();

        using var first = await client.GetAsync(new Uri("/devices/usb", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.SkipWhen(first.StatusCode == HttpStatusCode.ServiceUnavailable, "USB cameras are not supported on this host.");
        using var second = await client.GetAsync(new Uri("/devices/usb", UriKind.Relative), TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        // The output cache says how old a cached answer is.
        second.Headers.Age.Should().NotBeNull();
    }
}