namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

using ApplicationSettingsService = AtcSoft.Video.Surveillance.Wpf.Core.Services.ApplicationSettingsService;

public sealed class SettingsDialogTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-settings-dialog-" + Guid.NewGuid().ToString("N"));

    public SettingsDialogTests()
    {
        System.IO.Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(directory))
        {
            TestFolders.Delete(directory);
        }
    }

    [Fact]
    public void The_Dialog_Can_Be_Created()
    {
        // Loading the XAML resolves every control, resource and property in
        // it, so a control or property that does not exist fails here
        // instead of when a user opens Settings.
        var settingsService = new ApplicationSettingsService(System.IO.Path.Combine(directory, "settings.json"));

        var error = WpfTestThread.Run(() =>
        {
            var dialog = new SettingsDialog(new SettingsDialogViewModel(settingsService));
            dialog.Close();
        });

        error.Should().BeNull();
    }

    [Fact]
    public void The_Dialog_Stays_Open_While_Recordings_Are_Moved_And_Closes_When_They_Are()
    {
        // Closing the window during the move set DialogResult on a closed window afterwards: the
        // app crashed.
        var settingsService = new ApplicationSettingsService(System.IO.Path.Combine(directory, "settings.json"));
        var move = new TaskCompletionSource<AtcSoft.Video.Surveillance.Helpers.RecordingLayoutReorganizeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var synchronizer = Substitute.For<AtcSoft.Video.Surveillance.Wpf.Core.Services.IRecordingLayoutSynchronizer>();
        synchronizer.SynchronizeAsync(Arg.Any<CancellationToken>()).Returns(move.Task);
        var openDuringMove = false;
        bool? result = null;

        var error = WpfTestThread.Run(() =>
        {
            var viewModel = new SettingsDialogViewModel(settingsService, synchronizer);
            var dialog = new SettingsDialog(viewModel);
            dialog.Loaded += async (_, _) =>
            {
                viewModel.UseDailySubfolders = !settingsService.Recording.UseDailySubfolders;
                var saving = viewModel.SaveAsync();

                dialog.Close();
                openDuringMove = dialog.IsVisible;

                move.SetResult(default!);
                await saving.ConfigureAwait(true);
            };

            // Never left open by a failing test.
            var stop = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            stop.Tick += (_, _) =>
            {
                stop.Stop();
                move.TrySetResult(default!);
            };
            stop.Start();

            result = dialog.ShowDialog();
            stop.Stop();
        });

        error.Should().BeNull();
        openDuringMove.Should().BeTrue("the window stays open while recordings are moved");
        result.Should().BeTrue("it closes by itself, saved, when the move is done");
    }
}