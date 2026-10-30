namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Makes the preview pictures of a finished recording, the small tiles a timeline shows under the
/// mouse, and writes them beside it (<see cref="Helpers.RecordingPathHelper.BuildPreviewImagePath"/>).
/// </summary>
public interface IRecordingPreviewBuilder
{
    /// <summary>
    /// Makes the previews of a recording, in place of any earlier ones.
    /// </summary>
    /// <param name="videoPath">The recording; finished, not one still being written.</param>
    /// <param name="cancellationToken">Stops the work; nothing is written then.</param>
    /// <returns>The layout written, or <see langword="null"/> for a file without a picture.</returns>
    RecordingPreviews? Build(
        string videoPath,
        CancellationToken cancellationToken = default);
}