namespace AtcSoft.Video.Surveillance.Wpf.Core.Diagnostics;

/// <summary>
/// How a running recording changed since the last look of a <see cref="RecordingGrowthWatch"/>.
/// </summary>
/// <param name="Recording">The recording.</param>
/// <param name="GrowthBytes">How many bytes the file grew since the last look.</param>
/// <param name="State">What that means.</param>
/// <param name="StalledFor">How long the file has not grown.</param>
public sealed record RecordingGrowth(
    RecordingFile Recording,
    long GrowthBytes,
    RecordingGrowthState State,
    TimeSpan StalledFor);