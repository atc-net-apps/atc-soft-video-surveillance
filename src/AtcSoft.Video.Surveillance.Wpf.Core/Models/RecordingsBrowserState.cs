namespace AtcSoft.Video.Surveillance.Wpf.Core.Models;

/// <summary>
/// What the recordings browser looked like when it was closed, so it can
/// be opened the same way again.
/// </summary>
/// <param name="CameraFilters">The ticked cameras; none means every camera.</param>
/// <param name="DayFilter">The selected day filter key.</param>
/// <param name="TimeFilter">The selected time filter key.</param>
/// <param name="SelectedFilePath">The path of the selected recording, if any.</param>
public sealed record RecordingsBrowserState(
    IReadOnlyList<string> CameraFilters,
    string DayFilter,
    string TimeFilter,
    string? SelectedFilePath);