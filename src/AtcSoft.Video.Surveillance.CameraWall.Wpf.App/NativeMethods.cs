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

    /// <summary>
    /// Ends this process at once, with an exit code (see CameraWallApp.ApplicationExit).
    /// </summary>
    /// <param name="process">The process: <see cref="GetCurrentProcess"/>.</param>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>True when it was ended (it does not return then).</returns>
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static extern bool TerminateProcess(
        nint process,
        uint exitCode);

    /// <summary>
    /// Gets a handle of this process (a pseudo handle, nothing to close).
    /// </summary>
    /// <returns>The handle.</returns>
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    internal static extern nint GetCurrentProcess();
}