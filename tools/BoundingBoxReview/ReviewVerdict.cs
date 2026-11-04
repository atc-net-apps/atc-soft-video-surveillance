namespace BoundingBoxReview;

/// <summary>
/// A quick verdict on a picture's yellow (detector) boxes, as written in <c>boxes.json</c>.
/// </summary>
public static class ReviewVerdict
{
    /// <summary>The boxes are right.</summary>
    public const string Good = "good";

    /// <summary>A little off, but acceptable.</summary>
    public const string Okay = "okay";

    /// <summary>Wrong: missing, too many, or far off.</summary>
    public const string Bad = "bad";

    /// <summary>Not judged (the picture is unclear).</summary>
    public const string Skip = "skip";

    public static string Symbol(string? verdict)
        => verdict switch
        {
            Good => "✓ good",
            Okay => "~ okay",
            Bad => "✗ bad",
            Skip => "⏭ skipped",
            _ => string.Empty,
        };
}