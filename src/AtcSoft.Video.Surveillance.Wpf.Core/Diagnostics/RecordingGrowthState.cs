namespace AtcSoft.Video.Surveillance.Wpf.Core.Diagnostics;

/// <summary>
/// What a <see cref="RecordingGrowthWatch"/> saw a recording do since its last look.
/// </summary>
public enum RecordingGrowthState
{
    /// <summary>A file not seen before: a recording or segment that started.</summary>
    New,

    /// <summary>The file grew.</summary>
    Growing,

    /// <summary>The file did not grow, but not for long enough to count as stalled.</summary>
    Waiting,

    /// <summary>The file has not grown for the stall time: reported once.</summary>
    Stalled,

    /// <summary>The file is still stalled.</summary>
    StillStalled,

    /// <summary>The file grows again after it was stalled.</summary>
    Resumed,
}