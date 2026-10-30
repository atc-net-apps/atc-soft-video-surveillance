namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class ProblemTextTests
{
    [Fact]
    public void The_Page_Sentence_Is_Followed_By_The_Server_Reason()
        => ProblemText.WithReason("Failed to update camera.", "Camera 42 not found.")
            .Should().Be("Failed to update camera. Camera 42 not found.");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Without_A_Reason_The_Page_Sentence_Stands_Alone(string? reason)
        => ProblemText.WithReason("Failed to update camera.", reason).Should().Be("Failed to update camera.");
}