namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// The server has no one to show a dialog to: without the FFmpeg the engine needs it says what is
/// needed and how to get it, and stops; started with <c>--install-ffmpeg</c> it installs it first.
/// </summary>
public sealed class ServerFFmpegStartupTests
{
    private static readonly FFmpegCheck Missing = FFmpegRuntime.Check([]);

    [Fact]
    public async Task A_Ready_FFmpeg_Is_Used_Without_An_Install()
    {
        var ready = new FFmpegCheck(FFmpegStatus.Ready, @"C:\ffmpeg\bin", FFmpegRuntime.Expected, []);
        var installs = 0;

        var folder = await ServerFFmpegStartup.EnsureReadyAsync(ready, installRequested: true, (_, _) => Task.FromResult(++installs == 1 ? "x" : "y"), new ListLogger(), TestContext.Current.CancellationToken);

        folder.Should().Be(@"C:\ffmpeg\bin");
        installs.Should().Be(0);
    }

    [Fact]
    public async Task A_Missing_FFmpeg_Stops_The_Server_With_What_Is_Needed_And_How_To_Install_It()
    {
        var logger = new ListLogger();

        var folder = await ServerFFmpegStartup.EnsureReadyAsync(Missing, installRequested: false, (_, _) => throw new InvalidOperationException("No install was asked for."), logger, TestContext.Current.CancellationToken);

        folder.Should().BeNull();
        logger.Messages.Should().ContainSingle(m => m.Level == LogLevel.Critical)
            .Which.Text.Should().Contain($"FFmpeg {FFmpegRuntime.Expected.FFmpegMajor}").And.Contain(ServerFFmpegStartup.InstallArgument);
    }

    [Fact]
    public async Task Asked_To_Install_It_Installs_FFmpeg_And_Starts()
    {
        var logger = new ListLogger();

        var folder = await ServerFFmpegStartup.EnsureReadyAsync(
            Missing,
            installRequested: true,
            (progress, _) =>
            {
                progress.Report(new FFmpegInstallProgress(FFmpegInstallStage.Downloading, 50, 100));
                return Task.FromResult(@"C:\ProgramData\AtcSoft\ffmpeg\bin");
            },
            logger,
            TestContext.Current.CancellationToken);

        folder.Should().Be(@"C:\ProgramData\AtcSoft\ffmpeg\bin");
        logger.Messages.Should().Contain(m => m.Text.Contains("installed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Failed_Install_Stops_The_Server_With_The_Reason()
    {
        var logger = new ListLogger();

        var folder = await ServerFFmpegStartup.EnsureReadyAsync(Missing, installRequested: true, (_, _) => throw new FFmpegInstallException("The download does not match its checksum"), logger, TestContext.Current.CancellationToken);

        folder.Should().BeNull();
        logger.Messages.Should().Contain(m => m.Level == LogLevel.Critical && m.Text.Contains("checksum", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Install_Argument_Is_Taken_Out_Before_The_Host_Reads_The_Command_Line()
    {
        var found = ServerFFmpegStartup.TakeInstallArgument(["--urls", "http://+:5000", "--install-ffmpeg"], out var rest);

        found.Should().BeTrue();
        rest.Should().Equal("--urls", "http://+:5000");
    }

    private sealed class ListLogger : Microsoft.Extensions.Logging.ILogger
    {
        public List<(LogLevel Level, string Text)> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add((logLevel, formatter(state, exception)));
    }
}