namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// A WPF app under test, driven through UI Automation patterns (no keystrokes): find by the
/// en-US text, invoke, select tabs, close windows. Every wait has a deadline.
/// </summary>
internal sealed class DesktopApp : IDisposable
{
    public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan ElementTimeout = TimeSpan.FromSeconds(10);

    private readonly Application application;
    private readonly string dumpFolder;
    private int dumpCount;

    public DesktopApp(
        ChildProcess process,
        string dumpFolder)
    {
        Process = process;
        this.dumpFolder = dumpFolder;
        Automation = new UIA3Automation();
        application = Application.Attach(process.Process.Id);
    }

    public ChildProcess Process { get; }

    public UIA3Automation Automation { get; }

    public ConditionFactory Conditions
        => Automation.ConditionFactory;

    /// <summary>
    /// Gets the app's windows: the top-level ones and every window they own. UI Automation
    /// shows an owned (modal) dialog as a child of its owner, not of the desktop.
    /// </summary>
    public IReadOnlyList<Window> Windows()
    {
        var windows = new List<Window>();
        try
        {
            foreach (var window in application.GetAllTopLevelWindows(Automation))
            {
                // A context menu or a drop-down is a pop-up window, not a dialog.
                if (!string.Equals(window.Properties.ClassName.ValueOrDefault, "Popup", StringComparison.Ordinal))
                {
                    AddWithOwned(window, windows, depth: 0);
                }
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or TimeoutException or InvalidOperationException)
        {
            // The app is between windows.
        }

        return windows;
    }

    public Window WaitForWindow(
        Func<Window, bool> match,
        TimeSpan timeout,
        string what)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Process.HasExited)
            {
                throw new InvalidOperationException($"The app exited (code {Process.Process.ExitCode}) while waiting for {what}. Output: {Process.OutputPath}");
            }

            var window = Windows().FirstOrDefault(w => SafeMatch(match, w));
            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(250);
        }

        throw Fail($"No window {what} within {timeout.TotalSeconds:N0} s.");
    }

    public Window WaitForWindowTitled(
        string titlePart,
        TimeSpan? timeout = null)
        => WaitForWindow(
            w => w.Title.Contains(titlePart, StringComparison.OrdinalIgnoreCase),
            timeout ?? WindowTimeout,
            $"titled '{titlePart}'");

    /// <summary>
    /// Invokes what <paramref name="open"/> finds and waits for the new window it opens.
    /// </summary>
    public Window OpenWindow(
        Action open,
        string what)
    {
        var before = Windows().Select(Handle).ToHashSet();
        open();
        return WaitForWindow(w => !before.Contains(Handle(w)), WindowTimeout, $"opened by {what}");
    }

    public AutomationElement Find(
        AutomationElement root,
        string name,
        ControlType? controlType = null,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? ElementTimeout);
        while (DateTime.UtcNow < deadline)
        {
            var element = TryFind(root, name, controlType);
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(200);
        }

        throw Fail($"No {controlType?.ToString() ?? "element"} named '{name}' in '{SafeName(root)}'.");
    }

    /// <summary>
    /// Waits until no element with the name is left under the root.
    /// </summary>
    /// <returns>Whether it went within the time.</returns>
    public bool WaitUntilGone(
        AutomationElement root,
        string name,
        ControlType? controlType,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (TryFind(root, name, controlType) is not null)
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(200);
        }

        return true;
    }

    public AutomationElement? TryFind(
        AutomationElement root,
        string name,
        ControlType? controlType = null)
    {
        ConditionBase condition = Conditions.ByName(name);
        if (controlType is { } type)
        {
            condition = condition.And(Conditions.ByControlType(type));
        }

        try
        {
            return root.FindFirstDescendant(condition);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    /// <summary>
    /// Finds the first element below the root that the predicate accepts, waiting for it.
    /// </summary>
    public AutomationElement FindWhere(
        AutomationElement root,
        Func<AutomationElement, bool> predicate,
        string what,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? ElementTimeout);
        while (DateTime.UtcNow < deadline)
        {
            var found = SafeFindAll(root, TrueCondition.Default)
                .FirstOrDefault(e => SafeMatch(predicate, e));
            if (found is not null)
            {
                return found;
            }

            Thread.Sleep(250);
        }

        throw Fail($"Nothing {what} in '{SafeName(root)}'.");
    }

    /// <summary>
    /// Opens the ribbon's backstage (File) and presses one of its items.
    /// </summary>
    public void PressBackstageItem(
        Window main,
        string itemName)
    {
        var backstage = FindWhere(
            main,
            e => string.Equals(e.Properties.AutomationId.ValueOrDefault, "Backstage", StringComparison.Ordinal),
            "with the automation id Backstage");

        var item = TryFindVisible(main, itemName);
        if (item is null)
        {
            Press(backstage);
            item = FindWhere(
                main,
                e => IsNamed(e, itemName) && !e.IsOffscreen && e.Properties.ControlType.ValueOrDefault is ControlType.Button or ControlType.TabItem or ControlType.ListItem or ControlType.MenuItem,
                $"named '{itemName}' in the backstage");
        }

        Press(item);
    }

    /// <summary>
    /// Opens an element's context menu (right click) and presses the item with the name. An
    /// item that is disabled (a command that waits for a state) is retried until the deadline.
    /// </summary>
    public void PressContextMenuItem(
        AutomationElement target,
        string itemName,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        var sawDisabled = false;
        var lastMenu = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            target.RightClick();
            var item = WaitForMenuItem(itemName, TimeSpan.FromSeconds(3), out lastMenu);
            if (item is not null && item.IsEnabled)
            {
                Press(item);
                return;
            }

            sawDisabled |= item is not null;

            // Close the menu again and give the app a moment.
            target.Click();
            Thread.Sleep(1000);
        }

        throw Fail(sawDisabled
            ? $"The menu item '{itemName}' stayed disabled on '{SafeName(target)}' at {target.BoundingRectangle}. Menu: {lastMenu}."
            : $"No menu item '{itemName}' after a right click on '{SafeName(target)}'.");
    }

    private AutomationElement? WaitForMenuItem(
        string itemName,
        TimeSpan timeout,
        out string menu)
    {
        menu = string.Empty;

        // A context menu is a pop-up window of its own, a child of the desktop.
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var popup in SafeFindAll(Automation.GetDesktop(), Conditions.ByProcessId(Process.Process.Id), TreeScope.Children))
            {
                var items = SafeFindAll(popup, Conditions.ByControlType(ControlType.MenuItem));
                menu = string.Join(", ", items.Select(e => $"{SafeName(e)}={(e.IsEnabled ? "on" : "off")}"));
                var item = items.FirstOrDefault(e => IsNamed(e, itemName));
                if (item is not null)
                {
                    return item;
                }
            }

            Thread.Sleep(250);
        }

        return null;
    }

    /// <summary>
    /// Walks a dialog: every tab, every drop-down, then closes it.
    /// </summary>
    public void WalkAndClose(Window dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        Settle(TimeSpan.FromMilliseconds(800));
        SelectEveryTab(dialog);
        OpenEveryDropDown(dialog);
        CloseWindow(dialog);
    }

    private static bool IsNamed(
        AutomationElement element,
        string name)
        => string.Equals(element.Properties.Name.ValueOrDefault?.Trim(), name, StringComparison.OrdinalIgnoreCase);

    private AutomationElement? TryFindVisible(
        AutomationElement root,
        string name)
        => SafeFindAll(root, Conditions.ByName(name))
            .FirstOrDefault(e => !e.IsOffscreen && e.IsEnabled);

    /// <summary>
    /// Presses a button, a ribbon button, a menu item or a hyperlink the way UI Automation
    /// does: Invoke, else Toggle, else Select, else Expand.
    /// </summary>
    public static void Press(AutomationElement element)
    {
        var patterns = element.Patterns;
        if (patterns.Invoke.IsSupported)
        {
            patterns.Invoke.Pattern.Invoke();
        }
        else if (patterns.Toggle.IsSupported)
        {
            patterns.Toggle.Pattern.Toggle();
        }
        else if (patterns.SelectionItem.IsSupported)
        {
            patterns.SelectionItem.Pattern.Select();
        }
        else if (patterns.ExpandCollapse.IsSupported)
        {
            patterns.ExpandCollapse.Pattern.Expand();
        }
        else
        {
            element.Click();
        }
    }

    /// <summary>
    /// Selects every tab in the window, nested tabs included, so each tab's content is
    /// created and bound (binding errors show up when a template is used).
    /// </summary>
    /// <returns>The names of the tabs selected.</returns>
    public IReadOnlyList<string> SelectEveryTab(AutomationElement root)
    {
        var visited = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var round = 0; round < 6; round++)
        {
            var tabs = SafeFindAll(root, Conditions.ByControlType(ControlType.TabItem));
            var selectedAny = false;
            foreach (var tab in tabs)
            {
                var key = SafeName(tab) + "@" + tab.Properties.BoundingRectangle.ValueOrDefault.ToString();
                if (!seen.Add(key))
                {
                    continue;
                }

                try
                {
                    if (tab.Patterns.SelectionItem.IsSupported)
                    {
                        tab.Patterns.SelectionItem.Pattern.Select();
                        visited.Add(SafeName(tab));
                        selectedAny = true;
                        Thread.Sleep(400);
                    }
                }
                catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
                {
                    // A tab that went away when another was selected.
                }
            }

            if (!selectedAny)
            {
                break;
            }
        }

        return visited;
    }

    /// <summary>
    /// Opens and closes every drop-down in the window, so their item templates are used.
    /// </summary>
    public void OpenEveryDropDown(AutomationElement root)
    {
        foreach (var combo in SafeFindAll(root, Conditions.ByControlType(ControlType.ComboBox)))
        {
            try
            {
                if (!combo.IsEnabled || !combo.Patterns.ExpandCollapse.IsSupported || combo.IsOffscreen)
                {
                    continue;
                }

                combo.Patterns.ExpandCollapse.Pattern.Expand();
                Thread.Sleep(250);
                combo.Patterns.ExpandCollapse.Pattern.Collapse();
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
            {
                // A drop-down that cannot open right now.
            }
        }
    }

    /// <summary>
    /// Closes a dialog with its Cancel / Close / OK button, else through the window itself,
    /// and waits until it is gone.
    /// </summary>
    public void CloseWindow(Window window)
    {
        var handle = Handle(window);
        foreach (var name in new[] { "Cancel", "Close", "OK", "No" })
        {
            var button = TryFind(window, name, ControlType.Button);
            if (button is not null && button.IsEnabled && !button.IsOffscreen)
            {
                try
                {
                    Press(button);
                }
                catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
                {
                    continue;
                }

                if (WaitGone(handle, TimeSpan.FromSeconds(5)))
                {
                    return;
                }
            }
        }

        try
        {
            window.Close();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // Closing as it closes.
        }

        if (!WaitGone(handle, TimeSpan.FromSeconds(10)))
        {
            throw Fail($"The window '{SafeName(window)}' did not close.");
        }
    }

    /// <summary>
    /// Checks that a full-screen window fills the screen: it covers the taskbar, not just the
    /// space beside it. Passes when there is no taskbar to find.
    /// </summary>
    public void ShouldCoverTheTaskbar(Window window)
    {
        var desktop = Automation.GetDesktop();
        if (desktop.FindFirstChild(Conditions.ByClassName("Shell_TrayWnd")) is not { } taskbar)
        {
            return;
        }

        // An auto-hidden taskbar sits partly off the screen: only its part on the screen counts.
        var visible = System.Drawing.Rectangle.Intersect(taskbar.BoundingRectangle, desktop.BoundingRectangle);
        if (!window.BoundingRectangle.Contains(visible))
        {
            throw Fail($"The window '{SafeName(window)}' at {window.BoundingRectangle} does not cover the taskbar at {visible}.");
        }
    }

    /// <summary>
    /// Writes every window's UI Automation tree to a file in the run folder, for a failure
    /// and for writing new steps.
    /// </summary>
    /// <returns>The file written.</returns>
    public string DumpTree(string label)
    {
        Directory.CreateDirectory(dumpFolder);
        var path = Path.Combine(
            dumpFolder,
            $"{Interlocked.Increment(ref dumpCount):00}-{string.Concat(label.Where(char.IsLetterOrDigit))}.txt");
        var text = new StringBuilder();
        foreach (var window in Windows())
        {
            Dump(window, 0, text);
        }

        File.WriteAllText(path, text.ToString());
        return path;
    }

    public Exception Fail(string message)
    {
        string dump;
        try
        {
            dump = DumpTree("failure");
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or InvalidOperationException)
        {
            dump = "(no dump: " + ex.Message + ")";
        }

        return new InvalidOperationException($"{message} UI tree: {dump}");
    }

    /// <summary>
    /// Gives the app time to finish what a step started (load a view, connect a camera).
    /// </summary>
    public static void Settle(TimeSpan duration)
        => Thread.Sleep(duration);

    /// <summary>
    /// Asks the app to close its main window and waits for the process to end.
    /// </summary>
    /// <returns><see langword="true"/> when it ended by itself.</returns>
    public bool CloseApp(Window mainWindow)
    {
        try
        {
            mainWindow.Close();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // Closing as it closes.
        }

        return Process.Process.WaitForExit(30_000);
    }

    public void Dispose()
    {
        Automation.Dispose();
        application.Dispose();
    }

    private void AddWithOwned(
        Window window,
        List<Window> windows,
        int depth)
    {
        windows.Add(window);
        if (depth > 5)
        {
            return;
        }

        foreach (var child in SafeFindAll(window, Conditions.ByControlType(ControlType.Window), TreeScope.Children))
        {
            AddWithOwned(child.AsWindow(), windows, depth + 1);
        }
    }

    private static IntPtr Handle(Window window)
    {
        try
        {
            return window.Properties.NativeWindowHandle.ValueOrDefault;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return IntPtr.Zero;
        }
    }

    private static string SafeName(AutomationElement element)
    {
        try
        {
            return element.Properties.Name.ValueOrDefault ?? string.Empty;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return string.Empty;
        }
    }

    private static bool SafeMatch<T>(
        Func<T, bool> match,
        T window)
    {
        try
        {
            return match(window);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            return false;
        }
    }

    private static AutomationElement[] SafeFindAll(
        AutomationElement root,
        ConditionBase condition,
        TreeScope scope = TreeScope.Descendants)
    {
        try
        {
            return scope == TreeScope.Children ? root.FindAllChildren(condition) : root.FindAllDescendants(condition);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return [];
        }
    }

    private static void Dump(
        AutomationElement element,
        int depth,
        StringBuilder text)
    {
        if (depth > 40)
        {
            return;
        }

        try
        {
            var properties = element.Properties;
            text.Append(' ', depth * 2)
                .Append(properties.ControlType.ValueOrDefault)
                .Append(" name='").Append(properties.Name.ValueOrDefault)
                .Append("' id='").Append(properties.AutomationId.ValueOrDefault)
                .Append("' class='").Append(properties.ClassName.ValueOrDefault)
                .Append("' enabled=").Append(properties.IsEnabled.ValueOrDefault)
                .Append(" offscreen=").Append(properties.IsOffscreen.ValueOrDefault)
                .AppendLine();

            foreach (var child in element.FindAllChildren())
            {
                Dump(child, depth + 1, text);
            }
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            text.Append(' ', depth * 2).AppendLine("(gone)");
        }
    }

    private bool WaitGone(
        IntPtr handle,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (!Windows().Any(w => Handle(w) == handle))
            {
                return true;
            }

            Thread.Sleep(200);
        }

        return false;
    }
}