namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs.Parts.Settings;

/// <summary>
/// Logging settings (debug logging, what the app adds, log path).
/// </summary>
public partial class AdvancedLoggingSettings
{
    // What an app adds below the debug-logging switch; only CameraWall has diagnostics, and a
    // switch bound to a setting the other app does not have is a binding error there.
    [DependencyProperty]
    private object? extraContent;

    public AdvancedLoggingSettings()
    {
        InitializeComponent();
    }
}