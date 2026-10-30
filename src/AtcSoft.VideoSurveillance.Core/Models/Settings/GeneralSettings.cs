namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// General application settings.
/// </summary>
public class GeneralSettings
{
    public string ThemeBase { get; set; } = "Dark";

    public string ThemeAccent { get; set; } = "Blue";

    public string Language { get; set; } = "1033";

    public bool ConnectCamerasOnStartup { get; set; } = true;

    public bool StartMaximized { get; set; }

    public bool StartRibbonCollapsed { get; set; }

    /// <summary>
    /// Gets or sets the size of the camera grid the desktop app was last
    /// showing: 0 for the automatic grid with every camera, 1 to 4 for a
    /// fixed grid with that many cameras along a side. Chosen in the main
    /// window, and a matter of each desktop: it is not part of the API.
    /// </summary>
    public int GridSize { get; set; }

    /// <inheritdoc />
    public override string ToString()
        => $"GeneralSettings {{ Theme='{ThemeBase}/{ThemeAccent}', Language='{Language}' }}";

    public GeneralSettings Clone()
        => new()
        {
            ThemeBase = ThemeBase,
            ThemeAccent = ThemeAccent,
            Language = Language,
            ConnectCamerasOnStartup = ConnectCamerasOnStartup,
            StartMaximized = StartMaximized,
            StartRibbonCollapsed = StartRibbonCollapsed,
            GridSize = GridSize,
        };
}