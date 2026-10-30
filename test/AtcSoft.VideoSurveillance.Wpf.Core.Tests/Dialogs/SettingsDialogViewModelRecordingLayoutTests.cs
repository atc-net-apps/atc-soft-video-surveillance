namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using ApplicationSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.ApplicationSettingsService;
using IRecordingLayoutSynchronizer = AtcSoft.VideoSurveillance.Wpf.Core.Services.IRecordingLayoutSynchronizer;
using RecordingLayoutReorganizeResult = AtcSoft.VideoSurveillance.Helpers.RecordingLayoutReorganizeResult;

public sealed class SettingsDialogViewModelRecordingLayoutTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-settings-vm-" + Guid.NewGuid().ToString("N"));
    private readonly ApplicationSettingsService settingsService;

    public SettingsDialogViewModelRecordingLayoutTests()
    {
        System.IO.Directory.CreateDirectory(directory);
        settingsService = new ApplicationSettingsService(System.IO.Path.Combine(directory, "settings.json"));
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(directory))
        {
            TestFolders.Delete(directory);
        }
    }

    [Fact]
    public void The_Example_Shows_A_Day_Folder_And_The_Thumbnails_Folder_When_Both_Are_On()
    {
        var sut = new SettingsDialogViewModel(settingsService)
        {
            UseDailySubfolders = true,
            SeparateThumbnailFolder = true,
        };

        sut.RecordingPathExample.Should().MatchRegex(@"^[^\\]+\\\d{4}-\d{2}-\d{2}\\[^\\]+\.\w+$");
        sut.ThumbnailPathExample.Should().MatchRegex(@"^thumbnails\\[^\\]+\\\d{4}-\d{2}-\d{2}\\[^\\]+\.png$");
    }

    [Fact]
    public void The_Example_Shows_One_Folder_Per_Camera_When_Both_Are_Off()
    {
        var sut = new SettingsDialogViewModel(settingsService)
        {
            UseDailySubfolders = false,
            SeparateThumbnailFolder = false,
        };

        sut.RecordingPathExample.Should().MatchRegex(@"^[^\\]+\\[^\\]+\.\w+$");
        sut.ThumbnailPathExample.Should().MatchRegex(@"^[^\\]+\\[^\\]+\.png$");
        sut.ThumbnailPathExample.Should().NotStartWith("thumbnails");
    }

    [Fact]
    public void The_Example_Follows_The_Switches_And_The_Format()
    {
        var sut = new SettingsDialogViewModel(settingsService);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.UseDailySubfolders = !sut.UseDailySubfolders;
        sut.SeparateThumbnailFolder = !sut.SeparateThumbnailFolder;
        sut.SelectedRecordingFormat = "mp4";

        changed.Should().Contain(nameof(SettingsDialogViewModel.RecordingPathExample));
        changed.Should().Contain(nameof(SettingsDialogViewModel.ThumbnailPathExample));
        sut.RecordingPathExample.Should().EndWith(".mp4");
    }

    [Fact]
    public async Task Saving_A_Changed_Layout_Moves_The_Recordings_Before_The_Dialog_Closes()
    {
        var synchronizer = Substitute.For<IRecordingLayoutSynchronizer>();
        var busyWhileMoving = false;
        var closedWhileMoving = false;
        var closed = false;

        var sut = new SettingsDialogViewModel(settingsService, synchronizer);
        sut.CloseRequested += (_, _) => closed = true;
        synchronizer.SynchronizeAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            busyWhileMoving = sut.IsApplyingRecordingLayout;
            closedWhileMoving = closed;
            return Task.FromResult(new RecordingLayoutReorganizeResult(0, 0, []));
        });

        sut.UseDailySubfolders = !settingsService.Recording.UseDailySubfolders;
        await sut.SaveAsync();

        await synchronizer.Received(1).SynchronizeAsync(Arg.Any<CancellationToken>());
        busyWhileMoving.Should().BeTrue("the dialog shows it is working while files move");
        closedWhileMoving.Should().BeFalse("the dialog stays open until the files are moved");
        sut.IsApplyingRecordingLayout.Should().BeFalse();
        closed.Should().BeTrue();
    }

    [Fact]
    public async Task Saving_Without_A_Layout_Change_Moves_Nothing()
    {
        var synchronizer = Substitute.For<IRecordingLayoutSynchronizer>();
        var sut = new SettingsDialogViewModel(settingsService, synchronizer);

        sut.EnableRecordingOnMotion = !sut.EnableRecordingOnMotion;
        await sut.SaveAsync();

        await synchronizer.DidNotReceive().SynchronizeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Failed_Move_Still_Saves_And_Closes()
    {
        var synchronizer = Substitute.For<IRecordingLayoutSynchronizer>();
        synchronizer.SynchronizeAsync(Arg.Any<CancellationToken>())
            .Returns<Task<RecordingLayoutReorganizeResult>>(_ => throw new System.IO.IOException("disk gone"));
        var closed = false;
        var sut = new SettingsDialogViewModel(settingsService, synchronizer);
        sut.CloseRequested += (_, _) => closed = true;

        var newValue = !settingsService.Recording.UseDailySubfolders;
        sut.UseDailySubfolders = newValue;
        await sut.SaveAsync();

        settingsService.Recording.UseDailySubfolders.Should().Be(newValue);
        sut.IsApplyingRecordingLayout.Should().BeFalse();
        closed.Should().BeTrue();
    }

    [Fact]
    public async Task A_Failed_Move_Is_Written_To_The_Log()
    {
        var synchronizer = Substitute.For<IRecordingLayoutSynchronizer>();
        synchronizer.SynchronizeAsync(Arg.Any<CancellationToken>())
            .Returns<Task<RecordingLayoutReorganizeResult>>(_ => throw new System.IO.IOException("disk gone"));
        var logger = new CollectingLogger<SettingsDialogViewModel>();
        var sut = new SettingsDialogViewModel(settingsService, synchronizer, logger);

        sut.UseDailySubfolders = !settingsService.Recording.UseDailySubfolders;
        await sut.SaveAsync();

        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("new folder layout failed", StringComparison.Ordinal));
    }
}