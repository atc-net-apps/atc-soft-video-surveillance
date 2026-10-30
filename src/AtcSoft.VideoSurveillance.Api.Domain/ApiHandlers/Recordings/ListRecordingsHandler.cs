namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Recordings;

/// <summary>
/// Handler business logic for the ListRecordings operation.
/// </summary>
/// <param name="recordingService">The recordings in progress.</param>
/// <param name="settingsService">The application settings.</param>
/// <param name="cameraStorageService">The cameras.</param>
/// <param name="durationProvider">Reads how long a finished recording is. Optional: without it the length is left out.</param>
/// <param name="sparklines">The recordings' sparklines already read. Optional: without it there are none.</param>
public sealed class ListRecordingsHandler(
    IRecordingService recordingService,
    IApplicationSettingsService settingsService,
    ICameraStorageService cameraStorageService,
    IRecordingDurationProvider? durationProvider = null,
    ActivitySparklineCache? sparklines = null) : IListRecordingsHandler
{
    public Task<ListRecordingsResult> ExecuteAsync(
        ListRecordingsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var recordingPath = settingsService.Recording.RecordingPath;
        var files = RecordingCatalog
            .Find(recordingPath, cameraStorageService.GetAllCameras(), recordingService.GetActiveSessions(), parameters.CameraId, DateTimeOffset.UtcNow);

        // A cold listing reads many indexes: in parallel, as the timeline does.
        var activity = new IReadOnlyList<int>?[files.Count];
        if (sparklines is not null)
        {
            Parallel.For(
                0,
                files.Count,
                new ParallelOptions { CancellationToken = cancellationToken },
                i => activity[i] = sparklines.Get(RecordingPathHelper.BuildActivityPath(files[i].FullPath)));
        }

        var recordings = files
            .Select((file, i) => ToRecording(file, recordingPath, activity[i]))
            .OrderByDescending(r => r.StartedAt)
            .ToList();

        return Task.FromResult(ListRecordingsResult.Ok(recordings));
    }

    private Recording ToRecording(
        RecordingFile file,
        string recordingPath,
        IReadOnlyList<int>? sparkline)
    {
        var thumbnail = FindRelativeThumbnail(file.FullPath, recordingPath);
        string length;
        if (file.InProgress)
        {
            length = System.Xml.XmlConvert.ToString(file.LastWrittenAt - file.StartedAt);
        }
        else
        {
            length = durationProvider?.GetDuration(file.FullPath) is { } duration
                ? System.Xml.XmlConvert.ToString(duration)
                : string.Empty;
        }

        return new Recording(
            Id: file.InProgress ? file.CameraId : RecordingMappingExtensions.CreateId(file.RelativePath),
            CameraId: file.CameraId,
            CameraName: file.CameraName,
            FilePath: file.RelativePath,
            StartedAt: file.StartedAt,
            Duration: length,
            FileSizeBytes: file.Length,
            HasThumbnail: thumbnail is not null,
            InProgress: file.InProgress,
            ThumbnailPath: thumbnail,
            Activity: sparkline is null ? null : [.. sparkline]);
    }

    private static string? FindRelativeThumbnail(
        string videoPath,
        string recordingPath)
    {
        if (string.IsNullOrEmpty(recordingPath))
        {
            return null;
        }

        var thumbnail = RecordingPathHelper.FindThumbnailPath(videoPath, recordingPath);
        if (thumbnail is null)
        {
            return null;
        }

        var relative = RecordingCatalog.ToRelativePath(thumbnail, recordingPath);
        return Path.IsPathRooted(relative) ? null : relative;
    }
}