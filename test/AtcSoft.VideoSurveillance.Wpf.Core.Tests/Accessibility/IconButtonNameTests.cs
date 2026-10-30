namespace AtcSoft.VideoSurveillance.Wpf.Core.Accessibility;

/// <summary>
/// A button that shows only an icon says nothing to a screen reader unless
/// it is given a name. The tool tip is not read as one. This looks at every
/// XAML file of the desktop apps.
/// </summary>
public sealed partial class IconButtonNameTests
{
    [Fact]
    public void Every_Button_Without_A_Text_Has_A_Name_For_Screen_Readers()
    {
        var source = FindSourceFolder();
        var nameless = new List<string>();

        foreach (var file in System.IO.Directory.EnumerateFiles(source, "*.xaml", System.IO.SearchOption.AllDirectories))
        {
            if (file.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var xaml = System.IO.File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match button in ButtonTag().Matches(xaml))
            {
                var attributes = button.Groups["attributes"].Value;
                var hasText = attributes.Contains("Content=", StringComparison.Ordinal);
                var hasName = attributes.Contains("AutomationProperties.Name=", StringComparison.Ordinal);
                var isIconOnly = !hasText && attributes.Contains("ToolTip=", StringComparison.Ordinal);

                if (isIconOnly && !hasName)
                {
                    var line = xaml[..button.Index].Count(c => c == '\n') + 1;
                    nameless.Add($"{System.IO.Path.GetRelativePath(source, file)}:{line}");
                }
            }
        }

        nameless.Should().BeEmpty("each of these buttons shows only an icon; give it AutomationProperties.Name");
    }

    // The opening tag of a plain button, with its attributes.
    [System.Text.RegularExpressions.GeneratedRegex(@"<Button\b(?<attributes>[^>]*)>", System.Text.RegularExpressions.RegexOptions.Singleline, matchTimeoutMilliseconds: 5000)]
    private static partial System.Text.RegularExpressions.Regex ButtonTag();

    private static string FindSourceFolder()
    {
        var folder = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !System.IO.File.Exists(System.IO.Path.Combine(folder.FullName, "AtcSoft.VideoSurveillance.slnx")))
        {
            folder = folder.Parent;
        }

        folder.Should().NotBeNull("the test runs from below the repository");
        return System.IO.Path.Combine(folder!.FullName, "src");
    }
}