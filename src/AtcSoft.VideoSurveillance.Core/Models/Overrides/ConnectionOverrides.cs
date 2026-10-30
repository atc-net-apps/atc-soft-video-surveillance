namespace AtcSoft.VideoSurveillance.Models.Overrides;

/// <summary>
/// Per-camera connection setting overrides.
/// </summary>
public class ConnectionOverrides
{
    public int? ConnectionTimeoutSeconds { get; set; }

    public int? ReconnectDelaySeconds { get; set; }

    public bool? AutoReconnectOnFailure { get; set; }

    public bool? ShowNotificationOnDisconnect { get; set; }

    public bool? ShowNotificationOnReconnect { get; set; }

    public bool? PlayNotificationSound { get; set; }

    public bool HasAnyOverride()
        => ConnectionTimeoutSeconds.HasValue ||
           ReconnectDelaySeconds.HasValue ||
           AutoReconnectOnFailure.HasValue ||
           ShowNotificationOnDisconnect.HasValue ||
           ShowNotificationOnReconnect.HasValue ||
           PlayNotificationSound.HasValue;

    /// <inheritdoc />
    public override string ToString()
    {
        var count = new[] { ConnectionTimeoutSeconds.HasValue, ReconnectDelaySeconds.HasValue, AutoReconnectOnFailure.HasValue, ShowNotificationOnDisconnect.HasValue, ShowNotificationOnReconnect.HasValue, PlayNotificationSound.HasValue }.Count(v => v);
        return $"ConnectionOverrides {{ NonNullOverrides={count.ToString(CultureInfo.InvariantCulture)} }}";
    }

    public ConnectionOverrides Clone()
        => new()
        {
            ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
            ReconnectDelaySeconds = ReconnectDelaySeconds,
            AutoReconnectOnFailure = AutoReconnectOnFailure,
            ShowNotificationOnDisconnect = ShowNotificationOnDisconnect,
            ShowNotificationOnReconnect = ShowNotificationOnReconnect,
            PlayNotificationSound = PlayNotificationSound,
        };

    public void CopyFrom(ConnectionOverrides? source)
    {
        ConnectionTimeoutSeconds = source?.ConnectionTimeoutSeconds;
        ReconnectDelaySeconds = source?.ReconnectDelaySeconds;
        AutoReconnectOnFailure = source?.AutoReconnectOnFailure;
        ShowNotificationOnDisconnect = source?.ShowNotificationOnDisconnect;
        ShowNotificationOnReconnect = source?.ShowNotificationOnReconnect;
        PlayNotificationSound = source?.PlayNotificationSound;
    }

    public bool ValueEquals(ConnectionOverrides? other)
    {
        if (other is null)
        {
            return !HasAnyOverride();
        }

        return ConnectionTimeoutSeconds == other.ConnectionTimeoutSeconds &&
               ReconnectDelaySeconds == other.ReconnectDelaySeconds &&
               AutoReconnectOnFailure == other.AutoReconnectOnFailure &&
               ShowNotificationOnDisconnect == other.ShowNotificationOnDisconnect &&
               ShowNotificationOnReconnect == other.ShowNotificationOnReconnect &&
               PlayNotificationSound == other.PlayNotificationSound;
    }
}