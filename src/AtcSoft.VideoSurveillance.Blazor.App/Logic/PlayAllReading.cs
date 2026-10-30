namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// What a tile's video element reports to *play all from here* on each tick.
/// </summary>
/// <param name="Time">How far into the recording it is, in seconds.</param>
/// <param name="Opened">Whether it began to play since the last reading.</param>
/// <param name="Ended">Whether it played to its end since the last reading.</param>
/// <param name="Failed">Whether it failed since the last reading.</param>
public sealed record PlayAllReading(
    double Time,
    bool Opened,
    bool Ended,
    bool Failed);