namespace BoundingBoxWpfApp;

/// <summary>
/// A video sent to the player as a camera would send it: the FFmpeg tool reads it at its own
/// pace (<c>-re</c>) and streams it over UDP to this machine, keeping its times (from 0), so the
/// player's picture times are the video's and the truth can be found by them. Ends with the
/// video, or when disposed.
/// </summary>
public sealed class LiveSender : IDisposable
{
    private readonly System.Diagnostics.Process process;
    private readonly StreamWriter log;
    private readonly Lock gate;

    private LiveSender(
        System.Diagnostics.Process process,
        StreamWriter log,
        Lock gate,
        int port)
    {
        this.process = process;
        this.log = log;
        this.gate = gate;
        Port = port;
    }

    /// <summary>
    /// Gets the UDP port the stream goes to.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Gets whether the tool has stopped sending (the video ended, or it failed).
    /// </summary>
    public bool HasEnded
        => process.HasExited;

    /// <summary>
    /// Gets the address the player opens.
    /// </summary>
    public Uri Address
        => new($"udp://127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>
    /// Gets a UDP port nobody uses now.
    /// </summary>
    /// <returns>The port.</returns>
    public static int FreePort()
    {
        using var probe = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    /// <summary>
    /// Starts sending a video.
    /// </summary>
    /// <param name="tool">The FFmpeg command-line tool.</param>
    /// <param name="video">The video.</param>
    /// <param name="port">The UDP port on this machine.</param>
    /// <param name="logPath">Where the tool's own output goes.</param>
    /// <returns>The sender.</returns>
    public static LiveSender Start(
        string tool,
        string video,
        int port,
        string logPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(video);
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);

        var start = new System.Diagnostics.ProcessStartInfo(tool)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        // -copyts with no mux delay keeps the video's times, which start at 0, on the wire.
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "warning", "-re", "-copyts", "-i", video, "-map", "0:v:0", "-c", "copy",
            "-muxdelay", "0", "-muxpreload", "0", "-f", "mpegts",
            $"udp://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}?pkt_size=1316",
        })
        {
            start.ArgumentList.Add(argument);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var writer = new StreamWriter(logPath, append: true) { AutoFlush = true };
        var writing = new Lock();
        System.Diagnostics.Process started;
        try
        {
            started = System.Diagnostics.Process.Start(start)
                      ?? throw new InvalidOperationException("The FFmpeg tool did not start.");
        }
        catch
        {
            writer.Dispose();
            throw;
        }

        started.ErrorDataReceived += (_, e) => Write(e.Data);
        started.OutputDataReceived += (_, e) => Write(e.Data);
        started.BeginErrorReadLine();
        started.BeginOutputReadLine();
        return new LiveSender(started, writer, writing, port);

        void Write(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (writing)
            {
                try
                {
                    writer.WriteLine(line);
                }
                catch (ObjectDisposedException)
                {
                    // Written after the sender was disposed.
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Exiting while being killed.
        }

        process.Dispose();
        lock (gate)
        {
            log.Dispose();
        }
    }
}