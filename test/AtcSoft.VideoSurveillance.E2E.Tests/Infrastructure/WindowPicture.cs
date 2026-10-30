namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// Saves a picture of one window, to look at after a run: Windows draws that window alone
/// (<c>PrintWindow</c>), so the picture never holds the rest of the desktop (other apps' windows
/// are not the test's to keep) and does not depend on the window being in front.
/// </summary>
internal static class WindowPicture
{
    // Draw the window's content as the compositor shows it, also WPF's.
    private const uint RenderFullContent = 2;

    // Window sizes in the screen's real pixels, whatever this process's DPI awareness.
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    /// <summary>
    /// Saves a picture of a window.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="path">The PNG file.</param>
    public static void Save(
        AutomationElement window,
        string path)
    {
        ArgumentNullException.ThrowIfNull(window);

        DesktopApp.Settle(TimeSpan.FromMilliseconds(500));
        var handle = window.Properties.NativeWindowHandle.ValueOrDefault;
        var previous = NativeMethods.SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            if (handle == IntPtr.Zero || !NativeMethods.GetWindowRect(handle, out var bounds))
            {
                return;
            }

            using var picture = new System.Drawing.Bitmap(Math.Max(1, bounds.Right - bounds.Left), Math.Max(1, bounds.Bottom - bounds.Top));
            using (var graphics = System.Drawing.Graphics.FromImage(picture))
            {
                var dc = graphics.GetHdc();
                try
                {
                    NativeMethods.PrintWindow(handle, dc, RenderFullContent);
                }
                finally
                {
                    graphics.ReleaseHdc(dc);
                }
            }

            picture.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        finally
        {
            NativeMethods.SetThreadDpiAwarenessContext(previous);
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool PrintWindow(
            IntPtr hwnd,
            IntPtr hdc,
            uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool GetWindowRect(
            IntPtr hwnd,
            out Rect rect);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
    }
}