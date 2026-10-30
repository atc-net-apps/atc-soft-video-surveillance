namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

using RecordingVerdictSaver = AtcSoft.VideoSurveillance.Wpf.Core.Services.RecordingVerdictSaver;
using TimelineVerdict = AtcSoft.VideoSurveillance.Helpers.Review.TimelineVerdict;
using Translations = AtcSoft.VideoSurveillance.Resources.Translations;

/// <summary>
/// A verdict chosen on the player's timeline is handed to the window's saver, and the window says
/// whether it was saved or, in the saver's words, why not. A window without a saver offers none.
/// </summary>
public sealed class FullScreenRecordingVerdictTests
{
    private static readonly TimelineVerdict Correct = new("correct", 20, 30);

    [Fact]
    public void A_Verdict_Is_Saved_And_The_Window_Says_So()
    {
        TimelineVerdict? saved = null;
        var message = string.Empty;

        var error = WpfTestThread.Run(() =>
        {
            using var viewModel = Player((verdict, _) =>
            {
                saved = verdict;
                return Task.CompletedTask;
            });

            viewModel.GiveVerdictCommand.CanExecute(Correct).Should().BeTrue();
            viewModel.GiveVerdictCommand.ExecuteAsync(Correct).GetAwaiter().GetResult();
            message = viewModel.VerdictMessage;
        });

        error.Should().BeNull();
        saved.Should().Be(Correct);
        message.Should().Be(Translations.VerdictSaved);
    }

    [Fact]
    public void A_Verdict_The_Server_Refuses_Is_Told_In_Its_Words()
    {
        var message = string.Empty;

        var error = WpfTestThread.Run(() =>
        {
            using var viewModel = Player((_, _) => Task.FromException(new System.Net.Http.HttpRequestException("The recording is still being made.")));

            viewModel.GiveVerdictCommand.ExecuteAsync(Correct).GetAwaiter().GetResult();
            message = viewModel.VerdictMessage;
        });

        error.Should().BeNull();
        message.Should().Be(string.Format(CultureInfo.CurrentCulture, Translations.VerdictNotSaved1, "The recording is still being made."));
    }

    [Fact]
    public void A_Window_Without_A_Saver_Offers_No_Verdicts()
    {
        var canGive = true;

        var error = WpfTestThread.Run(() =>
        {
            using var viewModel = Player(saver: null);
            canGive = viewModel.GiveVerdictCommand.CanExecute(Correct);
        });

        error.Should().BeNull();
        canGive.Should().BeFalse();
    }

    private static FullScreenRecordingWindowViewModel Player(
        RecordingVerdictSaver? saver)
        => new(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "no-such-recording.mkv"),
            "Cam_20261012_101500_000.mkv",
            verdictSaver: saver);
}