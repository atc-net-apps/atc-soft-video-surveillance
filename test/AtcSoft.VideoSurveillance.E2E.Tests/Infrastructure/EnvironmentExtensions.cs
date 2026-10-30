namespace AtcSoft.VideoSurveillance.E2E.Infrastructure;

/// <summary>
/// Merges a run's own environment variables with the ones a test adds.
/// </summary>
internal static class EnvironmentExtensions
{
    public static Dictionary<string, string> With(
        this Dictionary<string, string> environment,
        IReadOnlyDictionary<string, string>? more)
    {
        ArgumentNullException.ThrowIfNull(environment);

        foreach (var (name, value) in more ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            environment[name] = value;
        }

        return environment;
    }
}