namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// A recording whose output can take nothing more says so and stops counting as recording,
/// instead of dropping every packet while it claims to run. A full disk cannot be made in a
/// test, so the output here is a connection the other side closes: the same broken output.
/// Runs against a live test stream from the FFmpeg command-line tool; skips without it.
/// </summary>
public sealed class RecordingWriteErrorTests
{
    [Fact]
    public async Task A_Recording_Whose_Output_Breaks_Reports_It_And_Stops_Recording()
    {
        TestEngine.Require();
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var cameraPort = GetFreePort();
        using var camera = StartCamera(tool!, cameraPort);
        using var output = new TcpListener(IPAddress.Loopback, 0);
        output.Start();
        var outputPort = ((IPEndPoint)output.LocalEndpoint).Port;

        using var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance);
        try
        {
            player.Open(new Uri($"tcp://127.0.0.1:{cameraPort}"), new StreamOptions { HardwareAcceleration = false });
            await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, TimeSpan.FromSeconds(20), cancellationToken);

            var accept = output.AcceptTcpClientAsync(cancellationToken);
            player.StartRecording($"tcp://127.0.0.1:{outputPort}/recording.mkv");
            using (var reader = await accept)
            {
                // Read a little of the recording, then go away.
                var buffer = new byte[4096];
                var read = await reader.GetStream().ReadAtLeastAsync(buffer, 1, throwOnEndOfStream: false, cancellationToken);
                read.Should().BeGreaterThan(0, "the recording reached the output before it broke");
            }

            output.Stop();
            await WaitUntilAsync(() => player.RecordingError is not null, TimeSpan.FromSeconds(30), cancellationToken);

            player.RecordingError.Should().NotBeNullOrEmpty();
            player.IsRecording.Should().BeFalse("nothing reaches the output any more");
        }
        finally
        {
            player.StopRecording();
            player.Close();
            Stop(camera);
        }
    }

    private static Process StartCamera(
        string tool,
        int port)
        => Process.Start(new ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                "-hide_banner -loglevel error -re -f lavfi -i testsrc=size=640x480:rate=25 " +
                "-c:v libx264 -preset ultrafast -tune zerolatency -g 25 -pix_fmt yuv420p -t 60 " +
                $"-f mpegts \"tcp://127.0.0.1:{port}?listen=1\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("The test stream could not be started.");

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }
}