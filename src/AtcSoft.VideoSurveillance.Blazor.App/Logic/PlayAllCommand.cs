namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// One command to a tile's video element.
/// </summary>
/// <param name="Kind">What to do.</param>
/// <param name="Source">The address of the recording to open, for <see cref="PlayAllCommandKind.Open"/>.</param>
/// <param name="Seconds">How far into it, for <see cref="PlayAllCommandKind.Open"/>.</param>
public sealed record PlayAllCommand(
    PlayAllCommandKind Kind,
    string? Source = null,
    double Seconds = 0);