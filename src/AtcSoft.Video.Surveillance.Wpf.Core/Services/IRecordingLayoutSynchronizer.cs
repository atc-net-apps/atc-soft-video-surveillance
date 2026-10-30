namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

/// <summary>
/// Brings the recording folder in line with the folder layout settings:
/// restarts running recordings that are not in place and moves the existing
/// recordings and thumbnails. Hosts that record register an implementation;
/// without one, changing the layout only affects new recordings.
/// </summary>
public interface IRecordingLayoutSynchronizer
{
    /// <summary>
    /// Moves the recordings to match the current settings.
    /// </summary>
    /// <param name="cancellationToken">A token to stop waiting.</param>
    /// <returns>What was moved.</returns>
    Task<AtcSoft.Video.Surveillance.Helpers.RecordingLayoutReorganizeResult> SynchronizeAsync(
        CancellationToken cancellationToken = default);
}