namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

using IWpfCoreSettingsService = AtcSoft.Video.Surveillance.Wpf.Core.Services.IApplicationSettingsService;
using RecordingFolderHistory = AtcSoft.Video.Surveillance.Wpf.Core.Models.RecordingFolderHistory;
using RecordingSourceChoice = AtcSoft.Video.Surveillance.Wpf.Core.Models.RecordingSourceChoice;

/// <summary>
/// The browser lists another CameraWall's recordings (its shared data folder or its recordings
/// folder) without changing the recording folder setting, and leaves that folder as it is.
/// </summary>
public sealed class RecordingsBrowserDialogViewModelOtherFolderTests : IDisposable
{
    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-other-" + Guid.NewGuid().ToString("N"));

    private string Own
        => System.IO.Path.Combine(root, "own");

    private string Other
        => System.IO.Path.Combine(root, "other");

    public void Dispose()
    {
        if (System.IO.Directory.Exists(root))
        {
            TestFolders.Delete(root);
        }
    }

    [Fact]
    public async Task Another_CameraWalls_Recordings_Are_Listed_Read_Only_And_Remembered()
    {
        Touch(Own, "Front Door", "2026-10-12", "Front Door_20261012_101500_000.mkv");
        System.IO.Directory.CreateDirectory(Other);
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(Other, "cameras.json"), """{ "cameras": [] }""", TestContext.Current.CancellationToken);
        var video = Touch(Other, "recordings", "TAPO-C120-04", "2026-11-07", "TAPO-C120-04_20261107_165700_000.mkv");
        var otherRecordings = System.IO.Path.Combine(Other, "recordings");
        var history = new RecordingFolderHistory(System.IO.Path.Combine(root, "folders.json"));
        string? picked = otherRecordings;

        using var viewModel = Create(() => picked, history);
        await viewModel.Loading;
        viewModel.FilteredRecordings.Select(r => r.CameraName).Should().Equal("Front Door");
        viewModel.CanOpenOtherFolder.Should().BeTrue();

        var busy = new List<(bool Busy, string Text)>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecordingsBrowserDialogViewModel.IsReadingOtherFolder))
            {
                busy.Add((viewModel.IsReadingOtherFolder, viewModel.ReadingOtherFolderText));
            }
        };

        await viewModel.OpenOtherFolderCommand.ExecuteAsync(null);

        // The window says it reads the folder while it does, and stops saying so.
        busy.Should().Contain((true, $"Opening {otherRecordings}..."));
        viewModel.IsReadingOtherFolder.Should().BeFalse();
        viewModel.IsExternal.Should().BeTrue();
        viewModel.FilteredRecordings.Select(r => r.CameraName).Should().Equal("TAPO-C120-04");
        viewModel.SelectedSource.Folder.Should().Be(otherRecordings);
        history.Read().Should().Equal(otherRecordings);

        // Played, but nothing is deleted, analysed, previewed or reviewed there.
        viewModel.SelectedRecording = viewModel.FilteredRecordings.Single();
        viewModel.CanPlay.Should().BeTrue();
        viewModel.CanDelete.Should().BeFalse();
        viewModel.CanAnalyse.Should().BeFalse();
        viewModel.PreviewsToMake.Should().BeNull();
        viewModel.VerdictSaverFor(viewModel.SelectedRecording).Should().BeNull();
        viewModel.DeleteCommand.Execute(null);
        System.IO.File.Exists(video).Should().BeTrue();

        // A folder that cannot be opened leaves the list as it was.
        picked = System.IO.Path.Combine(root, "missing");
        await viewModel.OpenOtherFolderCommand.ExecuteAsync(null);
        viewModel.FilteredRecordings.Select(r => r.CameraName).Should().Equal("TAPO-C120-04");
        viewModel.SelectedSource.Folder.Should().Be(otherRecordings);
        viewModel.StatusMessage.Should().Contain("missing");

        // Back to this computer.
        viewModel.SelectedSource = viewModel.SourceChoices[0];
        await viewModel.Loading;
        viewModel.IsExternal.Should().BeFalse();
        viewModel.FilteredRecordings.Select(r => r.CameraName).Should().Equal("Front Door");
    }

    [Fact]
    public async Task Without_A_Folder_Picker_Only_This_Computer_Is_Offered()
    {
        using var viewModel = Create(pickFolder: null, new RecordingFolderHistory(System.IO.Path.Combine(root, "folders.json")));
        await viewModel.Loading;

        viewModel.CanOpenOtherFolder.Should().BeFalse();
        viewModel.SourceChoices.Should().Equal(RecordingSourceChoice.ThisComputer);
    }

    private RecordingsBrowserDialogViewModel Create(
        Func<string?>? pickFolder,
        RecordingFolderHistory history)
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = Own });

        return new RecordingsBrowserDialogViewModel(settingsService, pickFolder: pickFolder, folderHistory: history);
    }

    private static string Touch(params string[] segments)
    {
        var path = System.IO.Path.Combine(segments);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "x");
        return path;
    }
}