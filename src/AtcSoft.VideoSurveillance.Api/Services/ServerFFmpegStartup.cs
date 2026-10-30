namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Makes sure the FFmpeg the engine needs is there before the server starts the engine. The
/// server has no one to show a dialog to: without it, it says what is needed and how to get it,
/// and stops; started with <see cref="InstallArgument"/> it installs it first.
/// </summary>
public static partial class ServerFFmpegStartup
{
    /// <summary>
    /// The command-line argument that installs FFmpeg when it is not found.
    /// </summary>
    public const string InstallArgument = "--install-ffmpeg";

    /// <summary>
    /// Takes <see cref="InstallArgument"/> out of the command line, which the host would otherwise
    /// read as a configuration key.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <param name="rest">The command line without it.</param>
    /// <returns><see langword="true"/> when it was there.</returns>
    public static bool TakeInstallArgument(
        string[] args,
        out string[] rest)
    {
        ArgumentNullException.ThrowIfNull(args);

        rest = args.Where(a => !string.Equals(a, InstallArgument, StringComparison.OrdinalIgnoreCase)).ToArray();
        return rest.Length != args.Length;
    }

    /// <summary>
    /// Returns the folder with the FFmpeg the engine needs, installing it when asked to.
    /// </summary>
    /// <param name="check">What the look for FFmpeg found.</param>
    /// <param name="installRequested">Whether the server was started with <see cref="InstallArgument"/>.</param>
    /// <param name="install">Installs FFmpeg and returns its folder (<see cref="FFmpegInstaller"/>).</param>
    /// <param name="logger">The start-up log.</param>
    /// <param name="cancellationToken">Stops the install.</param>
    /// <returns>The folder, or <see langword="null"/> when the server cannot start.</returns>
    public static async Task<string?> EnsureReadyAsync(
        FFmpegCheck check,
        bool installRequested,
        Func<IProgress<FFmpegInstallProgress>, CancellationToken, Task<string>> install,
        Microsoft.Extensions.Logging.ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(logger);

        if (check.Status == FFmpegStatus.Ready)
        {
            return check.Folder;
        }

        if (!installRequested)
        {
            LogFFmpegNotReady(logger, check.Describe(), InstallArgument, FFmpegRuntime.PathEnvironmentVariable);
            return null;
        }

        LogFFmpegInstalling(logger, FFmpegRuntime.ReleaseVersion, FFmpegInstaller.Package.DownloadUri, FFmpegRuntime.ManagedFolder);
        try
        {
            var lastTenth = -1;
            var progress = new Progress<FFmpegInstallProgress>(p =>
            {
                var tenth = (int)((p.Fraction ?? 0) * 10);
                if (p.Stage == FFmpegInstallStage.Downloading && tenth != lastTenth)
                {
                    lastTenth = tenth;
                    LogFFmpegDownloadProgress(logger, tenth * 10);
                }
            });

            var folder = await install(progress, cancellationToken).ConfigureAwait(false);
            LogFFmpegInstalled(logger, FFmpegRuntime.ReleaseVersion, folder);
            return folder;
        }
        catch (FFmpegInstallException ex)
        {
            LogFFmpegInstallFailed(logger, ex.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "{Description}. Start the server once with {InstallArgument} to install it, or name an FFmpeg folder in {PathVariable}")]
    private static partial void LogFFmpegNotReady(Microsoft.Extensions.Logging.ILogger logger, string description, string installArgument, string pathVariable);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing FFmpeg {Version} from {DownloadUri} into '{Folder}'")]
    private static partial void LogFFmpegInstalling(Microsoft.Extensions.Logging.ILogger logger, string version, Uri downloadUri, string folder);

    [LoggerMessage(Level = LogLevel.Information, Message = "FFmpeg download {Percent}%")]
    private static partial void LogFFmpegDownloadProgress(Microsoft.Extensions.Logging.ILogger logger, int percent);

    [LoggerMessage(Level = LogLevel.Information, Message = "FFmpeg {Version} installed in '{Folder}'")]
    private static partial void LogFFmpegInstalled(Microsoft.Extensions.Logging.ILogger logger, string version, string folder);

    [LoggerMessage(Level = LogLevel.Critical, Message = "FFmpeg could not be installed, the server cannot start: {Reason}")]
    private static partial void LogFFmpegInstallFailed(Microsoft.Extensions.Logging.ILogger logger, string reason);
}