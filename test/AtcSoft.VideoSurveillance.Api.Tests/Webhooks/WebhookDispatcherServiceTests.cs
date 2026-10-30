namespace AtcSoft.VideoSurveillance.Api.Webhooks;

public sealed class WebhookDispatcherServiceTests
{
    private static readonly WebhookEvent Motion = new(WebhookEventTypes.MotionStarted, Guid.NewGuid(), "Front Door", DateTimeOffset.UtcNow, new global::VideoSurveillance.Generated.Models.WebhookPayloadData());
    private static readonly WebhookEvent Recording = new(WebhookEventTypes.RecordingStopped, Guid.NewGuid(), "Garden", DateTimeOffset.UtcNow, new global::VideoSurveillance.Generated.Models.WebhookPayloadData());

    private readonly IWebhookSender sender = Substitute.For<IWebhookSender>();
    private readonly IMotionDetectionService motionDetection = Substitute.For<IMotionDetectionService>();
    private WebhookRegistry? registry;

    [Fact]
    public async Task Each_Queued_Event_Goes_To_Every_Endpoint_That_Wants_It()
    {
        var all = new WebhookEndpoint { Url = "https://all.test/hook" };
        var motionOnly = new WebhookEndpoint { Url = "https://motion.test/hook", Events = ["motion.*"] };
        using var sut = CreateSut(all, motionOnly);

        sut.Enqueue(Motion).Should().BeTrue();
        sut.Enqueue(Recording).Should().BeTrue();
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        await sender.Received(1).SendAsync(all, Motion, Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync(all, Recording, Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync(motionOnly, Motion, Arg.Any<CancellationToken>());
        await sender.DidNotReceive().SendAsync(motionOnly, Recording, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_Event_Is_Sent_Once()
    {
        using var sut = CreateSut(new WebhookEndpoint { Url = "https://all.test/hook" });
        sut.Enqueue(Motion);

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        await sender.ReceivedWithAnyArgs(1).SendAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Without_Endpoints_Nothing_Is_Queued_Or_Sent()
    {
        using var sut = CreateSut();

        sut.Enqueue(Motion).Should().BeFalse();
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task An_Endpoint_With_An_Invalid_Address_Is_Left_Out()
    {
        var valid = new WebhookEndpoint { Url = "https://all.test/hook" };
        using var sut = CreateSut(new WebhookEndpoint { Url = "nonsense" }, valid);
        sut.Enqueue(Motion);

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        await sender.Received(1).SendAsync(valid, Motion, Arg.Any<CancellationToken>());
        await sender.ReceivedWithAnyArgs(1).SendAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task One_Endpoint_Failing_Does_Not_Keep_The_Event_From_The_Others()
    {
        var broken = new WebhookEndpoint { Url = "https://broken.test/hook" };
        var working = new WebhookEndpoint { Url = "https://working.test/hook" };
        sender.SendAsync(broken, Arg.Any<WebhookEvent>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("unexpected"));
        using var sut = CreateSut(broken, working);
        sut.Enqueue(Motion);

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        await sender.Received(1).SendAsync(working, Motion, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Motion_Is_Sent_As_One_Start_And_One_Stop()
    {
        // While something moves, the detector reports every picture it
        // moved in (for live overlays). A receiver wants to hear of the
        // motion, not of each picture.
        var endpoint = new WebhookEndpoint { Url = "https://all.test/hook" };
        using var sut = CreateSut(endpoint);
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var camera = Guid.NewGuid();
        var otherCamera = Guid.NewGuid();

        RaiseMotion(camera, active: true);
        RaiseMotion(camera, active: true);
        RaiseMotion(otherCamera, active: true);
        RaiseMotion(camera, active: true);
        RaiseMotion(camera, active: false);
        RaiseMotion(camera, active: true);
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _ = sender.SendAsync(endpoint, Arg.Is<WebhookEvent>(e => e.Type == WebhookEventTypes.MotionStarted && e.CameraId == camera), Arg.Any<CancellationToken>());
            _ = sender.SendAsync(endpoint, Arg.Is<WebhookEvent>(e => e.Type == WebhookEventTypes.MotionStarted && e.CameraId == otherCamera), Arg.Any<CancellationToken>());
            _ = sender.SendAsync(endpoint, Arg.Is<WebhookEvent>(e => e.Type == WebhookEventTypes.MotionStopped && e.CameraId == camera), Arg.Any<CancellationToken>());
            _ = sender.SendAsync(endpoint, Arg.Is<WebhookEvent>(e => e.Type == WebhookEventTypes.MotionStarted && e.CameraId == camera), Arg.Any<CancellationToken>());
        });
        await sender.ReceivedWithAnyArgs(4).SendAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    private void RaiseMotion(
        Guid cameraId,
        bool active)
        => motionDetection.MotionDetected += Raise.EventWith(new MotionDetectedEventArgs(cameraId, 5, active));

    [Fact]
    public void A_Full_Queue_Drops_The_Oldest_Event_Instead_Of_Growing()
    {
        using var sut = CreateSut(new WebhookEndpoint { Url = "https://all.test/hook" });

        for (var i = 0; i < WebhookDispatcherService.QueueCapacity + 25; i++)
        {
            sut.Enqueue(Motion);
        }

        sut.QueuedCount.Should().Be(WebhookDispatcherService.QueueCapacity);
    }

    [Fact]
    public async Task A_Receiver_Added_While_The_Server_Runs_Gets_The_Next_Event()
    {
        using var sut = CreateSut();
        sut.Enqueue(Motion).Should().BeFalse("there is nobody to send it to yet");

        registry!.Add("https://added.test/hook", secret: null, events: null);
        sut.Enqueue(Motion).Should().BeTrue();
        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        await sender.Received(1).SendAsync(
            Arg.Is<WebhookEndpoint>(e => e.Url == "https://added.test/hook"),
            Motion,
            Arg.Any<CancellationToken>());
    }

    private WebhookDispatcherService CreateSut(
        params WebhookEndpoint[] endpoints)
    {
        registry = new WebhookRegistry(
            Path.Combine(Path.GetTempPath(), "atcsoft-webhooks-" + Guid.NewGuid().ToString("N"), "webhooks.json"),
            new WebhookOptions { Endpoints = [.. endpoints] },
            sender,
            new SecretProtector(new EphemeralDataProtectionProvider()),
            NullLogger<WebhookRegistry>.Instance);

        return new(
            NullLogger<WebhookDispatcherService>.Instance,
            new WebhookDispatcherServiceOptions(),
            registry,
            sender,
            Substitute.For<IRecordingService>(),
            motionDetection,
            Substitute.For<ICameraStorageService>(),
            Substitute.For<IApplicationSettingsService>());
    }
}