namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// When a tile of *play all from here* has opened its recording where it was asked: its picture is
/// at most <see cref="Behind"/> before the asked place and at most <see cref="Ahead"/> past it. A
/// player that cannot seek starts at the beginning instead, and would pull every tile back.
/// </summary>
/// <remarks>
/// A copy of Core's <c>PlayAllStart</c>, kept equal by a test.
/// </remarks>
public static class PlayAllStart
{
    /// <summary>How far before the asked place the picture may be.</summary>
    public static readonly TimeSpan Behind = TimeSpan.FromSeconds(3);

    /// <summary>How far past the asked place the picture may be: it plays on while it opens.</summary>
    public static readonly TimeSpan Ahead = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets a value indicating whether the picture is near the asked place.
    /// </summary>
    /// <param name="position">Where the player is in the recording.</param>
    /// <param name="requested">Where it was asked to start.</param>
    public static bool IsNear(
        TimeSpan position,
        TimeSpan requested)
        => position >= requested - Behind && position <= requested + Ahead;
}