namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Settings;

/// <summary>
/// Handler business logic for the GetStorageStatus operation.
/// </summary>
public sealed class GetStorageStatusHandler(
    IApplicationSettingsService settingsService) : IGetStorageStatusHandler
{
    // A share that is offline or a drive that is gone does not come back in seconds.
    private const int RetryAfterSeconds = 30;

    public Task<GetStorageStatusResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var recording = settingsService.Recording;
        var status = DiskSpaceStatus.Read(
            recording.RecordingPath,
            recording.Cleanup.MinFreeSpaceMb,
            recording.Cleanup.EnableDiskSpaceGuard);

        return Task.FromResult(status is null
            ? GetStorageStatusResult.ServiceUnavailable("The disk of the recording folder cannot be read.", retryAfter: RetryAfterSeconds)
            : GetStorageStatusResult.Ok(new StorageStatus(
                FreeBytes: status.FreeBytes,
                TotalBytes: status.TotalBytes,
                MinFreeBytes: status.MinFreeBytes,
                IsLow: status.IsLow,
                UsedPercent: status.UsedPercent,
                Text: status.Text)));
    }
}