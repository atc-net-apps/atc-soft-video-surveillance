namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Removes one recording that a client names by its path below the
/// recording folder. The path comes from outside, so it is only followed
/// when it leads to a video file in a camera folder below that root.
/// </summary>
public static class RecordingRemover
{
    /// <summary>
    /// Removes a recording together with its thumbnail and activity index, and the day folder
    /// it leaves empty.
    /// </summary>
    /// <param name="recordingRoot">The recording folder.</param>
    /// <param name="relativePath">The recording's path below it, as a listing gives it (<c>{camera}/.../{file}</c>).</param>
    /// <param name="activePaths">The full paths of the files that are being recorded right now.</param>
    /// <returns>How it went.</returns>
    public static RecordingRemovalOutcome Remove(
        string recordingRoot,
        string? relativePath,
        IReadOnlySet<string> activePaths)
    {
        ArgumentNullException.ThrowIfNull(activePaths);

        if (!RecordingPathHelper.TryResolveRecording(recordingRoot, relativePath, out var fullPath))
        {
            return RecordingRemovalOutcome.NotARecording;
        }

        if (!File.Exists(fullPath))
        {
            return RecordingRemovalOutcome.NotFound;
        }

        // Asked of the file itself as well: another program may be
        // recording into the same folder.
        if (activePaths.Contains(fullPath) || RecordingPathHelper.IsBeingWritten(fullPath))
        {
            return RecordingRemovalOutcome.InProgress;
        }

        try
        {
            File.Delete(fullPath);
        }
        catch (IOException)
        {
            return RecordingRemovalOutcome.InProgress;
        }

        foreach (var thumbnail in RecordingPathHelper.GetCompanionPaths(fullPath, recordingRoot))
        {
            try
            {
                File.Delete(thumbnail);
            }
            catch (IOException)
            {
                // The recording is gone; a companion left behind is removed by the clean-up.
            }
            catch (UnauthorizedAccessException)
            {
                // As above.
            }
        }

        _ = MediaCleanupRunner.RemoveEmptyDirectoriesBelow(recordingRoot, MediaCleanupRunner.EmptyDirectoryGracePeriod);

        return RecordingRemovalOutcome.Removed;
    }
}