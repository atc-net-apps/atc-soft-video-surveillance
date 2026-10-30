namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Provides the application version as a 3-part SemVer string.
/// </summary>
public static class ApplicationHelper
{
    /// <summary>
    /// Gets the application version as a 3-part SemVer string (e.g., "1.0.6"),
    /// matching the value declared in version.json and GitHub release tags.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="Atc.Helpers.AssemblyHelper.GetSystemVersion()"/>, which reads the
    /// assembly file version. Nerdbank.GitVersioning stamps a 4-part value (e.g., "1.0.6.7"
    /// where ".7" is the build height); <see cref="Version.ToString(int)"/> clips it to 3 parts
    /// so the user-facing version matches the published SemVer.
    /// </remarks>
    /// <returns>The version string (e.g., "1.0.6").</returns>
    public static string GetVersion()
        => Atc.Helpers.AssemblyHelper.GetSystemVersion().ToString(3);
}