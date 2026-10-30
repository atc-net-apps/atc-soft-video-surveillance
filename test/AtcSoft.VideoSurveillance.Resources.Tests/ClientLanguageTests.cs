namespace AtcSoft.VideoSurveillance.Resources;

/// <summary>
/// The language belongs to each client, so no client sends it to the server.
/// The server's setting is [Obsolete], but a named constructor argument
/// (Language: ...) of a generated request does not raise CS0618, so the
/// sources are read instead.
/// </summary>
public sealed partial class ClientLanguageTests
{
    [Fact]
    public void No_Client_Sends_Its_Language_To_The_Server()
        => new[] { "src/AtcSoft.VideoSurveillance.Wpf", "src/AtcSoft.VideoSurveillance.Wpf.App", "src/AtcSoft.VideoSurveillance.Blazor.App" }
            .SelectMany(folder => RepositoryFiles.Find("*.cs", folder).Concat(RepositoryFiles.Find("*.razor", folder)))
            .Where(file => LanguageArgument().IsMatch(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative)
            .Should().BeEmpty("the language is the client's own; a request leaves it out");

    [GeneratedRegex(@"\bLanguage:\s", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LanguageArgument();
}