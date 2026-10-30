namespace AtcSoft.VideoSurveillance.Api.Webhooks;

public sealed class WebhookEndpointTests
{
    [Fact]
    public void Without_A_List_Of_Events_An_Endpoint_Gets_Them_All()
    {
        var sut = new WebhookEndpoint { Url = "https://example.test/hook" };

        sut.Wants(WebhookEventTypes.MotionStarted).Should().BeTrue();
        sut.Wants(WebhookEventTypes.RecordingStopped).Should().BeTrue();
    }

    [Fact]
    public void With_A_List_An_Endpoint_Gets_Only_Those()
    {
        var sut = new WebhookEndpoint { Url = "https://example.test/hook", Events = ["motion.started", "CAMERA.DISCONNECTED"] };

        sut.Wants(WebhookEventTypes.MotionStarted).Should().BeTrue();
        sut.Wants(WebhookEventTypes.CameraDisconnected).Should().BeTrue("the names are not case sensitive");
        sut.Wants(WebhookEventTypes.MotionStopped).Should().BeFalse();
    }

    [Theory]
    [InlineData("motion.*", "motion.started", true)]
    [InlineData("motion.*", "motion.stopped", true)]
    [InlineData("motion.*", "recording.started", false)]
    public void A_Group_Can_Be_Asked_For_With_A_Star(
        string pattern,
        string type,
        bool expected)
        => new WebhookEndpoint { Url = "https://example.test/hook", Events = [pattern] }.Wants(type).Should().Be(expected);

    [Theory]
    [InlineData("https://hooks.example.test/services/T000/B000/secret-token?key=abc", "https://hooks.example.test")]
    [InlineData("http://192.168.1.5:8123/api/webhook/abc123", "http://192.168.1.5:8123")]
    [InlineData("not a url", "(invalid address)")]
    [InlineData("", "(invalid address)")]
    public void The_Name_Used_In_Logs_Leaves_Out_The_Path_And_The_Query(
        string address,
        string expected)
    {
        // Webhook addresses often carry their secret in the path or the query.
        new WebhookEndpoint { Url = address }.LogName.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.test/hook", true)]
    [InlineData("http://example.test/hook", true)]
    [InlineData("ftp://example.test/hook", false)]
    [InlineData("example.test/hook", false)]
    [InlineData("", false)]
    public void Only_Http_Addresses_Are_Valid(
        string address,
        bool expected)
        => new WebhookEndpoint { Url = address }.IsValid.Should().Be(expected);
}