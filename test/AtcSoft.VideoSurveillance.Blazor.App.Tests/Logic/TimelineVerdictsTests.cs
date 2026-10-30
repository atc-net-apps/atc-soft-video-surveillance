namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

using CoreEvent = AtcSoft.VideoSurveillance.Models.ActivityEvent;
using CoreVerdictNames = AtcSoft.VideoSurveillance.Helpers.Review.ReviewVerdicts;
using CoreVerdicts = AtcSoft.VideoSurveillance.Helpers.Review.TimelineVerdicts;
using Translations = AtcSoft.VideoSurveillance.Resources.Translations;

/// <summary>
/// A right-click on the web player's timeline offers what the desktop apps' does (a copy of Core's
/// rule, kept equal here), named as the server names the verdicts, in the desktop apps' words.
/// </summary>
public sealed class TimelineVerdictsTests
{
    private static readonly (double Start, double End)[][] Recordings =
    [
        [(0.2, 0.3), (0.6, 0.65)],
        [(0, 1)],
        [],
    ];

    [Fact]
    public void The_Copy_Offers_What_The_Desktop_Apps_Offer()
    {
        foreach (var recording in Recordings)
        {
            foreach (var duration in new[] { 0, 3, 5, 100, 3600 })
            {
                foreach (var place in new[] { 0, 0.001, 0.25, 0.31, 0.5, 0.66, 0.999, 1, 1.5 })
                {
                    foreach (var slack in new[] { 0, 0.02 })
                    {
                        TimelineVerdicts.At([.. recording.Select(e => new ActivityTimelineEvent(e.Start, e.End))], place, duration, slack)
                            .Select(v => (v.Verdict, v.FromSeconds, v.ToSeconds))
                            .Should().Equal(CoreVerdicts.At([.. recording.Select(e => new CoreEvent(e.Start, e.End))], place, duration, slack)
                                .Select(v => (v.Verdict, v.FromSeconds, v.ToSeconds)));
                    }
                }
            }
        }

        TimelineVerdicts.MissedSeconds.Should().Be(CoreVerdicts.MissedSeconds);
    }

    [Fact]
    public void The_Verdicts_Are_Named_As_The_Server_Names_Them_In_The_Desktop_Apps_Words()
    {
        (TimelineVerdicts.Correct, TimelineVerdicts.FalseBox, TimelineVerdicts.Missed)
            .Should().Be((CoreVerdictNames.Correct, CoreVerdictNames.FalseBox, CoreVerdictNames.Missed));
        TimelineVerdicts.Text(TimelineVerdicts.Correct).Should().Be(Translations.VerdictCorrect);
        TimelineVerdicts.Text(TimelineVerdicts.FalseBox).Should().Be(Translations.VerdictFalseAlarm);
        TimelineVerdicts.Text(TimelineVerdicts.Missed).Should().Be(Translations.VerdictMissed);
    }
}