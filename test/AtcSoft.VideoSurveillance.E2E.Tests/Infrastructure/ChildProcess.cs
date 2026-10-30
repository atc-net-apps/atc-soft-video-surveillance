namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// A process the smoke tests start: its output goes to a file in the run folder, and it is
/// killed with everything it started when the test is done.
/// </summary>
internal sealed class ChildProcess : IDisposable
{
    private readonly StreamWriter output;
    private readonly Lock outputLock = new();

    private ChildProcess(
        Process process,
        string outputPath)
    {
        Process = process;
        OutputPath = outputPath;
        output = new StreamWriter(outputPath, append: false, Encoding.UTF8) { AutoFlush = true };
    }

    public Process Process { get; }

    /// <summary>
    /// Gets the file with the process's standard output and error.
    /// </summary>
    public string OutputPath { get; }

    public bool HasExited
        => Process.HasExited;

    public static ChildProcess Start(
        string fileName,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        string outputPath,
        string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(fileName)!,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Never inherit an Aspire or developer setting that points the app elsewhere.
        foreach (var name in new[] { "OTEL_EXPORTER_OTLP_ENDPOINT", "services__api__http__0", "services__api__https__0", "Storage__DataPath", "ATCSOFT_DATA_PATH" })
        {
            startInfo.Environment.Remove(name);
        }

        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        var process = new Process { StartInfo = startInfo };
        var child = new ChildProcess(process, outputPath);
        process.OutputDataReceived += (_, e) => child.Write(e.Data);
        process.ErrorDataReceived += (_, e) => child.Write(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return child;
    }

    public string ReadOutput()
    {
        lock (outputLock)
        {
            using var stream = new FileStream(OutputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    public void Dispose()
    {
        try
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                Process.WaitForExit(10_000);
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

        Process.Dispose();
        lock (outputLock)
        {
            output.Dispose();
        }
    }

    private void Write(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (outputLock)
        {
            try
            {
                output.WriteLine(line);
            }
            catch (ObjectDisposedException)
            {
                // Output that arrives after the test is done.
            }
        }
    }
}