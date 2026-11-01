namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.App;

/// <summary>
/// Windows calls of the app itself.
/// </summary>
internal static class NativeMethods
{
    /// <summary>
    /// Asks Windows to start the app again when it crashes or stops answering (after it has run for
    /// at least a minute), so the cameras that record on connect record again.
    /// </summary>
    /// <param name="commandLine">The arguments to start it with, or <see langword="null"/>.</param>
    /// <param name="flags">Cases left out; 0 for none.</param>
    /// <returns>0 (S_OK) when registered.</returns>
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    internal static extern int RegisterApplicationRestart(
        string? commandLine,
        int flags);
}