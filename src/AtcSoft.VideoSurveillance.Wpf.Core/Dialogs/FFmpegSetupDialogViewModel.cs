namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// View model for the FFmpeg set-up dialog, shown at start when the FFmpeg the engine needs is
/// not found. It says what is needed and what was found, installs it, or takes a folder the user
/// names, and closes as done only when the folder really holds that FFmpeg.
/// </summary>
public sealed partial class FFmpegSetupDialogViewModel : ViewModelDialogBase
{
    private const double BytesPerMegabyte = 1024 * 1024;
    private static readonly Uri DownloadPage = new("https://www.gyan.dev/ffmpeg/builds/");

    private readonly Func<IProgress<FFmpegInstallProgress>, CancellationToken, Task<string>> install;
    private CancellationTokenSource? installCancellation;

    [ObservableProperty(AfterChangedCallback = nameof(OnStateChanged), DependentPropertyNames = [nameof(FolderDirectory)])]
    private string folder = string.Empty;

    [ObservableProperty(AfterChangedCallback = nameof(OnStateChanged))]
    private bool isInstalling;

    [ObservableProperty]
    private double installProgress;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private bool hasError;

    /// <summary>
    /// Initializes a new instance of the <see cref="FFmpegSetupDialogViewModel"/> class.
    /// </summary>
    /// <param name="check">What the look for FFmpeg found.</param>
    /// <param name="install">Installs FFmpeg and returns its folder (<see cref="FFmpegInstaller"/>).</param>
    public FFmpegSetupDialogViewModel(
        FFmpegCheck check,
        Func<IProgress<FFmpegInstallProgress>, CancellationToken, Task<string>> install)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(install);

        this.install = install;
        Check = check;

        Message = string.Format(
            CultureInfo.CurrentCulture,
            check.Status == FFmpegStatus.WrongVersion ? Translations.FFmpegOtherVersion1 : Translations.FFmpegNotInstalled1,
            FFmpegRuntime.ReleaseVersion);
        FoundFolders = check.Found
            .Select(f => string.Format(CultureInfo.CurrentCulture, Translations.FFmpegFoundIn2, f.Versions.FFmpegMajor, f.Folder))
            .ToList();
        InstallHint = string.Format(CultureInfo.CurrentCulture, Translations.FFmpegInstallHint2, FFmpegRuntime.ReleaseVersion, FFmpegRuntime.ManagedFolder);
        ChooseFolderText = string.Format(CultureInfo.CurrentCulture, Translations.FFmpegChooseFolder1, FFmpegRuntime.ReleaseVersion);
    }

    /// <summary>
    /// Occurs when the dialog requests to be closed: <see langword="true"/> with <see cref="ReadyFolder"/> set, or <see langword="false"/> to exit.
    /// </summary>
    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    /// <summary>
    /// Gets what the look for FFmpeg found.
    /// </summary>
    public FFmpegCheck Check { get; }

    /// <summary>
    /// Gets what is needed and why the dialog is shown.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets each FFmpeg found, with its version and folder.
    /// </summary>
    public IReadOnlyList<string> FoundFolders { get; }

    /// <summary>
    /// Gets a value indicating whether any FFmpeg was found.
    /// </summary>
    public bool HasFoundFolders
        => FoundFolders.Count > 0;

    /// <summary>
    /// Gets what the install does and where it puts FFmpeg.
    /// </summary>
    public string InstallHint { get; }

    /// <summary>
    /// Gets the text above the folder picker.
    /// </summary>
    public string ChooseFolderText { get; }

    /// <summary>
    /// Gets or sets <see cref="Folder"/> as the directory picker shows it.
    /// </summary>
    public DirectoryInfo? FolderDirectory
    {
        get => string.IsNullOrWhiteSpace(Folder) ? null : new DirectoryInfo(Folder);
        set => Folder = value?.FullName ?? string.Empty;
    }

    /// <summary>
    /// Gets the folder with the FFmpeg the engine needs, once installed or chosen.
    /// </summary>
    public string? ReadyFolder { get; private set; }

    /// <summary>
    /// Downloads and installs FFmpeg, then closes the dialog; says why when that fails.
    /// </summary>
    /// <returns>A task that completes when the install has ended.</returns>
    [RelayCommand("Install", CanExecute = nameof(CanChange))]
    public async Task InstallAsync()
    {
        if (IsInstalling)
        {
            return;
        }

        IsInstalling = true;
        HasError = false;
        InstallProgress = 0;
        using var cancellation = new CancellationTokenSource();
        installCancellation = cancellation;
        try
        {
            var installed = await install(new Progress<FFmpegInstallProgress>(Report), cancellation.Token).ConfigureAwait(true);
            Accept(installed);
        }
        catch (OperationCanceledException)
        {
            StatusText = Translations.FFmpegInstallCancelled;
        }
        catch (FFmpegInstallException ex)
        {
            ShowError(string.Format(CultureInfo.CurrentCulture, Translations.FFmpegInstallFailed1, ex.Message));
        }
        finally
        {
            installCancellation = null;
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Takes the chosen folder when it holds the FFmpeg the engine needs, and closes the dialog.
    /// </summary>
    [RelayCommand("UseFolder", CanExecute = nameof(CanUseFolder))]
    public void UseFolder()
    {
        HasError = false;
        Accept(Folder.Trim());
    }

    /// <summary>
    /// Shows how far the install has come.
    /// </summary>
    /// <param name="progress">How far the install has come.</param>
    public void Report(FFmpegInstallProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        switch (progress.Stage)
        {
            case FFmpegInstallStage.Downloading:
                InstallProgress = Math.Round((progress.Fraction ?? 0) * 100);
                StatusText = string.Format(
                    CultureInfo.CurrentCulture,
                    Translations.FFmpegDownloading2,
                    Math.Round(progress.BytesDone / BytesPerMegabyte),
                    Math.Round((progress.BytesTotal ?? 0) / BytesPerMegabyte));
                break;
            case FFmpegInstallStage.Verifying:
                InstallProgress = 100;
                StatusText = Translations.FFmpegVerifying;
                break;
            case FFmpegInstallStage.Extracting:
                InstallProgress = 100;
                StatusText = Translations.FFmpegExtracting;
                break;
        }
    }

    [RelayCommand("CancelInstall")]
    private void CancelInstall()
        => installCancellation?.Cancel();

    [RelayCommand("OpenDownloadPage")]
    private static void OpenDownloadPage()
        => Process.Start(new ProcessStartInfo(DownloadPage.AbsoluteUri) { UseShellExecute = true })?.Dispose();

    [RelayCommand("Exit", CanExecute = nameof(CanChange))]
    private void Exit()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: false));

    private static void OnStateChanged()
        => CommandManager.InvalidateRequerySuggested();

    private bool CanChange()
        => !IsInstalling;

    private bool CanUseFolder()
        => !IsInstalling && !string.IsNullOrWhiteSpace(Folder);

    private void Accept(string candidate)
    {
        if (FFmpegRuntime.Check([candidate]).Status != FFmpegStatus.Ready)
        {
            ShowError(string.Format(CultureInfo.CurrentCulture, Translations.FFmpegFolderNotReady1, FFmpegRuntime.ReleaseVersion));
            return;
        }

        ReadyFolder = candidate;
        CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));
    }

    private void ShowError(string text)
    {
        HasError = true;
        StatusText = text;
    }
}