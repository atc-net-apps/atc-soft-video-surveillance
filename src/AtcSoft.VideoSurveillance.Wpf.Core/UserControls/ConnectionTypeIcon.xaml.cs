namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// Small LAN / Wi-Fi / USB glyph, tinted by link health. Collapsed when
/// <see cref="ConnectionType"/> is <see cref="CameraConnectionType.None"/>.
/// Pass the <em>effective</em> connection type
/// (<c>CameraConnectionTypeHelper.GetEffective</c>), not the stored one.
/// </summary>
/// <remarks>
/// CS0169 / CS0414 are suppressed via &lt;NoWarn&gt; in the .csproj —
/// [DependencyProperty] backing fields are read by the source generator.
/// </remarks>
public partial class ConnectionTypeIcon
{
    private static readonly Brush PillBackground = CreatePillBackground();

    [DependencyProperty(DefaultValue = CameraConnectionType.None, PropertyChangedCallback = nameof(OnIconStateChanged))]
    private CameraConnectionType connectionType;

    [DependencyProperty(DefaultValue = ConnectionState.Connected, PropertyChangedCallback = nameof(OnIconStateChanged))]
    private ConnectionState connectionState;

    [DependencyProperty(DefaultValue = 14.0)]
    private double iconSize;

    [DependencyProperty(DefaultValue = true, PropertyChangedCallback = nameof(OnIconStateChanged))]
    private bool showBackground;

    public ConnectionTypeIcon()
    {
        InitializeComponent();
        UpdateIcon();
    }

    private static void OnIconStateChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is ConnectionTypeIcon icon)
        {
            icon.UpdateIcon();
        }
    }

    private static SolidColorBrush CreatePillBackground()
    {
        var brush = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0));
        brush.Freeze();
        return brush;
    }

    private void UpdateIcon()
    {
        var data = ConnectionTypeIconHelper.GetGeometryData(ConnectionType);
        if (data is null)
        {
            Visibility = Visibility.Collapsed;
            IconPath.Data = null;
            ToolTip = null;
            return;
        }

        IconPath.Data = Geometry.Parse(data);
        IconBorder.Background = ShowBackground ? PillBackground : Brushes.Transparent;
        IconBorder.Padding = ShowBackground ? new Thickness(5) : new Thickness(0);
        IconPath.Stroke = ConnectionTypeIconHelper.GetBrush(
            ConnectionTypeIconHelper.GetTint(ConnectionState));
        ToolTip = ConnectionTypeIconHelper.GetDisplayName(ConnectionType);
        Visibility = Visibility.Visible;
    }
}