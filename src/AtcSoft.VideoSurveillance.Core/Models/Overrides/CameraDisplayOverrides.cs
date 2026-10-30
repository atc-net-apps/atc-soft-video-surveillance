namespace AtcSoft.VideoSurveillance.Models.Overrides;

/// <summary>
/// Per-camera display setting overrides.
/// </summary>
public class CameraDisplayOverrides
{
    public bool? ShowOverlayTitle { get; set; }

    public bool? ShowOverlayDescription { get; set; }

    public bool? ShowOverlayTime { get; set; }

    public bool? ShowOverlayConnectionStatus { get; set; }

    public bool? ShowOverlayQuickActions { get; set; }

    public bool? ShowOverlayConnectionTypeIcon { get; set; }

    public ConnectionTypeIconPlacement? ConnectionTypeIconPlacement { get; set; }

    public double? OverlayOpacity { get; set; }

    public OverlayPosition? OverlayPosition { get; set; }

    public bool HasAnyOverride()
        => ShowOverlayTitle.HasValue ||
           ShowOverlayDescription.HasValue ||
           ShowOverlayTime.HasValue ||
           ShowOverlayConnectionStatus.HasValue ||
           ShowOverlayQuickActions.HasValue ||
           ShowOverlayConnectionTypeIcon.HasValue ||
           ConnectionTypeIconPlacement.HasValue ||
           OverlayOpacity.HasValue ||
           OverlayPosition.HasValue;

    /// <inheritdoc />
    public override string ToString()
    {
        var count = new[] { ShowOverlayTitle.HasValue, ShowOverlayDescription.HasValue, ShowOverlayTime.HasValue, ShowOverlayConnectionStatus.HasValue, ShowOverlayQuickActions.HasValue, ShowOverlayConnectionTypeIcon.HasValue, ConnectionTypeIconPlacement.HasValue, OverlayOpacity.HasValue, OverlayPosition.HasValue }.Count(v => v);
        return $"CameraDisplayOverrides {{ NonNullOverrides={count.ToString(CultureInfo.InvariantCulture)} }}";
    }

    public CameraDisplayOverrides Clone()
        => new()
        {
            ShowOverlayTitle = ShowOverlayTitle,
            ShowOverlayDescription = ShowOverlayDescription,
            ShowOverlayTime = ShowOverlayTime,
            ShowOverlayConnectionStatus = ShowOverlayConnectionStatus,
            ShowOverlayQuickActions = ShowOverlayQuickActions,
            ShowOverlayConnectionTypeIcon = ShowOverlayConnectionTypeIcon,
            ConnectionTypeIconPlacement = ConnectionTypeIconPlacement,
            OverlayOpacity = OverlayOpacity,
            OverlayPosition = OverlayPosition,
        };

    public void CopyFrom(CameraDisplayOverrides? source)
    {
        ShowOverlayTitle = source?.ShowOverlayTitle;
        ShowOverlayDescription = source?.ShowOverlayDescription;
        ShowOverlayTime = source?.ShowOverlayTime;
        ShowOverlayConnectionStatus = source?.ShowOverlayConnectionStatus;
        ShowOverlayQuickActions = source?.ShowOverlayQuickActions;
        ShowOverlayConnectionTypeIcon = source?.ShowOverlayConnectionTypeIcon;
        ConnectionTypeIconPlacement = source?.ConnectionTypeIconPlacement;
        OverlayOpacity = source?.OverlayOpacity;
        OverlayPosition = source?.OverlayPosition;
    }

    public bool ValueEquals(CameraDisplayOverrides? other)
    {
        if (other is null)
        {
            return !HasAnyOverride();
        }

        return ShowOverlayTitle == other.ShowOverlayTitle &&
               ShowOverlayDescription == other.ShowOverlayDescription &&
               ShowOverlayTime == other.ShowOverlayTime &&
               ShowOverlayConnectionStatus == other.ShowOverlayConnectionStatus &&
               ShowOverlayQuickActions == other.ShowOverlayQuickActions &&
               ShowOverlayConnectionTypeIcon == other.ShowOverlayConnectionTypeIcon &&
               ConnectionTypeIconPlacement == other.ConnectionTypeIconPlacement &&
               OverlayOpacity.IsEqual(other.OverlayOpacity) &&
               OverlayPosition == other.OverlayPosition;
    }
}