namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The filters of the recordings list.
/// </summary>
public sealed class RecordingFilter
{
    /// <summary>
    /// Gets or sets the camera to show, or <c>null</c> for all cameras.
    /// </summary>
    public Guid? CameraId { get; set; }

    /// <summary>
    /// Gets or sets the days to show.
    /// </summary>
    public RecordingDayFilter Day { get; set; }

    /// <summary>
    /// Returns the recordings that pass the filters, in their given order.
    /// Days are the viewer's days: <paramref name="now"/> carries the
    /// viewer's offset from UTC.
    /// </summary>
    public IEnumerable<Recording> Apply(
        IEnumerable<Recording> recordings,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(recordings);

        var today = now.Date;
        var (from, to) = Day switch
        {
            RecordingDayFilter.Today => (today, today),
            RecordingDayFilter.Yesterday => (today.AddDays(-1), today.AddDays(-1)),
            RecordingDayFilter.Last7Days => (today.AddDays(-6), today),
            RecordingDayFilter.Last30Days => (today.AddDays(-29), today),
            _ => (DateTime.MinValue, DateTime.MaxValue),
        };

        return recordings.Where(recording =>
        {
            if (CameraId is not null && recording.CameraId != CameraId)
            {
                return false;
            }

            var day = recording.StartedAt.ToOffset(now.Offset).Date;
            return day >= from && day <= to;
        });
    }
}