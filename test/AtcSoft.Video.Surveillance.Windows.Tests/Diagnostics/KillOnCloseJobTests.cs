namespace AtcSoft.Video.Surveillance.Windows.Diagnostics;

/// <summary>
/// A helper process in the job ends with the job, so it cannot outlive the server that started it.
/// </summary>
public sealed class KillOnCloseJobTests
{
    [Fact]
    public void A_Process_In_The_Job_Ends_When_The_Job_Is_Closed()
    {
        var job = KillOnCloseJob.TryCreate(out var createError);
        job.Should().NotBeNull(createError);

        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ping", "-n 30 127.0.0.1")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;

        try
        {
            job!.TryAdd(process, out var addError).Should().BeTrue(addError);
            process.HasExited.Should().BeFalse();

            job.Dispose();

            process.WaitForExit(5000).Should().BeTrue("closing the job ends what is in it");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
    }

    [Fact]
    public void A_Process_That_Has_Ended_Is_Not_Added()
    {
        using var job = KillOnCloseJob.TryCreate(out _)!;
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd", "/c exit 0")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        process.WaitForExit();

        job.TryAdd(process, out var error).Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }
}