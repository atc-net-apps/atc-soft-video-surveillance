namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// A recording the player moves on to when the one playing ends: a timeline plays a camera's
/// segments one after another.
/// </summary>
/// <param name="FilePath">The file or address to play.</param>
/// <param name="FileName">What the window shows as its name.</param>
/// <param name="RecordingStartTime">When the recording started, in local time, for the clock on the picture.</param>
/// <param name="ActivityLoader">Gets its activity for the timeline under the player, or <see langword="null"/>.</param>
/// <param name="PreviewLoader">Gets its preview pictures for the timeline, or <see langword="null"/>.</param>
/// <param name="VerdictSaver">Saves a verdict given on its timeline, or <see langword="null"/> for none.</param>
public sealed record RecordingSegment(
    string FilePath,
    string FileName,
    DateTime? RecordingStartTime,
    RecordingActivityLoader? ActivityLoader,
    RecordingPreviewLoader? PreviewLoader = null,
    RecordingVerdictSaver? VerdictSaver = null);