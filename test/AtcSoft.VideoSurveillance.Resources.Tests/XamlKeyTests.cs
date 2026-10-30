namespace AtcSoft.VideoSurveillance.Resources;

public sealed partial class XamlKeyTests
{
    [Fact]
    public void Every_Key_A_View_Names_Exists()
    {
        var keys = ResxFile.Read(string.Empty);
        var wrong = new List<string>();

        foreach (var file in RepositoryFiles.Find("*.xaml", "src"))
        {
            foreach (Match match in ResxBinding().Matches(File.ReadAllText(file)))
            {
                var name = match.Groups["name"].Value;
                var key = match.Groups["key"].Value;
                if (name != "AtcSoft.VideoSurveillance.Resources.Translations" || !keys.ContainsKey(key))
                {
                    wrong.Add($"{RepositoryFiles.Relative(file)}: {name}, {key}");
                }
            }
        }

        wrong.Should().BeEmpty("every view binds to an existing key of the shared Translations");
    }

    // {atcTranslation:Resx ResxName=..., Key=...}, possibly over several lines.
    [GeneratedRegex(@"atcTranslation:Resx\s+ResxName=(?<name>[\w.]+),\s*Key=(?<key>\w+)", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ResxBinding();
}