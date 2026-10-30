namespace AtcSoft.VideoSurveillance.Resources;

/// <summary>
/// The repository's source files, read as text: the tests check what the
/// build cannot (a key named in XAML, a literal text in a view).
/// </summary>
internal static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    public static IReadOnlyList<string> Find(
        string pattern,
        params string[] underFolders)
        => [.. underFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(Root, folder), pattern, SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Order(StringComparer.Ordinal)];

    public static string Relative(string path)
        => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}obj{separator}", StringComparison.Ordinal) ||
               path.Contains($"{separator}bin{separator}", StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AtcSoft.VideoSurveillance.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root (AtcSoft.VideoSurveillance.slnx) was not found.");
    }
}