namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// The child windows of a window, from Windows itself. VideoHost puts its picture and its overlay
/// (the boxes) in child windows of their own, which WPF does not know of, so they are not in the
/// window's UI Automation tree; asked by its handle, UI Automation reads an overlay all the same.
/// </summary>
internal static class ChildWindows
{
    /// <summary>
    /// Gets the handles of every window below a window, at any depth.
    /// </summary>
    /// <param name="parent">The window's handle.</param>
    /// <returns>The handles.</returns>
    public static IReadOnlyList<IntPtr> Of(IntPtr parent)
    {
        var children = new List<IntPtr>();
        NativeMethods.EnumChildWindows(
            parent,
            (child, _) =>
            {
                children.Add(child);
                return true;
            },
            IntPtr.Zero);
        return children;
    }

    /// <summary>
    /// Finds an element by its automation id in a window or in any of its child windows.
    /// </summary>
    /// <param name="app">The app.</param>
    /// <param name="window">The window.</param>
    /// <param name="automationId">The automation id.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    public static AutomationElement? Find(
        DesktopApp app,
        AutomationElement window,
        string automationId)
    {
        if (window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) is { } inTree)
        {
            return inTree;
        }

        var handle = window.Properties.NativeWindowHandle.ValueOrDefault;
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        foreach (var child in Of(handle))
        {
            var element = app.Automation.FromHandle(child);
            if (element.Properties.AutomationId.ValueOrDefault == automationId)
            {
                return element;
            }

            if (element.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static class NativeMethods
    {
        public delegate bool EnumWindowsProc(
            IntPtr hwnd,
            IntPtr parameter);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(
            IntPtr parent,
            EnumWindowsProc callback,
            IntPtr parameter);
    }
}