namespace AtcSoft.VideoSurveillance.Storybook.Model;

/// <summary>
/// What a good detector does in the scene.
/// </summary>
public sealed class StorybookExpect
{
    /// <summary>
    /// Gets or sets the number of tracks expected, when the detector gives track ids.
    /// </summary>
    public int? Tracks { get; set; }

    public double MinRecall { get; set; } = 0.95;

    public double MaxFalseBoxesPerFrame { get; set; } = 0.02;

    public int MaxIdSwitchesPerObject { get; set; } = 1;

    /// <summary>
    /// Gets or sets how long a detector may take to find an object after it starts to move.
    /// Frames before that do not count against recall.
    /// </summary>
    public double MaxLatencySeconds { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the overlap at which a box matches an object (motion boxes are padded, so
    /// lower than the usual 0.5). A box holding the object's centre matches too.
    /// </summary>
    public double MatchIou { get; set; } = 0.3;

    public IList<StorybookSpan> Spans { get; init; } = [];

    /// <summary>
    /// Gets or sets why the detector does not meet these expectations yet, for a scene that shows
    /// a known problem: its scores are still kept in the baseline, its expectations are not
    /// asserted until it meets them, and then the mark has to go.
    /// </summary>
    public string? KnownBad { get; set; }
}