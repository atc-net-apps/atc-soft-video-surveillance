namespace AtcSoft.VideoSurveillance.Resources;

/// <summary>
/// A text a person reads comes from the Translations. This finds the literal
/// texts left in the views of the API clients; the few that are allowed (a
/// product name, a symbol) are listed with the reason in literal-text-allowed.txt.
/// </summary>
public sealed partial class LiteralTextTests
{
    private static readonly HashSet<string> Allowed = [.. File
        .ReadAllLines(Path.Combine(AppContext.BaseDirectory, "literal-text-allowed.txt"))
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .Select(line => string.Join('|', line.Split('|').Take(2)))];

    [Theory]
    [InlineData("src/AtcSoft.VideoSurveillance.Wpf")]
    [InlineData("src/AtcSoft.VideoSurveillance.Wpf.App")]
    public void A_View_Has_No_Literal_Text(string folder)
    {
        var found = new List<string>();
        foreach (var file in RepositoryFiles.Find("*.xaml", folder))
        {
            foreach (Match match in XamlTextAttribute().Matches(File.ReadAllText(file)))
            {
                var entry = $"{RepositoryFiles.Relative(file)}|{match.Groups["text"].Value}";
                if (!Allowed.Contains(entry))
                {
                    found.Add(entry);
                }
            }
        }

        found.Should().BeEmpty("a visible text is a key in AtcSoft.VideoSurveillance.Resources");
    }

    [Fact]
    public void A_Page_Has_No_Literal_Text()
    {
        var found = new List<string>();
        foreach (var file in RepositoryFiles.Find("*.razor", "src/AtcSoft.VideoSurveillance.Blazor.App"))
        {
            // A lambda's => is no tag end.
            var razor = StyleBlock()
                .Replace(RazorComment().Replace(CodeBlock().Replace(File.ReadAllText(file), string.Empty), string.Empty), string.Empty)
                .Replace("=>", "=:", StringComparison.Ordinal);
            var texts = RazorElementText().Matches(razor)
                .Select(match => RazorExpression().Replace(match.Groups["text"].Value, " ").Trim())
                .Where(text => Words().IsMatch(text) && !IsCode(text))
                .Concat(RazorTextAttribute().Matches(razor).Select(match => match.Groups["text"].Value));
            foreach (var text in texts)
            {
                var entry = $"{RepositoryFiles.Relative(file)}|{text}";
                if (!Allowed.Contains(entry))
                {
                    found.Add(entry);
                }
            }
        }

        found.Should().BeEmpty("a visible text is a key in AtcSoft.VideoSurveillance.Resources");
    }

    [Fact]
    public void A_Page_Has_No_Literal_Text_In_An_Expression()
        => RepositoryFiles.Find("*.razor", "src/AtcSoft.VideoSurveillance.Blazor.App")
            .SelectMany(file => ExpressionLiteral().Matches(File.ReadAllText(file))
                .Select(match => $"{RepositoryFiles.Relative(file)}|{match.Groups["text"].Value}"))
            .Where(entry => !Allowed.Contains(entry))
            .Should().BeEmpty("a text inside @( ) is a key in AtcSoft.VideoSurveillance.Resources too");

    // A C# statement in the markup (var x = ...;) or the rest of a call's arguments over several lines.
    private static bool IsCode(string text)
        => text.EndsWith(';') || text.StartsWith(',');

    // A visible-text attribute whose value is not a markup extension and has a letter in it.
    [GeneratedRegex(@"\b(?:Text|Header|Title|Content|LabelText|ToolTip|WatermarkText|Description|InformationText|Message|Caption)=""(?<text>(?!\{)[^""]*\p{L}[^""]*)""", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex XamlTextAttribute();

    // The @code { ... } block is C#, not markup.
    [GeneratedRegex(@"@code\s*\{.*", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CodeBlock();

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RazorComment();

    // CSS, not text.
    [GeneratedRegex(@"<style>.*?</style>", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StyleBlock();

    // The text between two tags.
    [GeneratedRegex(@">(?<text>[^<>]*)<", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RazorElementText();

    // @if / @foreach lines, else, @(...), @Name.Member(...), entities and braces: code, not text.
    [GeneratedRegex(@"@(?:if|foreach|for|switch|while)\b[^\n]*|\belse\b(?:\s+if\b[^\n]*)?|@\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)|@\w+(?:\.\w+|\([^)]*\)|\[[^\]]*\])*|&#?\w+;|[{}]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RazorExpression();

    [GeneratedRegex(@"\p{L}{2,}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Words();

    // A capitalised word in quotes inside @( ... ): "Edit Camera" in @(IsEdit ? "Edit Camera" : ...).
    [GeneratedRegex(@"@\((?:[^()""]|""[^""]*""|\([^()]*\))*?""(?<text>[A-Z][a-z]+(?: [A-Za-z]+)*)""", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExpressionLiteral();

    // A visible-text parameter with a literal value.
    [GeneratedRegex(@"\b(?:Label|Text|Title|Placeholder|HelperText|AriaLabel|Tooltip|ButtonText|ConfirmText|CancelText|Message|Caption|Help|RequiredError|ErrorText|DataLabel|aria-label|alt)=""(?<text>(?!@)[^""]*\p{L}[^""]*)""", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RazorTextAttribute();
}