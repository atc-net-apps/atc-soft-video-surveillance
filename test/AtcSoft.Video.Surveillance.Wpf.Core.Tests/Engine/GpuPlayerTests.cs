namespace AtcSoft.Video.Surveillance.Wpf.Core.Engine;

using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Atc.Video.Engine.Capture;
using Atc.Video.Engine.DirectX;

/// <summary>
/// The player on the graphics card, as CameraWall runs it: the stream decoded by D3D11VA (FFmpeg
/// takes the device's lock, as our picture work does), and pictures for motion analysis copied back
/// from the card. Runs against a live test stream from the FFmpeg command-line tool; skips without
/// it or without a graphics card that decodes.
/// </summary>
public sealed class GpuPlayerTests
{
    [Fact]
    public async Task A_Stream_Decoded_On_The_Graphics_Card_Gives_Pictures_For_Analysis()
    {
        var tool = RequireFFmpeg();
        using var accelerator = new D3D11AcceleratorFactory().TryCreate(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        Assert.SkipUnless(accelerator is not null, "No graphics card that decodes.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var camera = StartCamera(tool, port);
        var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance, accelerator);
        try
        {
            player.Open(new Uri($"tcp://127.0.0.1:{port}"), new StreamOptions { HardwareAcceleration = true });
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!(player.State == PlayerState.Playing && player.FramesDecoded > 25) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100, cancellationToken);
            }

            player.StreamInfo!.IsHardwareAccelerated.Should().BeTrue("the stream is decoded on the graphics card");

            // Analysis asks for pictures one after another, and now and then two at once.
            var pictures = new List<GreyFrame>();
            for (var i = 0; i < 20; i++)
            {
                var both = await Task.WhenAll(player.CaptureGreyAsync(160, 90, cancellationToken), player.CaptureGreyAsync(160, 90, cancellationToken));
                pictures.AddRange(both.OfType<GreyFrame>());
                await Task.Delay(50, cancellationToken);
            }

            pictures.Should().HaveCountGreaterThanOrEqualTo(20, "one of each pair at least is taken");
            pictures.Should().OnlyContain(p => p.Width == 160 && p.Height == 90 && p.Pixels.Length == 160 * 90);
            pictures.Should().Contain(p => p.Pixels.Distinct().Count() > 10, "the test pattern is in the pictures");
            player.State.Should().Be(PlayerState.Playing, "copying pictures back did not hold up decoding");

            // Snapshots from the card, two at once: read under the picture's lock, encoded after it.
            var snapshots = await Task.WhenAll(player.CaptureFrameAsync(cancellationToken), player.CaptureFrameAsync(cancellationToken));
            snapshots.Should().OnlyContain(png => png != null && png.Length > 1000);
            snapshots.Select(png => PngSize(png!)).Should().AllBeEquivalentTo((1280, 720), "each is the whole picture");
        }
        finally
        {
            player.Dispose();
            Stop(camera);
        }
    }

    // A PNG's size is in its header: width and height, big-endian, from byte 16.
    private static (int Width, int Height) PngSize(byte[] png)
        => (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));

    private static string RequireFFmpeg()
    {
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, "ffmpeg.exe");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");
        VideoEngineBootstrap.Initialize();
        return tool!;
    }

    private static Process StartCamera(
        string tool,
        int port)
        => Process.Start(new ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                "-hide_banner -loglevel error -re -f lavfi -i testsrc=size=1280x720:rate=25 " +
                "-c:v libx264 -preset ultrafast -tune zerolatency -g 25 -pix_fmt yuv420p -t 60 " +
                $"-f mpegts \"tcp://127.0.0.1:{port}?listen=1\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("The test stream could not be started.");

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