namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// The editor of a camera's motion zones: the zones drawn on a snapshot, with buttons to take a
/// snapshot, add and remove zones and corners, and the picked zone's name. Bind <see cref="Zones"/>
/// two-way: each change sets a new list of new zones, never changes the old ones.
/// </summary>
/// <remarks>
/// CS0169 / CS0414 are suppressed via &lt;NoWarn&gt; in the .csproj —
/// [DependencyProperty] backing fields are read by the source generator.
/// </remarks>
public partial class MotionZoneEditor
{
    [DependencyProperty(PropertyChangedCallback = nameof(OnZonesChanged))]
    private IReadOnlyList<MotionZone>? zones;

    [DependencyProperty]
    private ImageSource? picture;

    [DependencyProperty]
    private ICommand? takePictureCommand;

    [DependencyProperty(PropertyChangedCallback = nameof(OnStatusChanged))]
    private string? status;

    [DependencyProperty(DefaultValue = -1, PropertyChangedCallback = nameof(OnSelectionChanged))]
    private int selectedZone = -1;

    [DependencyProperty(DefaultValue = -1, PropertyChangedCallback = nameof(OnSelectionChanged))]
    private int selectedCorner = -1;

    [DependencyProperty(PropertyChangedCallback = nameof(OnZoneNameChanged))]
    private string? zoneName;

    [DependencyProperty(PropertyChangedCallback = nameof(OnZoneKindKeyChanged))]
    private string? zoneKindKey;

    [DependencyProperty(PropertyChangedCallback = nameof(OnZoneSensitivityTextChanged))]
    private string? zoneSensitivityText;

    [DependencyProperty(PropertyChangedCallback = nameof(OnZoneSmallestObjectTextChanged))]
    private string? zoneSmallestObjectText;

    [DependencyProperty(PropertyChangedCallback = nameof(OnZoneColorKeyChanged))]
    private string? zoneColorKey;

    // The camera's smallest object, shown in a watch zone's empty field; null shows "Camera's own".
    [DependencyProperty(PropertyChangedCallback = nameof(OnCameraSmallestObjectPercentChanged))]
    private double? cameraSmallestObjectPercent;

    // Set while the fields are filled from the picked zone, so that is not taken as an edit.
    private bool showingName;

    // Atc.Wpf finds the texts only in assemblies already loaded: load them before the XAML.
    static MotionZoneEditor()
    {
        _ = Translations.ResourceManager;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MotionZoneEditor"/> class.
    /// </summary>
    public MotionZoneEditor()
    {
        InitializeComponent();
        SmallestObjectBox.WatermarkText = SmallestObjectHint(cameraPercent: null);
    }

    /// <summary>
    /// Gets the kinds a zone can be: ignored, or watched with its own values.
    /// </summary>
    public static IDictionary<string, string> KindItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(MotionZoneKind.Ignore)] = Translations.ZoneKindIgnore,
        [nameof(MotionZoneKind.Watch)] = Translations.ZoneKindWatch,
    };

    /// <summary>
    /// The hint a watch zone's empty smallest-object field shows: the camera's value, which the zone
    /// then takes, or "Camera's own" when the editor is not told it.
    /// </summary>
    /// <param name="cameraPercent">The camera's smallest object in percent of the picture, or null.</param>
    /// <returns>The text to show.</returns>
    public static string SmallestObjectHint(double? cameraPercent)
        => cameraPercent is { } percent
            ? percent.ToString(CultureInfo.CurrentCulture)
            : Translations.CamerasOwn;

    private IReadOnlyList<MotionZone> Current
        => Zones ?? [];

    private MotionZone? Picked
        => SelectedZone >= 0 && SelectedZone < Current.Count
            ? Current[SelectedZone]
            : null;

    private static void OnZonesChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not MotionZoneEditor editor)
        {
            return;
        }

        if (editor.SelectedZone >= editor.Current.Count)
        {
            editor.SetCurrentValue(SelectedZoneProperty, -1);
        }

        if (editor.Picked is { } zone && editor.SelectedCorner >= zone.Points.Count)
        {
            editor.SetCurrentValue(SelectedCornerProperty, -1);
        }

        editor.Refresh();
    }

    private static void OnSelectionChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionZoneEditor editor)
        {
            editor.Refresh();
        }
    }

    private static void OnStatusChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionZoneEditor editor)
        {
            editor.StatusText.Visibility = string.IsNullOrEmpty(editor.Status)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private static void OnZoneNameChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not MotionZoneEditor editor ||
            editor.showingName ||
            editor.Picked is not { } zone)
        {
            return;
        }

        var name = editor.ZoneName ?? string.Empty;
        if (name.Length > MotionZoneEditing.MaxNameLength)
        {
            name = name[..MotionZoneEditing.MaxNameLength];
        }

        if (!string.Equals(name, zone.Name, StringComparison.Ordinal))
        {
            editor.Change(editor.SelectedZone, z => z.Name = name);
        }
    }

    private static void OnZoneKindKeyChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not MotionZoneEditor editor ||
            editor.showingName ||
            editor.Picked is not { } zone ||
            !Enum.TryParse<MotionZoneKind>(editor.ZoneKindKey, out var kind) ||
            kind == zone.Kind)
        {
            return;
        }

        // An ignore zone has no values of its own.
        editor.Change(editor.SelectedZone, z =>
        {
            z.Kind = kind;
            if (kind == MotionZoneKind.Ignore)
            {
                z.Sensitivity = null;
                z.SmallestObjectPercent = null;
                z.Color = null;
            }
        });
    }

    private static void OnZoneSensitivityTextChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionZoneEditor editor &&
            !editor.showingName &&
            editor.Picked is { } zone &&
            MotionZoneEditing.TryReadSensitivity(editor.ZoneSensitivityText, CultureInfo.CurrentCulture, out var value) &&
            value != zone.Sensitivity)
        {
            editor.Change(editor.SelectedZone, z => z.Sensitivity = value);
        }
    }

    private static void OnZoneSmallestObjectTextChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionZoneEditor editor &&
            !editor.showingName &&
            editor.Picked is { } zone &&
            MotionZoneEditing.TryReadSmallestObjectPercent(editor.ZoneSmallestObjectText, CultureInfo.CurrentCulture, out var value) &&
            !EqualityComparer<double?>.Default.Equals(value, zone.SmallestObjectPercent))
        {
            editor.Change(editor.SelectedZone, z => z.SmallestObjectPercent = value);
        }
    }

    private static void OnCameraSmallestObjectPercentChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionZoneEditor editor)
        {
            editor.SmallestObjectBox.WatermarkText = SmallestObjectHint(editor.CameraSmallestObjectPercent);
        }
    }

    private static void OnZoneColorKeyChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var color = d is MotionZoneEditor { ZoneColorKey: { Length: > 0 } key } ? key : null;
        if (d is MotionZoneEditor editor &&
            !editor.showingName &&
            editor.Picked is { } zone &&
            !string.Equals(color, zone.Color, StringComparison.Ordinal))
        {
            editor.Change(editor.SelectedZone, z => z.Color = color);
        }
    }

    private void OnZonesEdited(
        object? sender,
        MotionZonesEditedEventArgs e)
        => SetCurrentValue(ZonesProperty, e.Zones);

    private void OnAddZone(
        object sender,
        RoutedEventArgs e)
    {
        var current = Current;
        if (!MotionZoneEditing.CanAddZone(current.Count))
        {
            return;
        }

        var number = MotionZoneEditing.NextNumber(current.Select(z => z.Name), NameFor);
        var added = current.Select(z => z.Clone()).ToList();
        added.Add(new MotionZone
        {
            Name = NameFor(number),
            Kind = MotionZoneKind.Ignore,
            Points = [.. MotionZoneEditing.NewZoneCorners(current.Count)],
        });

        SetCurrentValue(ZonesProperty, added);
        SetCurrentValue(SelectedCornerProperty, -1);
        SetCurrentValue(SelectedZoneProperty, added.Count - 1);
    }

    private void OnRemoveZone(
        object sender,
        RoutedEventArgs e)
    {
        if (Picked is null)
        {
            return;
        }

        var removed = Current.Select(z => z.Clone()).ToList();
        removed.RemoveAt(SelectedZone);
        SetCurrentValue(SelectedCornerProperty, -1);
        SetCurrentValue(SelectedZoneProperty, -1);
        SetCurrentValue(ZonesProperty, removed);
    }

    private void OnAddCorner(
        object sender,
        RoutedEventArgs e)
    {
        if (Picked is not { } zone || !MotionZoneEditing.CanAddCorner(zone.Points.Count))
        {
            return;
        }

        // Without a pointer the corner goes in the middle of the longest edge, where it is easiest to grab.
        var size = ZoneCanvas.PictureSize;
        var edge = MotionZoneEditing.LongestEdge(zone.Points, size.Width, size.Height);
        if (edge < 0)
        {
            return;
        }

        Change(SelectedZone, z => z.Points = [.. MotionZoneEditing.AddCorner(zone.Points, edge)]);
        SetCurrentValue(SelectedCornerProperty, edge + 1);
    }

    private void OnRemoveCorner(
        object sender,
        RoutedEventArgs e)
    {
        if (Picked is not { } zone ||
            SelectedCorner < 0 ||
            SelectedCorner >= zone.Points.Count ||
            !MotionZoneEditing.CanRemoveCorner(zone.Points.Count))
        {
            return;
        }

        var corner = SelectedCorner;
        SetCurrentValue(SelectedCornerProperty, -1);
        Change(SelectedZone, z => z.Points = [.. MotionZoneEditing.RemoveCorner(zone.Points, corner)]);
    }

    private static string NameFor(int number)
        => string.Format(CultureInfo.CurrentCulture, Translations.Zone1, number);

    private void Change(
        int index,
        Action<MotionZone> change)
    {
        var changed = Current.Select(z => z.Clone()).ToList();
        change(changed[index]);
        SetCurrentValue(ZonesProperty, changed);
    }

    private void Refresh()
    {
        var zone = Picked;
        AddZoneButton.IsEnabled = MotionZoneEditing.CanAddZone(Current.Count);
        RemoveZoneButton.IsEnabled = zone is not null;
        AddCornerButton.IsEnabled = zone is not null && MotionZoneEditing.CanAddCorner(zone.Points.Count);
        RemoveCornerButton.IsEnabled = zone is not null &&
                                       SelectedCorner >= 0 &&
                                       SelectedCorner < zone.Points.Count &&
                                       MotionZoneEditing.CanRemoveCorner(zone.Points.Count);
        NameBox.IsEnabled = zone is not null;
        KindBox.IsEnabled = zone is not null;
        var watched = zone?.Kind == MotionZoneKind.Watch ? Visibility.Visible : Visibility.Collapsed;
        SensitivityBox.Visibility = watched;
        SmallestObjectBox.Visibility = watched;
        ColorBox.Visibility = watched;

        showingName = true;
        try
        {
            var name = zone?.Name ?? string.Empty;
            if (!string.Equals(ZoneName ?? string.Empty, name, StringComparison.Ordinal))
            {
                SetCurrentValue(ZoneNameProperty, name);
            }

            var kind = zone?.Kind.ToString();
            if (!string.Equals(ZoneKindKey, kind, StringComparison.Ordinal))
            {
                SetCurrentValue(ZoneKindKeyProperty, kind);
            }

            if (!string.Equals(ZoneColorKey ?? string.Empty, zone?.Color ?? string.Empty, StringComparison.Ordinal))
            {
                SetCurrentValue(ZoneColorKeyProperty, zone?.Color);
            }

            // A field is filled from the zone only when it means something else, so "0.0" on the
            // way to "0.05" stays as typed.
            if (!MotionZoneEditing.TryReadSensitivity(ZoneSensitivityText, CultureInfo.CurrentCulture, out var sensitivity) ||
                sensitivity != zone?.Sensitivity)
            {
                SetCurrentValue(ZoneSensitivityTextProperty, zone?.Sensitivity?.ToString(CultureInfo.CurrentCulture) ?? string.Empty);
            }

            if (!MotionZoneEditing.TryReadSmallestObjectPercent(ZoneSmallestObjectText, CultureInfo.CurrentCulture, out var smallest) ||
                !EqualityComparer<double?>.Default.Equals(smallest, zone?.SmallestObjectPercent))
            {
                SetCurrentValue(ZoneSmallestObjectTextProperty, zone?.SmallestObjectPercent?.ToString(CultureInfo.CurrentCulture) ?? string.Empty);
            }
        }
        finally
        {
            showingName = false;
        }
    }
}