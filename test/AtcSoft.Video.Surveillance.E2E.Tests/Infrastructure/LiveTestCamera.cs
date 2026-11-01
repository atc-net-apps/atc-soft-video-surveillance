namespace AtcSoft.Video.Surveillance.E2E.Infrastructure;

/// <summary>
/// A test pattern served over HTTP the way an HTTP camera does. FFmpeg serves one client and
/// ends when it goes, so this starts FFmpeg again on the same port each time: the camera
/// survives a reconnect, an edit and a second viewer after the first.
/// </summary>
internal sealed partial class LiveTestCamera : IDisposable
{
    private readonly string ffmpeg;
    private readonly int port;
    private readonly string outputPath;
    private readonly CancellationTokenSource stop = new();
    private readonly Task loop;
    private readonly Lock gate = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The loop disposes each process when it ends; this only points at the running one.")]
    private Process? current;
    private DateTime backAtUtc = DateTime.MinValue;

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

    /// <summary>
    /// Takes the camera off the network for a while, as a camera that reboots or loses its link:
    /// the stream ends at once and the camera answers nothing until the time is up.
    /// </summary>
    /// <param name="downFor">How long the camera is gone.</param>
    /// <summary>
    /// Makes the camera hang: it keeps the connection open but sends nothing for a while, as a camera
    /// that freezes or a network path that dies without closing. Then it goes on.
    /// </summary>
    /// <param name="hangFor">How long it sends nothing.</param>
    public void Hang(TimeSpan hangFor)
    {
        nint handle;
        lock (gate)
        {
            if (current is null || current.HasExited)
            {
                return;
            }

            handle = current.Handle;
            _ = NtSuspendProcess(handle);
        }

        _ = Task.Delay(hangFor, CancellationToken.None).ContinueWith(
            _ => Resume(handle),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    public void DropOut(TimeSpan downFor)
    {
        lock (gate)
        {
            backAtUtc = DateTime.UtcNow + downFor;
            Kill(current);
        }
    }

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

    private void Resume(nint handle)
    {
        lock (gate)
        {
            _ = NtResumeProcess(handle);
        }
    }

    [System.Runtime.InteropServices.LibraryImport("ntdll.dll")]
    private static partial int NtSuspendProcess(nint process);

    [System.Runtime.InteropServices.LibraryImport("ntdll.dll")]
    private static partial int NtResumeProcess(nint process);

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

            // The client went away; listen again for the next one, or once the camera is back.
            var away = backAtUtc - DateTime.UtcNow;
            await Task.Delay(away > TimeSpan.FromMilliseconds(200) ? away : TimeSpan.FromMilliseconds(200), CancellationToken.None);
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