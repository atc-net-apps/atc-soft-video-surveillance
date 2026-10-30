namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class WebhookEventChoicesTests
{
    [Fact]
    public void The_Choices_Are_The_Three_Groups_Of_Events_The_Server_Sends()
        => WebhookEventChoices.Groups.Select(g => g.Pattern)
            .Should().Equal("camera.*", "recording.*", "motion.*");

    [Fact]
    public void A_Receiver_Without_A_Choice_Gets_Everything()
    {
        WebhookEventChoices.Describe(events: null).Should().Be("All events");
        WebhookEventChoices.Describe([]).Should().Be("All events");
    }

    [Fact]
    public void A_Receiver_Of_Every_Group_Gets_Everything_Too()
        => WebhookEventChoices.Describe(["camera.*", "recording.*", "motion.*"]).Should().Be("All events");

    [Fact]
    public void Chosen_Groups_Are_Named_In_The_Order_Of_The_Choices()
        => WebhookEventChoices.Describe(["motion.*", "camera.*"]).Should().Be("Camera connection, Motion");

    [Fact]
    public void A_Single_Event_Set_By_Hand_Is_Shown_As_It_Is()
    {
        // Set through the API or the configuration; the page only offers groups.
        WebhookEventChoices.Describe(["motion.started", "recording.*"]).Should().Be("Recording, motion.started");
    }

    [Fact]
    public void Ticking_Every_Group_Is_Stored_As_No_Choice()
    {
        // "All" then also covers events the server learns to send later.
        WebhookEventChoices.ToEvents(["camera.*", "recording.*", "motion.*"], kept: []).Should().BeEmpty();
        WebhookEventChoices.ToEvents([], kept: []).Should().BeEmpty();
    }

    [Fact]
    public void Ticked_Groups_Are_Stored_With_Whatever_Was_Set_By_Hand()
        => WebhookEventChoices.ToEvents(["motion.*"], kept: ["recording.started"])
            .Should().Equal("motion.*", "recording.started");

    [Fact]
    public void What_Was_Set_By_Hand_Is_Told_Apart_From_The_Groups()
    {
        var (groups, others) = WebhookEventChoices.Split(["MOTION.*", "recording.started", " camera.* "]);

        groups.Should().BeEquivalentTo("camera.*", "motion.*");
        others.Should().Equal("recording.started");
    }

    [Theory]
    [InlineData("https://example.test/hook", true)]
    [InlineData("http://192.168.1.10:8080/events", true)]
    [InlineData("", false)]
    [InlineData("example.test/hook", false)]
    [InlineData("ftp://example.test/hook", false)]
    public void An_Address_Has_To_Be_Http_Or_Https(
        string address,
        bool expected)
        => WebhookEventChoices.IsUsableAddress(address).Should().Be(expected);
}