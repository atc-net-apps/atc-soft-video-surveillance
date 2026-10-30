namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class RecordingDeletionTests
{
    [Fact]
    public void The_Question_Names_The_Camera_And_The_Time_And_Says_It_Cannot_Be_Undone()
    {
        var question = RecordingDeletion.Question("Front Door", "12-10-2026 10:15");

        question.Should().Be("Delete the recording of Front Door from 12-10-2026 10:15? The file is removed from the server and cannot be brought back.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_Recording_Of_A_Camera_That_No_Longer_Exists_Is_Asked_About_Without_A_Name(
        string? cameraName)
        => RecordingDeletion.Question(cameraName, "12-10-2026 10:15")
            .Should().StartWith("Delete the recording from 12-10-2026 10:15?");

    [Theory]
    [InlineData(RecordingDeletionOutcome.Deleted, "Recording deleted.", true)]
    [InlineData(RecordingDeletionOutcome.InProgress, "The recording is still being made. Delete it when it has ended.", false)]
    [InlineData(RecordingDeletionOutcome.NotFound, "The recording was already gone.", false)]
    [InlineData(RecordingDeletionOutcome.Failed, "The recording could not be deleted.", false)]
    public void The_Answer_Says_What_Happened(
        RecordingDeletionOutcome outcome,
        string expectedText,
        bool expectedSuccess)
    {
        RecordingDeletion.ResultText(outcome).Should().Be(expectedText);
        RecordingDeletion.Succeeded(outcome).Should().Be(expectedSuccess);
    }

    [Theory]
    [InlineData(RecordingDeletionOutcome.Deleted, true)]
    [InlineData(RecordingDeletionOutcome.NotFound, true)]
    [InlineData(RecordingDeletionOutcome.InProgress, false)]
    [InlineData(RecordingDeletionOutcome.Failed, false)]
    public void A_Recording_That_Is_Gone_Leaves_The_List_Whoever_Removed_It(
        RecordingDeletionOutcome outcome,
        bool expected)
        => RecordingDeletion.IsGone(outcome).Should().Be(expected);
}