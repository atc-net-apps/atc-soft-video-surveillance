namespace AtcSoft.VideoEngine.Capture;

/// <summary>
/// The live player's grey pictures for motion detection: at the size asked for, turned the way the
/// picture is displayed, in order, and cheaper than a PNG read back as grey. The source is a live
/// test stream from the FFmpeg command-line tool; a machine without it skips the test.
/// </summary>
public sealed class VideoPlayerGreyCaptureTests
{
    [Fact]
    public async Task A_Grey_Picture_Is_Upright_At_The_Size_Asked_For_In_Order_And_Cheaper_Than_A_Png()
    {
        TestEngine.Require();
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();

        // 1280 x 720, dark on the left half, white on the right.
        using var source = Process.Start(new ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                "-hide_banner -loglevel error -re -f lavfi -i color=c=black:s=1280x720:r=25 " +
                "-vf \"drawbox=x=640:y=0:w=640:h=720:color=white:t=fill\" -c:v mpeg4 -q:v 3 -g 25 -t 60 " +
                $"-f nut \"tcp://127.0.0.1:{port}?listen=1\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("The test stream could not be started.");

        try
        {
            using var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance);
            player.Open(new Uri($"tcp://127.0.0.1:{port}"), new StreamOptions { HardwareAcceleration = false });
            var started = Stopwatch.StartNew();
            while (!(player.State == PlayerState.Playing && player.FramesDecoded > 0))
            {
                started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20), "the player starts playing");
                await Task.Delay(100, cancellationToken);
            }

            // Past the first key frame: the decoder shows grey until it has one.
            await Task.Delay(1500, cancellationToken);
            var first = (await player.CaptureGreyAsync(80, 60, cancellationToken))!;
            await Task.Delay(300, cancellationToken);
            var second = (await player.CaptureGreyAsync(80, 60, cancellationToken))!;

            (first.Width, first.Height, first.Pixels.Length).Should().Be((80, 60, 80 * 60));
            first.Pixels[(30 * 80) + 10].Should().BeLessThan(40, "the left half is dark");
            first.Pixels[(30 * 80) + 70].Should().BeGreaterThan(215, "the right half is white");
            second.Position.Should().BeGreaterThan(first.Position, "a later picture is further in");

            // Turned a quarter clockwise: portrait, the white half at the bottom.
            player.SetRotation(VideoRotation.Rotate90);
            var turned = (await player.CaptureGreyAsync(60, 80, cancellationToken))!;
            turned.Pixels[(10 * 60) + 30].Should().BeLessThan(40);
            turned.Pixels[(70 * 60) + 30].Should().BeGreaterThan(215);
            player.SetRotation(VideoRotation.None);

            // The cost of the two ways to a grey picture, twenty each.
            var grey = Stopwatch.StartNew();
            for (var i = 0; i < 20; i++)
            {
                await player.CaptureGreyAsync(800, 600, cancellationToken);
            }

            grey.Stop();
            var png = Stopwatch.StartNew();
            for (var i = 0; i < 20; i++)
            {
                await player.CaptureFrameAsync(cancellationToken);
            }

            png.Stop();
            TestContext.Current.TestOutputHelper?.WriteLine($"Grey capture: {grey.Elapsed.TotalMilliseconds / 20:F1} ms; PNG capture (before it is read back as grey): {png.Elapsed.TotalMilliseconds / 20:F1} ms");
            grey.Elapsed.Should().BeLessThan(png.Elapsed, "no PNG is encoded");
        }
        finally
        {
            if (!source.HasExited)
            {
                source.Kill(entireProcessTree: true);
            }
        }
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}