namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Event arguments for media cleanup completion.
/// </summary>
/// <param name="result">The cleanup result.</param>
public class MediaCleanupCompletedEventArgs(MediaCleanupResult result) : EventArgs
{
    /// <summary>
    /// Gets the cleanup result.
    /// </summary>
    public MediaCleanupResult Result { get; } = result ?? throw new ArgumentNullException(nameof(result));

    /// <inheritdoc />
    public override string ToString()
        => $"MediaCleanupCompleted {{ FilesDeleted={Result.TotalFilesDeleted.ToString(CultureInfo.InvariantCulture)}, BytesFreed={Result.BytesFreed.ToString(CultureInfo.InvariantCulture)} }}";
}