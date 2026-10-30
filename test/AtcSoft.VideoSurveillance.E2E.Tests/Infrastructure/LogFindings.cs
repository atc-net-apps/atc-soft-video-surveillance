namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// What a smoke test fails on in an app's log: a binding error, an unhandled exception or a
/// fatal entry. Errors a camera that is offline causes on purpose are expected and kept apart.
/// </summary>
internal static class LogFindings
{
    private static readonly string[] FailureMarkers =
    [
        "Binding error",
        "Unhandled Exception",
        "UnhandledException",
        "[FTL]",
        " FTL]",
    ];

    /// <summary>
    /// Reads every *.log file below the folder (the apps roll their logs per day).
    /// </summary>
    public static IReadOnlyList<string> ReadLogLines(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var lines = new List<string>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.log", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    /// <summary>
    /// Gets the lines a smoke test fails on.
    /// </summary>
    public static IReadOnlyList<string> Failures(IEnumerable<string> lines)
        => lines
            .Where(line => FailureMarkers.Any(marker => line.Contains(marker, StringComparison.Ordinal)))
            .ToList();

    /// <summary>
    /// Fails with every binding error and unhandled exception in the logs below the folder.
    /// </summary>
    public static void AssertClean(
        string logFolder,
        string appName)
    {
        var lines = ReadLogLines(logFolder);
        lines.Should().NotBeEmpty($"{appName} writes its log below {logFolder}, or the test would see nothing");

        var failures = Failures(lines);
        failures.Should().BeEmpty(
            $"{appName} must open its windows without binding errors or unhandled exceptions (log: {logFolder})");
    }
}