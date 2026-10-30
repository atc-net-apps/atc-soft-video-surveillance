namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using IWpfCoreSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService;

/// <summary>
/// A file that is still being recorded has no finished index: played, it
/// shows a black picture. The browser lists it, says that it is being
/// recorded, and does not offer to play or delete it until it is finished.
/// </summary>
public sealed class RecordingsBrowserDialogViewModelOngoingTests : IDisposable
{
    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-browser-ongoing-" + Guid.NewGuid().ToString("N"));
    private readonly string ongoing;
    private readonly string finished;
    private System.IO.FileStream? writer;

    public RecordingsBrowserDialogViewModelOngoingTests()
    {
        var folder = System.IO.Path.Combine(root, "Front Door");
        System.IO.Directory.CreateDirectory(folder);
        finished = System.IO.Path.Combine(folder, "Front Door_20261012_080000_000.mkv");
        ongoing = System.IO.Path.Combine(folder, "Front Door_20261013_090000_000.mkv");
        System.IO.File.WriteAllText(finished, "x");

        // The recorder has the file open for writing; readers are let in.
        writer = new System.IO.FileStream(ongoing, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read);
        writer.WriteByte(1);
        writer.Flush();
    }

    public void Dispose()
    {
        writer?.Dispose();
        if (System.IO.Directory.Exists(root))
        {
            TestFolders.Delete(root);
        }
    }

    [Fact]
    public void A_File_That_Is_Still_Being_Recorded_Is_Listed_And_Says_So()
    {
        using var sut = CreateSut();

        var entry = sut.Recordings.Single(r => r.FilePath == ongoing);
        entry.IsBeingRecorded.Should().BeTrue();
        entry.FormattedDuration.Should().Be(AtcSoft.VideoSurveillance.Resources.Translations.RecordingInProgress, "it has no length to show yet");
        sut.Recordings.Single(r => r.FilePath == finished).IsBeingRecorded.Should().BeFalse();
    }

    [Fact]
    public void It_Cannot_Be_Played()
    {
        using var sut = CreateSut();
        var played = new List<AtcSoft.VideoSurveillance.Wpf.Core.Models.RecordingEntry>();
        sut.PlayRecordingRequested += (_, entry) => played.Add(entry);
        sut.SelectedRecording = sut.Recordings.Single(r => r.FilePath == ongoing);

        sut.CanPlay.Should().BeFalse();
        sut.PlayCommand.CanExecute(parameter: null).Should().BeFalse();
        sut.PlayCommand.Execute(parameter: null);

        played.Should().BeEmpty();
    }

    [Fact]
    public void It_Cannot_Be_Deleted()
    {
        using var sut = CreateSut();
        sut.SelectedRecording = sut.Recordings.Single(r => r.FilePath == ongoing);

        sut.CanDelete.Should().BeFalse();
        sut.DeleteCommand.CanExecute(parameter: null).Should().BeFalse();
        sut.DeleteCommand.Execute(parameter: null);

        System.IO.File.Exists(ongoing).Should().BeTrue();
        sut.Recordings.Should().Contain(r => r.FilePath == ongoing);
    }

    [Fact]
    public void A_Finished_Recording_Next_To_It_Can_Be_Played_And_Deleted()
    {
        using var sut = CreateSut();
        sut.SelectedRecording = sut.Recordings.Single(r => r.FilePath == finished);

        sut.CanPlay.Should().BeTrue();
        sut.CanDelete.Should().BeTrue();
    }

    [Fact]
    public void Once_The_Recording_Has_Ended_A_Refresh_Makes_It_An_Ordinary_Recording()
    {
        using var sut = CreateSut();

        writer!.Dispose();
        writer = null;
        sut.RefreshCommand.Execute(parameter: null);

        var entry = sut.Recordings.Single(r => r.FilePath == ongoing);
        entry.IsBeingRecorded.Should().BeFalse();
        sut.SelectedRecording = entry;
        sut.CanPlay.Should().BeTrue();
        sut.CanDelete.Should().BeTrue();
    }

    [Fact]
    public void A_Recording_That_Started_After_The_List_Was_Loaded_Is_Not_Played_Either()
    {
        // The list is as old as its last refresh; the file is asked again
        // when it is about to be played.
        writer!.Dispose();
        writer = null;
        using var sut = CreateSut();
        var entry = sut.Recordings.Single(r => r.FilePath == ongoing);
        entry.IsBeingRecorded.Should().BeFalse();

        writer = new System.IO.FileStream(ongoing, System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.Read);
        sut.SelectedRecording = entry;

        sut.CanPlay.Should().BeFalse();
        sut.CanDelete.Should().BeFalse();
    }

    [Fact]
    public void When_The_Recording_Ends_Its_Row_Becomes_An_Ordinary_Recording_By_Itself()
    {
        // The dialog asks every few seconds; nobody has to press Refresh.
        using var sut = CreateSut();
        var entry = sut.Recordings.Single(r => r.FilePath == ongoing);
        var other = sut.Recordings.Single(r => r.FilePath == finished);
        sut.SelectedRecording = other;
        var changedOnEntry = new List<string?>();
        entry.PropertyChanged += (_, e) => changedOnEntry.Add(e.PropertyName);
        var listRebuilt = false;
        sut.PropertyChanged += (_, e) => listRebuilt |= e.PropertyName == nameof(RecordingsBrowserDialogViewModel.FilteredRecordings);

        writer!.Write(new byte[2048]);
        writer.Dispose();
        writer = null;
        sut.UpdateOngoingRecordings().Should().BeTrue();

        sut.Recordings.Single(r => r.FilePath == ongoing).Should().BeSameAs(entry, "the row stays where it is");
        entry.IsBeingRecorded.Should().BeFalse();
        entry.FileSizeBytes.Should().Be(2049, "the size is that of the finished file");
        entry.FormattedDuration.Should().NotBe(AtcSoft.VideoSurveillance.Resources.Translations.RecordingInProgress);
        changedOnEntry.Should().Contain([nameof(entry.IsBeingRecorded), nameof(entry.FormattedDuration), nameof(entry.FormattedFileSize)]);
        listRebuilt.Should().BeFalse("rebuilding the list would lose the scroll position");
        sut.SelectedRecording.Should().BeSameAs(other);

        sut.SelectedRecording = entry;
        sut.CanPlay.Should().BeTrue();
    }

    [Fact]
    public void While_Nothing_Changes_The_List_Is_Left_Alone()
    {
        using var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.UpdateOngoingRecordings().Should().BeFalse();

        changed.Should().BeEmpty();
        sut.Recordings.Single(r => r.FilePath == ongoing).IsBeingRecorded.Should().BeTrue();
    }

    [Fact]
    public void A_Recording_That_Starts_While_The_Browser_Is_Open_Appears_In_The_List()
    {
        using var sut = CreateSut();
        var selected = sut.Recordings.Single(r => r.FilePath == finished);
        sut.SelectedRecording = selected;
        var started = System.IO.Path.Combine(root, "Garden", "2026-10-13", "Garden_20261013_100000_000.mkv");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(started)!);

        using (new System.IO.FileStream(started, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read))
        {
            sut.UpdateOngoingRecordings().Should().BeTrue();
        }

        var entry = sut.Recordings.Single(r => r.FilePath == started);
        entry.IsBeingRecorded.Should().BeTrue();
        entry.CameraName.Should().Be("Garden");
        sut.Recordings.Should().BeInDescendingOrder(r => r.RecordingTime);
        sut.CameraFilters.Select(f => f.Key).Should().Contain("Garden");
        sut.SelectedRecording.Should().BeSameAs(selected, "the selection is kept");
    }

    private RecordingsBrowserDialogViewModel CreateSut()
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });
        return new RecordingsBrowserDialogViewModel(settingsService);
    }
}