using CoreRecordingEntry = AtcSoft.VideoSurveillance.Models.RecordingEntry;

namespace AtcSoft.VideoSurveillance.Api.Domain.Mapping;

internal static class RecordingMappingExtensions
{
    public static Recording ToApiModel(
        this CoreRecordingEntry core,
        Guid? cameraId = null)
        => new(
            Id: CreateId(core.FilePath),
            CameraId: cameraId ?? Guid.Empty,
            CameraName: core.CameraName,
            FilePath: core.FilePath,
            StartedAt: new DateTimeOffset(core.RecordingTime, TimeSpan.Zero),
            Duration: core.Duration.ToString("c"),
            FileSizeBytes: core.FileSizeBytes,
            HasThumbnail: core.HasThumbnail,
            InProgress: false,
            ThumbnailPath: null);

    /// <summary>
    /// Gets the id of a recording from its path, so the same file has the
    /// same id every time it is listed.
    /// </summary>
    public static Guid CreateId(string filePath)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(filePath));
        return new Guid(hash.AsSpan(0, 16));
    }
}