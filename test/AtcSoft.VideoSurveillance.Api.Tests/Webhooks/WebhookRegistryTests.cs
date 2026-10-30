namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// The receivers of the server's events: those from its configuration, which
/// stay as they are, and those added through the API, which are kept in a
/// file in the data folder.
/// </summary>
public sealed class WebhookRegistryTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-webhooks-" + Guid.NewGuid().ToString("N"));
    private readonly IWebhookSender sender = Substitute.For<IWebhookSender>();
    private readonly WebhookOptions configured = new();
    private readonly EphemeralDataProtectionProvider keys = new();

    public WebhookRegistryTests()
    {
        Directory.CreateDirectory(folder);
    }

    public void Dispose()
    {
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void A_Receiver_From_The_Configuration_Is_Listed_First_And_Cannot_Be_Changed()
    {
        configured.Endpoints.Add(new WebhookEndpoint { Url = "https://configured.test/hook", Secret = "s3cret", Events = ["motion.*"] });
        var sut = CreateSut();
        sut.Add("https://added.test/hook", secret: null, events: null);

        var all = sut.GetAll();

        all.Should().HaveCount(2);
        all[0].Url.Should().Be("https://configured.test/hook");
        all[0].IsEditable.Should().BeFalse();
        all[0].HasSecret.Should().BeTrue();
        all[0].Events.Should().Equal("motion.*");
        all[1].IsEditable.Should().BeTrue();

        sut.Update(all[0].Id, "https://other.test/hook", secret: null, events: null).Outcome.Should().Be(WebhookChangeOutcome.NotEditable);
        sut.Delete(all[0].Id).Should().Be(WebhookChangeOutcome.NotEditable);
        CreateSut().GetAll()[0].Id.Should().Be(all[0].Id, "a client holds on to the id between two listings");
    }

    [Fact]
    public void An_Added_Receiver_Is_Still_There_After_A_Restart_With_Its_Secret()
    {
        var added = CreateSut().Add("https://added.test/hook", "s3cret", ["recording.*"]);

        var afterRestart = CreateSut();

        added.Outcome.Should().Be(WebhookChangeOutcome.Done);
        var listed = afterRestart.GetAll().Should().ContainSingle().Subject;
        listed.Should().BeEquivalentTo(added.Webhook);
        listed.HasSecret.Should().BeTrue();
        afterRestart.ActiveEndpoints.Should().ContainSingle().Which.Secret.Should().Be("s3cret");
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("ftp://files.test/hook")]
    [InlineData("/relative/hook")]
    public void A_Receiver_Needs_An_Http_Or_Https_Address(string address)
    {
        var sut = CreateSut();
        var existing = sut.Add("https://added.test/hook", secret: null, events: null).Webhook!;

        sut.Add(address, secret: null, events: null).Outcome.Should().Be(WebhookChangeOutcome.InvalidAddress);
        sut.Update(existing.Id, address, secret: null, events: null).Outcome.Should().Be(WebhookChangeOutcome.InvalidAddress);

        sut.GetAll().Should().ContainSingle().Which.Url.Should().Be("https://added.test/hook");
    }

    [Fact]
    public void Changing_A_Receiver_Without_Giving_A_Secret_Keeps_The_One_It_Has()
    {
        // The secret is never sent out, so a client that edits the address
        // has no secret to send back.
        var sut = CreateSut();
        var id = sut.Add("https://added.test/hook", "s3cret", events: null).Webhook!.Id;

        var changed = sut.Update(id, "https://moved.test/hook", secret: null, ["motion.started"]);

        changed.Outcome.Should().Be(WebhookChangeOutcome.Done);
        changed.Webhook!.Url.Should().Be("https://moved.test/hook");
        changed.Webhook.Events.Should().Equal("motion.started");
        changed.Webhook.HasSecret.Should().BeTrue();
        sut.ActiveEndpoints.Single().Secret.Should().Be("s3cret");
    }

    [Fact]
    public void An_Empty_Secret_Removes_It_And_A_New_One_Replaces_It()
    {
        var sut = CreateSut();
        var id = sut.Add("https://added.test/hook", "s3cret", events: null).Webhook!.Id;

        sut.Update(id, "https://added.test/hook", "n3w", events: null);
        sut.ActiveEndpoints.Single().Secret.Should().Be("n3w");

        sut.Update(id, "https://added.test/hook", string.Empty, events: null).Webhook!.HasSecret.Should().BeFalse();
        sut.ActiveEndpoints.Single().Secret.Should().BeNullOrEmpty();
    }

    [Fact]
    public void A_Removed_Receiver_Is_Gone_And_An_Unknown_One_Is_Not_Found()
    {
        var sut = CreateSut();
        var id = sut.Add("https://added.test/hook", secret: null, events: null).Webhook!.Id;

        sut.Delete(id).Should().Be(WebhookChangeOutcome.Done);

        sut.GetAll().Should().BeEmpty();
        CreateSut().GetAll().Should().BeEmpty();
        sut.Delete(id).Should().Be(WebhookChangeOutcome.NotFound);
        sut.Update(id, "https://added.test/hook", secret: null, events: null).Outcome.Should().Be(WebhookChangeOutcome.NotFound);
    }

    [Fact]
    public void Events_Are_Tidied_When_They_Are_Stored()
    {
        var added = CreateSut().Add("https://added.test/hook", secret: null, [" motion.* ", string.Empty, "MOTION.*", "recording.started"]);

        added.Webhook!.Events.Should().Equal("motion.*", "recording.started");
    }

    [Fact]
    public async Task A_Test_Event_Goes_To_The_One_Receiver_And_Says_Whether_It_Was_Taken()
    {
        var sut = CreateSut();
        var id = sut.Add("https://added.test/hook", "s3cret", ["motion.*"]).Webhook!.Id;
        sut.Add("https://other.test/hook", secret: null, events: null);
        sender.SendAsync(Arg.Any<WebhookEndpoint>(), Arg.Any<WebhookEvent>(), Arg.Any<CancellationToken>()).Returns(true);

        var delivered = await sut.SendTestAsync(id, TestContext.Current.CancellationToken);

        delivered.Should().BeTrue();
        await sender.Received(1).SendAsync(
            Arg.Is<WebhookEndpoint>(e => e.Url == "https://added.test/hook" && e.Secret == "s3cret"),
            Arg.Is<WebhookEvent>(e => e.Type == WebhookEventTypes.Test),
            Arg.Any<CancellationToken>());
        await sender.ReceivedWithAnyArgs(1).SendAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_Test_Event_For_An_Unknown_Receiver_Is_Not_Sent()
    {
        var delivered = await CreateSut().SendTestAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        delivered.Should().BeNull();
        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void A_File_That_Cannot_Be_Read_Leaves_The_Configured_Receivers_Working()
    {
        configured.Endpoints.Add(new WebhookEndpoint { Url = "https://configured.test/hook" });
        File.WriteAllText(Path.Combine(folder, "webhooks.json"), "{ not json");

        var sut = CreateSut();

        sut.GetAll().Should().ContainSingle().Which.Url.Should().Be("https://configured.test/hook");
    }

    [Fact]
    public void The_Secret_Of_An_Added_Receiver_Is_Encrypted_In_The_File_And_Read_Back()
    {
        CreateSut().Add("https://added.test/hook", "hook-secret-1234", events: null);

        var raw = File.ReadAllText(FilePath);
        raw.Should().NotContain("hook-secret-1234");
        raw.Should().Contain(SecretProtector.Prefix);

        CreateSut().ActiveEndpoints.Should().ContainSingle().Which.Secret.Should().Be("hook-secret-1234");
    }

    [Fact]
    public void A_Secret_Written_Before_Encryption_Is_Encrypted_When_The_File_Is_Read()
    {
        File.WriteAllText(FilePath, $$"""[{"id":"{{Guid.NewGuid()}}","url":"https://added.test/hook","secret":"hook-secret-1234","events":[]}]""");

        var sut = CreateSut();

        sut.ActiveEndpoints.Should().ContainSingle().Which.Secret.Should().Be("hook-secret-1234");
        File.ReadAllText(FilePath).Should().NotContain("hook-secret-1234");
        File.ReadAllText(FilePath + ".bak").Should().NotContain("hook-secret-1234");
    }

    [Fact]
    public void A_Secret_From_Other_Keys_Is_Read_As_None()
    {
        new WebhookRegistry(FilePath, configured, sender, new SecretProtector(new EphemeralDataProtectionProvider()), NullLogger<WebhookRegistry>.Instance)
            .Add("https://added.test/hook", "hook-secret-1234", events: null);

        var all = CreateSut().GetAll();

        all.Should().ContainSingle().Which.HasSecret.Should().BeFalse();
    }

    private string FilePath
        => Path.Combine(folder, "webhooks.json");

    private WebhookRegistry CreateSut()
        => new(
            FilePath,
            configured,
            sender,
            new SecretProtector(keys),
            NullLogger<WebhookRegistry>.Instance);
}