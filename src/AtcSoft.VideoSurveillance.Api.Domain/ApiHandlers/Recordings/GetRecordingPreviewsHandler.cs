namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Recordings;

/// <summary>
/// Handler business logic for the GetRecordingPreviews operation: where the preview pictures of a
/// recording are in their JPEG. A finished recording without them is queued for them.
/// </summary>
/// <param name="settingsService">The application settings.</param>
/// <param name="previews">The recordings waiting for previews; <see langword="null"/> where none are made.</param>
public sealed class GetRecordingPreviewsHandler(
    IApplicationSettingsService settingsService,
    IRecordingPreviews? previews = null) : IGetRecordingPreviewsHandler
{
    public Task<GetRecordingPreviewsResult> ExecuteAsync(
        GetRecordingPreviewsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var root = settingsService.Recording.RecordingPath;
        if (!RecordingPathHelper.TryResolveRecording(root, parameters.FilePath, out var videoPath))
        {
            return Task.FromResult(GetRecordingPreviewsResult.BadRequest("The path does not name a recording."));
        }

        if (!File.Exists(videoPath))
        {
            return Task.FromResult(GetRecordingPreviewsResult.NotFound("There is no such recording."));
        }

        if (RecordingPreviewFile.Read(videoPath) is not { } sheet)
        {
            // One from before the previews, or one whose making failed: made now, in the background.
            previews?.TryEnqueue(videoPath);
            return Task.FromResult(GetRecordingPreviewsResult.Ok(new RecordingPreviewSheet(
                HasPreviews: false,
                IsBeingMade: previews?.Contains(videoPath) == true,
                ImagePath: null,
                IntervalSeconds: 0,
                TileWidth: 0,
                TileHeight: 0,
                Columns: 0,
                GridCount: 0,
                Events: [])));
        }

        return Task.FromResult(GetRecordingPreviewsResult.Ok(new RecordingPreviewSheet(
            HasPreviews: true,
            IsBeingMade: previews?.Contains(videoPath) == true,
            ImagePath: RecordingCatalog.ToRelativePath(RecordingPathHelper.BuildPreviewImagePath(videoPath), root),
            IntervalSeconds: sheet.IntervalSeconds,
            TileWidth: sheet.TileWidth,
            TileHeight: sheet.TileHeight,
            Columns: sheet.Columns,
            GridCount: sheet.GridCount,
            Events: [.. sheet.Events.Select(e => new RecordingPreviewTile(Seconds: e.Seconds, Tile: e.Tile))])));
    }
}