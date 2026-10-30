namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// Which days of recordings are shown.
/// </summary>
public enum RecordingDayFilter
{
    All,
    Today,
    Yesterday,
    Last7Days,
    Last30Days,
}