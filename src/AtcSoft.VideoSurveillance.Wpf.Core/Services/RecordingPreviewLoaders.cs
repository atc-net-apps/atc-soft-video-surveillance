namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// The ways a recording's preview pictures are loaded.
/// </summary>
public static class RecordingPreviewLoaders
{
    /// <summary>
    /// Reads the previews beside a recording on this machine. A recording without them is queued
    /// for them, when the app makes previews.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="previews">The app's previews in the making; <see langword="null"/> where none are made.</param>
    /// <returns>The loader.</returns>
    public static RecordingPreviewLoader FromFile(
        string videoPath,
        IRecordingPreviews? previews)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        return cancellationToken => Task.Run(
            () =>
            {
                if (RecordingPreviewFile.Read(videoPath) is { } layout &&
                    RecordingPreviewTiles.FromBytes(layout, File.ReadAllBytes(RecordingPathHelper.BuildPreviewImagePath(videoPath))) is { } sheet)
                {
                    return new RecordingPreviewLoad(sheet, IsBeingMade: false);
                }

                previews?.TryEnqueue(videoPath);
                return new RecordingPreviewLoad(Sheet: null, IsBeingMade: previews?.Contains(videoPath) == true);
            },
            cancellationToken);
    }
}