namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// Makes sure the FFmpeg the engine needs is there before the engine starts: when it is not
/// found, the set-up dialog installs it or takes a folder the user names. Call it before
/// <see cref="VideoEngineBootstrap.Initialize(VideoEngineConfig)"/>; nothing native is loaded yet,
/// so no restart is needed.
/// </summary>
public static class FFmpegSetup
{
    // Shared, not disposed with the dialog: a download the user cancelled may still be ending.
    private static readonly Lazy<HttpClient> Http = new(CreateClient);

    /// <summary>
    /// Returns the folder with the FFmpeg the engine needs, showing the set-up dialog when it is
    /// not found.
    /// </summary>
    /// <param name="configuredPath">A folder the app was configured with, looked in first.</param>
    /// <returns>The folder, or <see langword="null"/> when the user chose to exit.</returns>
    public static string? EnsureReady(string? configuredPath = null)
    {
        var check = FFmpegRuntime.Check(configuredPath);
        return check.Status == FFmpegStatus.Ready
            ? check.Folder
            : ShowDialog(check);
    }

    /// <summary>
    /// Shows the set-up dialog for what a look for FFmpeg found.
    /// </summary>
    /// <param name="check">What the look for FFmpeg found.</param>
    /// <returns>The folder, or <see langword="null"/> when the user chose to exit.</returns>
    public static string? ShowDialog(FFmpegCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        var viewModel = new FFmpegSetupDialogViewModel(
            check,
            (progress, cancellationToken) => FFmpegInstaller.InstallAsync(Http.Value, progress, cancellationToken));
        var dialog = new FFmpegSetupDialog(viewModel);
        if (dialog.ShowDialog() != true || viewModel.ReadyFolder is not { } folder)
        {
            return null;
        }

        // A folder the user chose is remembered for every app and the server; the installed
        // one is found without that. A limited search (the end-to-end tests) leaves no note.
        if (!FFmpegRuntime.IsSearchLimited() && FFmpegRuntime.Check().Folder is null)
        {
            FFmpegRuntime.RememberFolder(folder);
        }

        return folder;
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AtcSoft-VideoSurveillance");
        return http;
    }
}