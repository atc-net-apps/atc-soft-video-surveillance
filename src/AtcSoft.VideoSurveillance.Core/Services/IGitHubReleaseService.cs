namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Service for checking GitHub releases.
/// </summary>
public interface IGitHubReleaseService
{
    Task<Version?> GetLatestVersionAsync();

    Task<Uri?> GetLatestReleaseUrlAsync();

    Task<Uri?> GetLatestMsiDownloadUrlAsync();
}