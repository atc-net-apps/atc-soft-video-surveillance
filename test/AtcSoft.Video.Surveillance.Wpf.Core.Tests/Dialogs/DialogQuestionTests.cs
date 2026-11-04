namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// A yes/no question asked inside a dialog, never in a window of its own: it shows until it is
/// answered, and a new question answers the open one no.
/// </summary>
public sealed class DialogQuestionTests
{
    [Fact]
    public async Task Yes_Answers_True_And_Closes_The_Question()
    {
        var sut = new DialogQuestion();

        var answer = sut.AskAsync("Remove ops?");
        sut.IsOpen.Should().BeTrue();
        sut.Text.Should().Be("Remove ops?");
        sut.YesCommand.Execute(parameter: null);

        (await answer).Should().BeTrue();
        sut.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task No_Answers_False()
    {
        var sut = new DialogQuestion();

        var answer = sut.AskAsync("Remove ops?");
        sut.NoCommand.Execute(parameter: null);

        (await answer).Should().BeFalse();
    }

    [Fact]
    public async Task A_New_Question_Answers_The_Open_One_No()
    {
        var sut = new DialogQuestion();

        var first = sut.AskAsync("First?");
        var second = sut.AskAsync("Second?");
        sut.YesCommand.Execute(parameter: null);

        (await first).Should().BeFalse();
        (await second).Should().BeTrue();
    }
}