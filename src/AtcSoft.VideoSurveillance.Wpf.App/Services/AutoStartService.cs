namespace AtcSoft.VideoSurveillance.Wpf.App.Services;

/// <summary>
/// Windows-registry implementation of <see cref="IAutoStartService"/>.
/// Writes to <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> so
/// the app starts at user login. Per-user (HKCU) rather than per-machine
/// (HKLM) because HKLM requires elevation and we ship without an
/// installer-mediated UAC prompt for this toggle.
/// </summary>
public sealed partial class AutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AtcSoft.VideoSurveillance.Wpf.App";

    /// <summary>
    /// The Run value name used before the Linksoft to AtcSoft rename. Removed
    /// on sight so an upgraded install does not keep a startup entry pointing
    /// at an executable that no longer exists.
    /// </summary>
    private const string LegacyValueName = "Linksoft.VideoSurveillance.Wpf.App";

    private readonly ILogger<AutoStartService> logger;

    public AutoStartService(ILogger<AutoStartService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this.logger = logger;
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) is not null;
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                LogAutoStartReadFailed(ex);
                return false;
            }
        }
    }

    public void Enable()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            LogAutoStartExecutablePathUnavailable();
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);

            // Quote the path so spaces in the install location don't split
            // the command line.
            key.SetValue(ValueName, $"\"{exePath}\"");
            LogAutoStartEnabled(exePath);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            LogAutoStartEnableFailed(ex);
        }
    }

    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            LogAutoStartDisabled();
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            LogAutoStartDisableFailed(ex);
        }
    }
}