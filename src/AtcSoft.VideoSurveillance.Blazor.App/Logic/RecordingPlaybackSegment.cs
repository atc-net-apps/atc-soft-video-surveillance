namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A recording the web player moves on to when the one playing ends: the Timeline page plays a
/// camera's segments one after another.
/// </summary>
/// <param name="FilePath">The recording, as the listing names it.</param>
/// <param name="VideoUrl">Where the player fetches it.</param>
/// <param name="DownloadUrl">Where the browser saves it from.</param>
/// <param name="Title">The dialog's title while it plays.</param>
/// <param name="RecordingStart">When it started, for the time its timeline tells; <see langword="null"/> when not known.</param>
[SuppressMessage(
    "Design",
    "CA1054:URI parameters should not be strings",
    Justification = "As RecordingDisplay builds them and the player's src takes them.")]
[SuppressMessage(
    "Design",
    "CA1056:URI properties should not be strings",
    Justification = "As RecordingDisplay builds them and the player's src takes them.")]
public sealed record RecordingPlaybackSegment(
    string FilePath,
    string VideoUrl,
    string DownloadUrl,
    string Title,
    DateTimeOffset? RecordingStart = null);