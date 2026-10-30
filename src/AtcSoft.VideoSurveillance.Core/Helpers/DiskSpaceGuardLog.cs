namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Writes what the disk-space guard found and did to a log, the same way in every app. A volume
/// that is still short or cannot be read is reported when that starts, not on every pass.
/// </summary>
public static partial class DiskSpaceGuardLog
{
    private const double BytesPerMegabyte = 1024 * 1024;

    /// <summary>
    /// Writes a guard result to the log.
    /// </summary>
    /// <param name="logger">The log.</param>
    /// <param name="result">What the guard found and did.</param>
    /// <param name="minFreeMb">The space the guard keeps free, in megabytes.</param>
    public static void Write(
        ILogger logger,
        DiskSpaceGuardResult result,
        int minFreeMb)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(result);

        foreach (var volume in result.Volumes)
        {
            foreach (var file in volume.Run.DeletedFiles.Concat(volume.Run.DeletedThumbnails))
            {
                LogDeleted(logger, file);
            }

            foreach (var error in volume.Run.Errors)
            {
                LogDeleteFailed(logger, error.Exception, error.Path);
            }

            var freeMb = (volume.FreeBytes ?? 0) / BytesPerMegabyte;
            var freedMb = volume.Run.BytesFreed / BytesPerMegabyte;
            switch (volume.State)
            {
                case DiskSpaceVolumeState.Reclaimed:
                    LogReclaimed(logger, volume.Volume, freeMb, minFreeMb, volume.Run.DeletedFiles.Count, freedMb);
                    break;
                case DiskSpaceVolumeState.StillShort when volume.StateChanged:
                    LogStillShort(logger, volume.Volume, freeMb, minFreeMb, freedMb);
                    break;
                case DiskSpaceVolumeState.Unreadable when volume.StateChanged:
                    LogUnreadable(logger, volume.Volume);
                    break;
                case DiskSpaceVolumeState.Ok when volume.StateChanged && volume.FreeBytes is not null:
                    LogOk(logger, volume.Volume, freeMb, minFreeMb);
                    break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Disk-space guard deleted '{Path}'")]
    private static partial void LogDeleted(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disk-space guard could not delete '{Path}'")]
    private static partial void LogDeleteFailed(ILogger logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disk space low on '{Volume}': {FreeMb:F0} MB free, {MinFreeMb} MB kept free; deleted the {Count} oldest files ({FreedMb:F0} MB)")]
    private static partial void LogReclaimed(ILogger logger, string volume, double freeMb, int minFreeMb, int count, double freedMb);

    [LoggerMessage(Level = LogLevel.Error, Message = "Disk space low on '{Volume}': {FreeMb:F0} MB free, {MinFreeMb} MB kept free, and nothing more may be deleted ({FreedMb:F0} MB freed). Recordings on it are stopped and new ones refused")]
    private static partial void LogStillShort(ILogger logger, string volume, double freeMb, int minFreeMb, double freedMb);

    [LoggerMessage(Level = LogLevel.Error, Message = "The free space of '{Volume}' cannot be read. New recordings there are refused; running ones go on")]
    private static partial void LogUnreadable(ILogger logger, string volume);

    [LoggerMessage(Level = LogLevel.Information, Message = "Disk space on '{Volume}': {FreeMb:F0} MB free, {MinFreeMb} MB kept free")]
    private static partial void LogOk(ILogger logger, string volume, double freeMb, int minFreeMb);
}