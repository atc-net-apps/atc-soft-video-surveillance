namespace BoundingBoxWpfApp;

/// <summary>
/// A blob the detector found, as the debug layer draws it: its box in analysed pixels and what
/// became of it.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Label">What became of it, in a few words.</param>
/// <param name="Stroke">Its colour: grey when left out, white when shown.</param>
public sealed record CandidateView(
    double X,
    double Y,
    double Width,
    double Height,
    string Label,
    Brush Stroke)
{
    private static readonly Brush LeftOut = Frozen(Color.FromRgb(160, 160, 160));
    private static readonly Brush Shown = Frozen(Colors.White);

    /// <summary>
    /// Gets how a blob is drawn.
    /// </summary>
    /// <param name="candidate">The blob.</param>
    /// <returns>The view.</returns>
    public static CandidateView Of(MotionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var id = candidate.TrackId is { } track ? $"#{track} " : string.Empty;
        var label = candidate.State switch
        {
            MotionCandidateState.TooSmall => "too small",
            MotionCandidateState.TooNew => id + "too new",
            MotionCandidateState.NotTravelled => string.Create(CultureInfo.InvariantCulture, $"{id}not travelled {candidate.Travel:0.0}×"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{id}{candidate.Travel:0.0}×{(candidate.Held ? " held" : string.Empty)}"),
        };
        var box = candidate.Box;
        return new CandidateView(box.X, box.Y, box.Width, box.Height, label, candidate.State == MotionCandidateState.Shown ? Shown : LeftOut);
    }

    /// <summary>
    /// Gets a stage in words, for the debug line.
    /// </summary>
    /// <param name="stage">The stage.</param>
    /// <returns>The words.</returns>
    public static string Describe(MotionDiagnosticsStage stage)
        => stage switch
        {
            MotionDiagnosticsStage.Learning => "learning the first picture",
            MotionDiagnosticsStage.WarmUp => "warming up",
            MotionDiagnosticsStage.LightingChange => "change of light",
            MotionDiagnosticsStage.Settling => "settling after light",
            _ => "analysed",
        };

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}