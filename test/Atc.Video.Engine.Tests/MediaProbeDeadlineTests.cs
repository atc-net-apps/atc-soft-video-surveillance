namespace Atc.Video.Engine;

/// <summary>
/// Probing a recording on a share that stops answering gives up instead of holding its thread for
/// ever (the recordings browser's lengths, the cleanup, the previews). A source that takes the
/// connection and then sends nothing stands in for the share.
/// </summary>
public sealed class MediaProbeDeadlineTests
{
    [Fact]
    public async Task Probing_A_File_That_Never_Answers_Gives_Up()
    {
        TestEngine.Require();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);

        var probing = Task.Run(() => MediaProbe.GetDuration($"tcp://127.0.0.1:{port}/recording.mkv"), TestContext.Current.CancellationToken);
        using var silent = await accept;

        var finished = await Task.WhenAny(probing, Task.Delay(TimeSpan.FromSeconds(40), TestContext.Current.CancellationToken));

        finished.Should().BeSameAs(probing, "the probe gives up after its time limit");
        (await probing).Should().Be(TimeSpan.Zero);
    }
}