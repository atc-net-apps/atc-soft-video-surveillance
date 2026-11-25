namespace AtcSoft.Video.Surveillance.Resources;

/// <summary>
/// The WPF windows keep an overview: a dialog is no larger than its app's main window (one that
/// sizes itself to its content stops there too), and a group box never holds another group box,
/// also not through a part (a user control) it shows. Many group boxes go in tabs, as in Settings.
/// </summary>
public sealed class DialogLayoutTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string[] AppFolders = ["src", "tools"];

    [Fact]
    public void A_Group_Box_Holds_No_Group_Box()
    {
        var views = RepositoryFiles.Find("*.xaml", AppFolders)
            .Select(file => (File: file, Root: XDocument.Load(file).Root!))
            .ToList();
        var byClass = views
            .Where(view => view.Root.Attribute(Xaml + "Class") is not null)
            .GroupBy(view => ShortName(view.Root.Attribute(Xaml + "Class")!.Value), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var nested = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var view in views)
        {
            FindNested(view.Root, [], view.File, new HashSet<string>(StringComparer.Ordinal) { view.File }, byClass, nested);
        }

        nested.Should().BeEmpty("a group box never holds another group box; several group boxes go in tabs");
    }

    [Fact]
    public void A_Dialog_Is_No_Larger_Than_Its_Main_Window()
    {
        var tooLarge = new List<string>();
        foreach (var (folder, limit) in MainWindowLimits())
        {
            foreach (var file in RepositoryFiles.Find("*.xaml", folder))
            {
                var root = XDocument.Load(file).Root!;
                if (!IsWindow(root) || IsMainWindow(file) || IsFullScreen(root))
                {
                    continue;
                }

                tooLarge.AddRange(SizeProblems(root, limit).Select(problem => $"{RepositoryFiles.Relative(file)}: {problem}"));
            }
        }

        tooLarge.Should().BeEmpty("a dialog is no larger than its app's main window, also when it sizes itself to its content");
    }

    // The src apps share their dialogs (Wpf.Core): those stay within the smallest main window of them.
    // A tool's windows stay within the tool's own main window.
    private static IEnumerable<(string Folder, (double Width, double Height) Limit)> MainWindowLimits()
    {
        var appMains = RepositoryFiles.Find("MainWindow.xaml", "src").Select(SizeOf).ToList();
        appMains.Should().NotBeEmpty("the apps have main windows");
        yield return ("src", (appMains.Min(size => size.Width), appMains.Min(size => size.Height)));

        foreach (var toolMain in RepositoryFiles.Find("MainWindow.xaml", "tools"))
        {
            yield return (Path.GetRelativePath(RepositoryFiles.Root, Path.GetDirectoryName(toolMain)!), SizeOf(toolMain));
        }
    }

    private static IEnumerable<string> SizeProblems(
        XElement window,
        (double Width, double Height) limit)
    {
        var sizeToContent = window.Attribute("SizeToContent")?.Value ?? "Manual";
        foreach (var (axis, max) in new[] { ("Width", limit.Width), ("Height", limit.Height) })
        {
            foreach (var attribute in new[] { axis, "Min" + axis, "Max" + axis })
            {
                if (Number(window, attribute) is { } value && value > max)
                {
                    yield return $"{attribute} {value} is more than the main window's {max}";
                }
            }

            if (sizeToContent.Contains(axis, StringComparison.Ordinal) && Number(window, "Max" + axis) is null)
            {
                yield return $"sizes its {axis.ToLowerInvariant()} to its content without a Max{axis} (at most {max})";
            }
        }
    }

    private static void FindNested(
        XElement element,
        IReadOnlyList<string> boxes,
        string file,
        IReadOnlySet<string> followed,
        IReadOnlyDictionary<string, (string File, XElement Root)> byClass,
        ISet<string> nested)
    {
        if (IsGroupBox(element))
        {
            var header = HeaderOf(element);
            if (boxes.Count > 0)
            {
                nested.Add($"{RepositoryFiles.Relative(file)}: {string.Join(" > ", boxes)} > {header}");
            }

            boxes = [.. boxes, header];
        }

        // A part shown inside a group box: its own group boxes are inside it too.
        if (boxes.Count > 0 &&
            byClass.TryGetValue(element.Name.LocalName, out var part) &&
            !followed.Contains(part.File))
        {
            FindNested(part.Root, [.. boxes, $"[{element.Name.LocalName}]"], part.File, new HashSet<string>(followed, StringComparer.Ordinal) { part.File }, byClass, nested);
        }

        foreach (var child in element.Elements())
        {
            FindNested(child, boxes, file, followed, byClass, nested);
        }
    }

    private static bool IsGroupBox(XElement element)
        => element.Name.LocalName is "GroupBox" or "GroupBoxExpander";

    private static string HeaderOf(XElement element)
    {
        var header = element.Attribute("Header")?.Value ?? element.Name.LocalName;
        var key = header.IndexOf("Key=", StringComparison.Ordinal);
        return key < 0 ? header : header[(key + 4)..].TrimEnd('}', ' ');
    }

    private static bool IsWindow(XElement root)
        => root.Name.LocalName is "NiceWindow" or "Window" or "RibbonWindow";

    private static bool IsMainWindow(string file)
        => string.Equals(Path.GetFileName(file), "MainWindow.xaml", StringComparison.Ordinal);

    private static bool IsFullScreen(XElement root)
        => string.Equals(root.Attribute("WindowState")?.Value, "Maximized", StringComparison.Ordinal);

    private static (double Width, double Height) SizeOf(string file)
    {
        var root = XDocument.Load(file).Root!;
        return (Number(root, "Width") ?? double.MaxValue, Number(root, "Height") ?? double.MaxValue);
    }

    private static double? Number(
        XElement element,
        string attribute)
        => double.TryParse(element.Attribute(attribute)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string ShortName(string className)
        => className[(className.LastIndexOf(".", StringComparison.Ordinal) + 1)..];
}