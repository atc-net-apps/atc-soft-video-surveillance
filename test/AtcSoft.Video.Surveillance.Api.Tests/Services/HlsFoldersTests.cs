namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// A server that was killed leaves its live-stream folder, and maybe an FFmpeg that still
/// holds the camera; the next start clears what is left, and only that.
/// </summary>
public sealed class HlsFoldersTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-hls-" + Guid.NewGuid().ToString("N"));

    public HlsFoldersTests()
        => Directory.CreateDirectory(root);

    public void Dispose()
        => Directory.Delete(root, recursive: true);

    [Fact]
    public void The_Folder_Of_A_Running_Instance_Is_Kept()
    {
        var mine = HlsFolders.CreateInstanceFolder(root);

        HlsFolders.SweepStale(root, NullLogger.Instance);

        Directory.Exists(mine).Should().BeTrue();
    }

    [Fact]
    public void The_Folder_Of_An_Instance_That_Is_Gone_Is_Deleted()
    {
        var gone = Path.Combine(root, "instance-gone");
        Directory.CreateDirectory(Path.Combine(gone, "camera"));
        WriteMarker(Path.Combine(gone, HlsFolders.OwnerFileName), ExitedProcess());

        HlsFolders.SweepStale(root, NullLogger.Instance);

        Directory.Exists(gone).Should().BeFalse();
    }

    [Fact]
    public void A_Camera_Folder_From_An_Older_Server_Is_Deleted_And_A_Folder_That_Is_Not_Ours_Is_Kept()
    {
        var legacy = Directory.CreateDirectory(Path.Combine(root, Guid.NewGuid().ToString("N"))).FullName;
        var other = Directory.CreateDirectory(Path.Combine(root, "something-else")).FullName;

        HlsFolders.SweepStale(root, NullLogger.Instance);

        Directory.Exists(legacy).Should().BeFalse();
        Directory.Exists(other).Should().BeTrue();
    }

    [Fact]
    public void An_FFmpeg_Left_Running_By_An_Instance_That_Is_Gone_Is_Ended()
    {
        var folder = Atc.Video.Engine.VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, "ffmpeg.exe");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");

        var gone = Path.Combine(root, "instance-gone");
        var stream = Directory.CreateDirectory(Path.Combine(gone, "camera")).FullName;
        WriteMarker(Path.Combine(gone, HlsFolders.OwnerFileName), ExitedProcess());

        using var left = Process.Start(new ProcessStartInfo(tool!, "-hide_banner -loglevel error -re -f lavfi -i testsrc=rate=5 -t 60 -f null -")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        try
        {
            HlsFolders.MarkProcess(stream, left);

            HlsFolders.SweepStale(root, NullLogger.Instance);

            left.WaitForExit(5000).Should().BeTrue("the FFmpeg of a server that is gone is ended");
            Directory.Exists(gone).Should().BeFalse();
        }
        finally
        {
            if (!left.HasExited)
            {
                left.Kill();
            }
        }
    }

    private static (int Pid, DateTime StartedUtc) ExitedProcess()
    {
        using var process = Process.Start(OperatingSystem.IsWindows() ? new ProcessStartInfo("cmd", "/c exit 0") : new ProcessStartInfo("sh", "-c true")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var started = process.StartTime.ToUniversalTime();
        process.WaitForExit();
        return (process.Id, started);
    }

    private static void WriteMarker(
        string path,
        (int Pid, DateTime StartedUtc) process)
        => File.WriteAllText(path, JsonSerializer.Serialize(new { process.Pid, process.StartedUtc }));
}