namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// How far back a recording starts. A motion recording begins a few seconds before the motion
/// that started it, so the moment it was seen is in the file; any other recording begins at the
/// last key frame the camera sent, so its first picture's time is known.
/// </summary>
public static class RecordingPreRoll
{
    /// <summary>
    /// How far before the motion a motion recording starts.
    /// </summary>
    public static readonly TimeSpan Motion = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Starts a recording from the key frame <see cref="IMediaPipeline.FindRecordingStart"/> found, or
    /// at the next one when it found none.
    /// </summary>
    /// <param name="pipeline">The pipeline to record.</param>
    /// <param name="filePath">The file, named by <paramref name="startFromUtc"/> when there is one.</param>
    /// <param name="startFromUtc">What <see cref="IMediaPipeline.FindRecordingStart"/> returned.</param>
    public static void Start(
        IMediaPipeline pipeline,
        string filePath,
        DateTime? startFromUtc)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        if (startFromUtc is { } from)
        {
            pipeline.StartRecording(filePath, from);
        }
        else
        {
            pipeline.StartRecording(filePath);
        }
    }

    /// <summary>
    /// Moves a recording on to its next file, from the key frame <see cref="IMediaPipeline.FindRecordingStart"/>
    /// found (the two files then overlap a little), or at the next one when it found none.
    /// </summary>
    /// <param name="pipeline">The pipeline that records.</param>
    /// <param name="filePath">The next file, named by <paramref name="startFromUtc"/> when there is one.</param>
    /// <param name="startFromUtc">What <see cref="IMediaPipeline.FindRecordingStart"/> returned.</param>
    public static void Switch(
        IMediaPipeline pipeline,
        string filePath,
        DateTime? startFromUtc)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        if (startFromUtc is { } from)
        {
            pipeline.SwitchRecording(filePath, from);
        }
        else
        {
            pipeline.SwitchRecording(filePath);
        }
    }
}