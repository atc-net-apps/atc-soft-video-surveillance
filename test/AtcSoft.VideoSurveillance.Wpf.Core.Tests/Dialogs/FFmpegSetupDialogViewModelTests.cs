namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// The set-up dialog is shown at start when the FFmpeg the engine needs is not found. It says
/// what is needed and what was found, installs it, or takes a folder the user names, and only
/// closes as done when the folder really holds that FFmpeg.
/// </summary>
public sealed class FFmpegSetupDialogViewModelTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-ffsetup-" + Guid.NewGuid().ToString("N"));

    public FFmpegSetupDialogViewModelTests()
    {
        System.IO.Directory.CreateDirectory(directory);
    }

    public void Dispose()
        => TestFolders.Delete(directory);

    [Fact]
    public void A_Missing_FFmpeg_Is_Said_With_The_Version_Needed()
    {
        var viewModel = new FFmpegSetupDialogViewModel(Check([]), NoInstall);

        viewModel.Message.Should().Contain($"FFmpeg {FFmpegRuntime.ReleaseVersion}").And.Contain("not installed");
        viewModel.FoundFolders.Should().BeEmpty();
        viewModel.InstallHint.Should().Contain(FFmpegRuntime.ManagedFolder);
    }

    [Fact]
    public void Another_FFmpeg_Is_Listed_With_Its_Version_And_Folder()
    {
        var older = Install("older", FFmpegRuntime.Expected with { Avutil = FFmpegRuntime.Expected.Avutil - 1 });

        var viewModel = new FFmpegSetupDialogViewModel(Check([older]), NoInstall);

        viewModel.Message.Should().Contain("another version");
        viewModel.FoundFolders.Should().ContainSingle().Which.Should().Contain(older).And.Contain($"FFmpeg {FFmpegRuntime.Expected.FFmpegMajor - 1}");
    }

    [Fact]
    public async Task An_Install_Closes_The_Dialog_With_The_Installed_Folder()
    {
        var ready = Install("ready", FFmpegRuntime.Expected);
        var viewModel = new FFmpegSetupDialogViewModel(Check([]), (progress, _) =>
        {
            progress.Report(new FFmpegInstallProgress(FFmpegInstallStage.Downloading, 50, 100));
            return Task.FromResult(ready);
        });
        bool? closedWith = null;
        viewModel.CloseRequested += (_, e) => closedWith = e.DialogResult;

        await viewModel.InstallAsync();

        closedWith.Should().BeTrue();
        viewModel.ReadyFolder.Should().Be(ready);
        viewModel.IsInstalling.Should().BeFalse();
    }

    [Fact]
    public async Task A_Failed_Install_Says_Why_And_Keeps_The_Dialog_Open()
    {
        var viewModel = new FFmpegSetupDialogViewModel(Check([]), (_, _) => throw new FFmpegInstallException("The download does not match its checksum"));
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        await viewModel.InstallAsync();

        closed.Should().BeFalse();
        viewModel.HasError.Should().BeTrue();
        viewModel.StatusText.Should().Contain("checksum");
        viewModel.IsInstalling.Should().BeFalse();
        viewModel.ReadyFolder.Should().BeNull();
    }

    [Fact]
    public async Task A_Cancelled_Install_Keeps_The_Dialog_Open_Without_An_Error()
    {
        var viewModel = new FFmpegSetupDialogViewModel(Check([]), (_, cancellationToken) => Task.FromCanceled<string>(new CancellationToken(canceled: true)));

        await viewModel.InstallAsync();

        viewModel.HasError.Should().BeFalse();
        viewModel.IsInstalling.Should().BeFalse();
        viewModel.ReadyFolder.Should().BeNull();
    }

    [Fact]
    public void A_Folder_With_The_Right_FFmpeg_Is_Taken()
    {
        var ready = Install("ready", FFmpegRuntime.Expected);
        var viewModel = new FFmpegSetupDialogViewModel(Check([]), NoInstall) { Folder = ready };
        bool? closedWith = null;
        viewModel.CloseRequested += (_, e) => closedWith = e.DialogResult;

        viewModel.UseFolder();

        closedWith.Should().BeTrue();
        viewModel.ReadyFolder.Should().Be(ready);
    }

    [Fact]
    public void A_Folder_With_Another_FFmpeg_Is_Refused()
    {
        var older = Install("older", FFmpegRuntime.Expected with { Avutil = FFmpegRuntime.Expected.Avutil - 1 });
        var viewModel = new FFmpegSetupDialogViewModel(Check([older]), NoInstall) { Folder = older };
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        viewModel.UseFolder();

        closed.Should().BeFalse();
        viewModel.HasError.Should().BeTrue();
        viewModel.StatusText.Should().Contain($"FFmpeg {FFmpegRuntime.ReleaseVersion}");
        viewModel.ReadyFolder.Should().BeNull();
    }

    [Fact]
    public void The_Download_Progress_Is_Shown()
    {
        var viewModel = new FFmpegSetupDialogViewModel(Check([]), NoInstall);

        viewModel.Report(new FFmpegInstallProgress(FFmpegInstallStage.Downloading, 25 * 1024 * 1024, 100 * 1024 * 1024));

        viewModel.InstallProgress.Should().Be(25);
        viewModel.StatusText.Should().Contain("25").And.Contain("100");
    }

    [Fact]
    public void The_Dialog_Can_Be_Created()
    {
        var error = WpfTestThread.Run(() =>
        {
            var dialog = new FFmpegSetupDialog(new FFmpegSetupDialogViewModel(Check([]), NoInstall));
            dialog.Close();
        });

        error.Should().BeNull();
    }

    private static Task<string> NoInstall(
        IProgress<FFmpegInstallProgress> progress,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException("Not installed in this test.");

    private static FFmpegCheck Check(IEnumerable<string> folders)
        => FFmpegRuntime.Check(folders);

    private string Install(
        string name,
        FFmpegLibraryVersions versions)
    {
        var folder = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(directory, name)).FullName;
        foreach (var (library, major) in versions.Libraries)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, $"{library}-{major}.dll"), string.Empty);
        }

        return folder;
    }
}