namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// A test pattern served over HTTP the way an HTTP camera does. FFmpeg serves one client and
/// ends when it goes, so this starts FFmpeg again on the same port each time: the camera
/// survives a reconnect, an edit and a second viewer after the first.
/// </summary>
internal sealed class LiveTestCamera : IDisposable
{
    private readonly string ffmpeg;
    private readonly int port;
    private readonly string outputPath;
    private readonly CancellationTokenSource stop = new();
    private readonly Task loop;
    private readonly Lock gate = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The loop disposes each process when it ends; this only points at the running one.")]
    private Process? current;

    public LiveTestCamera(
        string ffmpeg,
        int port,
        string outputPath)
    {
        this.ffmpeg = ffmpeg;
        this.port = port;
        this.outputPath = outputPath;
        loop = Task.Run(RunAsync, CancellationToken.None);
    }

    public int Port
        => port;

    public void Dispose()
    {
        stop.Cancel();
        lock (gate)
        {
            Kill(current);
        }

        try
        {
            loop.Wait(10_000, CancellationToken.None);
        }
        catch (AggregateException)
        {
            // The loop ends by cancellation.
        }

        stop.Dispose();
    }

    private static void Kill(Process? process)
    {
        try
        {
            if (process is not null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
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
    }

    private async Task RunAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            Process process;
            lock (gate)
            {
                if (stop.IsCancellationRequested)
                {
                    return;
                }

                process = Start();
                current = process;
            }

            try
            {
                await process.WaitForExitAsync(stop.Token);
            }
            catch (OperationCanceledException)
            {
                Kill(process);
                return;
            }
            finally
            {
                process.Dispose();
            }

            // The client went away; listen again for the next one.
            await Task.Delay(200, CancellationToken.None);
        }
    }

    private Process Start()
    {
        var startInfo = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        foreach (var argument in new[]
                 {
                     "-hide_banner", "-loglevel", "error",
                     "-re", "-f", "lavfi", "-i", "testsrc=size=640x360:rate=15",
                     "-c:v", "libx264", "-preset", "ultrafast", "-tune", "zerolatency",
                     "-g", "15", "-pix_fmt", "yuv420p",
                     "-t", "1800",
                     "-f", "mpegts", "-listen", "1", $"http://127.0.0.1:{port}/{TestCameras.StreamPath}",
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        process.ErrorDataReceived += (_, e) => Append(e.Data);
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        return process;
    }

    private void Append(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (gate)
        {
            try
            {
                File.AppendAllText(outputPath, line + Environment.NewLine);
            }
            catch (IOException)
            {
                // Diagnostics only.
            }
        }
    }
}