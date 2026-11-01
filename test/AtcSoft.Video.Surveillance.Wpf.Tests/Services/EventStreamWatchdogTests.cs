namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// An event stream that is silent past its heartbeats, or that the computer's
/// wake-up ends, fails its read as a broken connection does, so the reader
/// opens it again after the last event.
/// </summary>
public sealed class EventStreamWatchdogTests
{
    [Fact]
    public async Task A_Stream_Silent_Past_Its_Heartbeats_Fails_As_A_Broken_Connection()
    {
        var time = new FakeTimeProvider();
        var watchdog = new EventStreamWatchdog(time);
        using var silent = new SilentStream();
        using var stream = watchdog.Watch(silent);

        var read = stream.ReadAsync(new byte[16], TestContext.Current.CancellationToken).AsTask();
        time.Advance(EventStreamWatchdog.IdleTimeout - TimeSpan.FromSeconds(1));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        read.IsCompleted.Should().BeFalse();

        time.Advance(TimeSpan.FromSeconds(1));

        await read.Invoking(r => r).Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task A_Stream_That_Sends_Is_Read()
    {
        var watchdog = new EventStreamWatchdog(new FakeTimeProvider());
        using var stream = watchdog.Watch(new MemoryStream("event: ping\n\n"u8.ToArray()));
        using var reader = new StreamReader(stream);

        var line = await reader.ReadLineAsync(TestContext.Current.CancellationToken);

        line.Should().Be("event: ping");
    }

    [Fact]
    public async Task A_Wake_Up_Ends_Every_Open_Stream()
    {
        var watchdog = new EventStreamWatchdog(new FakeTimeProvider());
        using var silent = new SilentStream();
        using var stream = watchdog.Watch(silent);
        var read = stream.ReadAsync(new byte[16], TestContext.Current.CancellationToken).AsTask();

        watchdog.AbortAll();

        await read.Invoking(r => r).Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task The_Reader_Leaving_Is_No_Broken_Connection()
    {
        var watchdog = new EventStreamWatchdog(new FakeTimeProvider());
        using var silent = new SilentStream();
        using var stream = watchdog.Watch(silent);
        using var leaving = new CancellationTokenSource();
        var read = stream.ReadAsync(new byte[16], leaving.Token).AsTask();

        await leaving.CancelAsync();

        await read.Invoking(r => r).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Only_The_Event_Stream_Is_Watched()
    {
        using var watchdog = new HandlerUnderTest();
        using var events = new HttpRequestMessage(HttpMethod.Get, "http://server.test/events");
        ApiRequest.Stamp(events, ApiOperations.ListEvents, ApiOperationSecurities.ListEvents);
        using var cameras = new HttpRequestMessage(HttpMethod.Get, "http://server.test/cameras");
        ApiRequest.Stamp(cameras, ApiOperations.ListCameras, ApiOperationSecurities.ListCameras);

        using var eventsResponse = await watchdog.SendAsync(events, TestContext.Current.CancellationToken);
        using var camerasResponse = await watchdog.SendAsync(cameras, TestContext.Current.CancellationToken);

        (await eventsResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken)).GetType().Name.Should().Be("IdleTimeoutStream");
        eventsResponse.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        (await camerasResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken)).GetType().Name.Should().NotBe("IdleTimeoutStream");
    }

    // A stream whose read waits until it is cancelled, as a dead connection's does.
    private sealed class SilentStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
            => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(
            long offset,
            SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
            => throw new NotSupportedException();
    }

    private sealed class HandlerUnderTest : IDisposable
    {
        private readonly HttpMessageInvoker invoker = new(
            new EventStreamWatchdogHandler(new EventStreamWatchdog(new FakeTimeProvider()))
            {
                InnerHandler = new AnswerHandler(),
            });

        public Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => invoker.SendAsync(request, cancellationToken);

        public void Dispose()
            => invoker.Dispose();
    }

    private sealed class AnswerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("event: ping\n\n", System.Text.Encoding.UTF8, "text/event-stream"),
                RequestMessage = request,
            });
    }
}