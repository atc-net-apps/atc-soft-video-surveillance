namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// Gives a verdict on a player's activity timeline as a person does: a right-click on its middle
/// opens a menu of the verdicts offered there (a pop-up window of the app's own), one is chosen by
/// its en-US text, and the player says it is saved in the recording's review.
/// </summary>
internal static class VerdictWalk
{
    public const string Saved = "Saved in the recording's review.";

    /// <summary>
    /// Right-clicks the middle of a timeline and chooses a verdict.
    /// </summary>
    /// <param name="app">The app.</param>
    /// <param name="player">The player window.</param>
    /// <param name="timeline">The timeline in it.</param>
    /// <param name="verdict">The menu item's text: <c>Boxed correctly</c>, <c>False alarm</c> or <c>Missed something here</c>.</param>
    public static void Give(
        DesktopApp app,
        AutomationElement player,
        AutomationElement timeline,
        string verdict)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(timeline);

        var bounds = timeline.BoundingRectangle;
        FlaUI.Core.Input.Mouse.RightClick(new System.Drawing.Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2)));

        var desktop = app.Automation.GetDesktop();
        var processId = timeline.Properties.ProcessId.Value;
        var deadline = DateTime.UtcNow + DesktopApp.ElementTimeout;
        AutomationElement? item = null;
        while (item is null && DateTime.UtcNow < deadline)
        {
            item = desktop
                .FindAllChildren(app.Conditions.ByProcessId(processId))
                .Select(w => w.FindFirstDescendant(app.Conditions.ByControlType(ControlType.MenuItem).And(app.Conditions.ByName(verdict))))
                .FirstOrDefault(i => i is not null);
            if (item is null)
            {
                Thread.Sleep(200);
            }
        }

        if (item is null)
        {
            throw app.Fail($"A right-click on the timeline did not offer '{verdict}'.");
        }

        item.Patterns.Invoke.Pattern.Invoke();
        app.Find(player, Saved, timeout: TimeSpan.FromSeconds(20));
    }

    /// <summary>
    /// Reads the marked stretches of every review written under a folder (the server's is shared by
    /// the walks of both API clients).
    /// </summary>
    /// <param name="folder">The app's or the server's data folder.</param>
    /// <returns>The stretches.</returns>
    public static IReadOnlyList<ReviewSpan> SpansUnder(string folder)
        => [.. Directory.GetFiles(folder, "*" + RecordingPathHelper.ReviewExtension, SearchOption.AllDirectories)
            .SelectMany(review => RecordingReviewFile.Read(review).Spans)];
}