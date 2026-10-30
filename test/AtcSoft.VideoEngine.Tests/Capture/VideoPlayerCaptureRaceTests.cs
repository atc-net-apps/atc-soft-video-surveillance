namespace AtcSoft.VideoEngine.Capture;

/// <summary>
/// Captures overlap in the apps: the motion analysis, a snapshot and a thumbnail can ask one player
/// for a picture at the same time, and a camera can be closed while a capture runs. Neither may
/// bring the process down (it did: an access violation in the PNG encoder). The source is a live
/// test stream from the FFmpeg command-line tool; a machine without it skips the test.
/// </summary>
public sealed class VideoPlayerCaptureRaceTests
{
    [Fact]
    public async Task Captures_At_The_Same_Time_And_A_Dispose_Under_Them_Do_Not_Crash()
    {
        TestEngine.Require();
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = Process.Start(new ProcessStartInfo
        {
            FileName = tool,
            Arguments = $"-hide_banner -loglevel error -re -f lavfi -i testsrc=size=1280x720:rate=25 -c:v mpeg4 -q:v 3 -t 60 -f nut \"tcp://127.0.0.1:{port}?listen=1\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("The test stream could not be started.");

        var captured = 0;
        try
        {
            var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance);
            player.Open(new Uri($"tcp://127.0.0.1:{port}"), new StreamOptions { HardwareAcceleration = false });
            var started = Stopwatch.StartNew();
            while (!(player.State == PlayerState.Playing && player.FramesDecoded > 0))
            {
                started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20), "the player starts playing");
                await Task.Delay(100, cancellationToken);
            }

            // Eight at once, for three seconds.
            var until = DateTime.UtcNow.AddSeconds(3);
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(
                async () =>
                {
                    while (DateTime.UtcNow < until)
                    {
                        if (await player.CaptureFrameAsync(cancellationToken) is { Length: > 0 })
                        {
                            Interlocked.Increment(ref captured);
                        }
                    }
                },
                cancellationToken)));

            // And the player disposed while eight are running.
            var running = Enumerable.Range(0, 8).Select(_ => player.CaptureFrameAsync(cancellationToken)).ToList();
            player.Dispose();
            foreach (var capture in running)
            {
                try
                {
                    await capture;
                }
                catch (ObjectDisposedException)
                {
                    // Asked for after the player was gone: refused, not a crash.
                }
            }
        }
        finally
        {
            if (!source.HasExited)
            {
                source.Kill(entireProcessTree: true);
            }
        }

        captured.Should().BeGreaterThan(8, "the captures did their work");
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}