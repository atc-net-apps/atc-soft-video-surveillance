namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Per-user-machine preferences for the WPF API client. Persisted to
/// <c>%LocalAppData%\AtcSoft\VideoSurveillance.Client\client-prefs.json</c>
/// — only the handful of fields that genuinely belong to *this* Windows
/// user on *this* machine and would be wrong to share across every WPF
/// client connected to a given API server. Everything else (cameras,
/// recording paths, motion detection, etc.) lives on the API server and
/// is fetched / mutated through <see cref="GatewayService"/>.
/// </summary>
public sealed class ClientPreferences
{
    public string ThemeBase { get; set; } = "Dark";

    public string ThemeAccent { get; set; } = "Blue";

    public string Language { get; set; } = "1033";

    public bool StartMaximized { get; set; }

    public bool StartRibbonCollapsed { get; set; }

    /// <summary>
    /// Gets or sets the size of the live view's camera grid: 0 for the
    /// automatic grid, 1 to 4 for that many cameras along a side.
    /// </summary>
    public int GridSize { get; set; }

    /// <summary>
    /// Takes from the general settings what is kept on this computer.
    /// </summary>
    public static ClientPreferences From(
        AtcSoft.VideoSurveillance.Models.Settings.GeneralSettings general)
    {
        ArgumentNullException.ThrowIfNull(general);

        return new ClientPreferences
        {
            ThemeBase = general.ThemeBase,
            ThemeAccent = general.ThemeAccent,
            Language = general.Language,
            StartMaximized = general.StartMaximized,
            StartRibbonCollapsed = general.StartRibbonCollapsed,
            GridSize = general.GridSize,
        };
    }
}