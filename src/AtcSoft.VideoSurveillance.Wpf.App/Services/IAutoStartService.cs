namespace AtcSoft.VideoSurveillance.Wpf.App.Services;

/// <summary>
/// Manages the Windows per-user auto-start registration for this app.
/// Reads / writes the <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>
/// value; per-user so installation does not require elevation.
/// </summary>
public interface IAutoStartService
{
    /// <summary>
    /// <see langword="true"/> when the Run-key value for this app is set.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Writes the Run-key value pointing at the current executable.
    /// Silently logs and returns on failure (registry I/O can fail under
    /// constrained Windows-policy environments — never a fatal app error).
    /// </summary>
    void Enable();

    /// <summary>
    /// Removes the Run-key value if present. No-op when already absent.
    /// </summary>
    void Disable();
}