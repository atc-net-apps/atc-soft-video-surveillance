namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using IWpfCoreSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService;
using RecordingEntry = AtcSoft.VideoSurveillance.Wpf.Core.Models.RecordingEntry;

/// <summary>
/// Playing a recording leaves the browser as it was, so the next recording
/// can be picked without opening the browser and filtering again.
/// </summary>
public sealed class RecordingsBrowserDialogTests : IDisposable
{
    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-browser-dialog-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (System.IO.Directory.Exists(root))
        {
            TestFolders.Delete(root);
        }
    }

    [Fact]
    public void The_Dialog_Stays_Open_With_Its_Filter_And_Selection_When_A_Recording_Is_Played()
    {
        // Arrange
        Touch("Front Door", "Front Door_20261012_101500_000.mkv");
        Touch("Garden", "Garden_20261012_220000_000.mkv");
        Touch("Garden", "Garden_20261012_230000_000.mkv");

        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });

        string? played = null;
        var closedByPlay = true;
        string? filterAfterPlay = null;
        string? selectedAfterPlay = null;

        // Act
        var error = WpfTestThread.Run(() =>
        {
            var viewModel = new RecordingsBrowserDialogViewModel(settingsService)
            {
                SelectedCameraFilter = "Garden",
            };

            RecordingEntry? playedRecording = null;
            var closed = false;
            var dialog = new RecordingsBrowserDialog(viewModel, recording => playedRecording = recording);
            dialog.Closed += (_, _) => closed = true;

            viewModel.SelectedRecording = viewModel.FilteredRecordings.Last();
            viewModel.PlayCommand.Execute(parameter: null);

            played = playedRecording?.FileName;
            closedByPlay = closed;
            filterAfterPlay = viewModel.SelectedCameraFilter;
            selectedAfterPlay = viewModel.SelectedRecording?.FileName;

            dialog.Close();
        });

        // Assert
        error.Should().BeNull();
        played.Should().Be("Garden_20261012_220000_000.mkv");
        closedByPlay.Should().BeFalse("the browser is still there when playback ends");
        filterAfterPlay.Should().Be("Garden");
        selectedAfterPlay.Should().Be("Garden_20261012_220000_000.mkv");
    }

    [Fact]
    public void The_Dialog_Closes_Before_Playing_When_The_User_Chose_That()
    {
        // Arrange
        Touch("Garden", "Garden_20261012_220000_000.mkv");

        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root, CloseRecordingsBrowserOnPlay = true });

        string? played = null;
        var closedBeforePlay = false;

        // Act
        var error = WpfTestThread.Run(() =>
        {
            var viewModel = new RecordingsBrowserDialogViewModel(settingsService);
            var closed = false;
            var dialog = new RecordingsBrowserDialog(
                viewModel,
                recording =>
                {
                    closedBeforePlay = closed;
                    played = recording.FileName;
                });
            dialog.Closed += (_, _) => closed = true;

            viewModel.SelectedRecording = viewModel.FilteredRecordings.Single();
            viewModel.PlayCommand.Execute(parameter: null);
        });

        // Assert
        error.Should().BeNull();
        played.Should().Be("Garden_20261012_220000_000.mkv");
        closedBeforePlay.Should().BeTrue();
    }

    [Fact]
    public void Enter_Plays_The_Selected_Recording()
    {
        // Arrange
        Touch("Garden", "Garden_20261012_220000_000.mkv");

        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });

        string? played = null;
        var handled = false;

        // Act
        var error = WpfTestThread.Run(() =>
        {
            var viewModel = new RecordingsBrowserDialogViewModel(settingsService);
            var dialog = new RecordingsBrowserDialog(viewModel, recording => played = recording.FileName);
            viewModel.SelectedRecording = viewModel.FilteredRecordings.Single();

            // A key event needs a source; the dialog itself is never shown.
            using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("test"));
            var grid = (System.Windows.Controls.DataGrid)dialog.FindName("RecordingsGrid");
            var enter = new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice,
                source,
                timestamp: 0,
                System.Windows.Input.Key.Enter)
            {
                RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
            };

            grid.RaiseEvent(enter);
            handled = enter.Handled;
        });

        // Assert — and the grid does not also move the selection down a row
        error.Should().BeNull();
        played.Should().Be("Garden_20261012_220000_000.mkv");
        handled.Should().BeTrue();
    }

    private void Touch(params string[] segments)
    {
        var path = System.IO.Path.Combine([root, .. segments]);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "x");
    }
}