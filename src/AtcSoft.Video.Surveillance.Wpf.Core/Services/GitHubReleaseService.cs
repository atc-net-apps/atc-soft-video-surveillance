// ReSharper disable RedundantArgumentDefaultValue
namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

/// <summary>
/// Service for checking GitHub releases for the AtcSoft.Video.Surveillance repository.
/// </summary>
[Registration(Lifetime.Singleton)]
public sealed class GitHubReleaseService : IGitHubReleaseService, IDisposable
{
    private const string GitHubApiUrl = "https://api.github.com/repos/atc-net-apps/atc-soft-video-surveillance/releases/latest";
    private const string UserAgent = "AtcSoft-VideoSurveillance";
    private const string AppSuffix = ".Wpf.App";
    private static readonly TimeSpan LockAcquisitionTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HttpRequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient httpClient;
    private readonly string? installerAssetName;
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private string? cachedResponse;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubReleaseService"/> class.
    /// </summary>
    public GitHubReleaseService()
    {
        httpClient = new HttpClient
        {
            Timeout = HttpRequestTimeout,
        };
        httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        installerAssetName = GetInstallerAssetName(System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name);
    }

    /// <summary>
    /// Gets the name of the installer a desktop app is updated with. Every release carries one
    /// installer per app, named after the app: <c>X.Wpf.App</c> is installed by <c>X.Installer.msi</c>.
    /// </summary>
    /// <param name="appAssemblyName">The app's assembly name, e.g. <c>AtcSoft.Video.Surveillance.Wpf.App</c>.</param>
    /// <returns>The installer's file name, or <see langword="null"/> for anything that is not a desktop app.</returns>
    public static string? GetInstallerAssetName(string? appAssemblyName)
        => appAssemblyName is not null && appAssemblyName.EndsWith(AppSuffix, StringComparison.Ordinal)
            ? string.Concat(appAssemblyName.AsSpan(0, appAssemblyName.Length - AppSuffix.Length), ".Installer.msi")
            : null;

    /// <inheritdoc />
    public async Task<Version?> GetLatestVersionAsync()
    {
        try
        {
            var response = await GetCachedResponseAsync().ConfigureAwait(false);
            if (response is null)
            {
                return null;
            }

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("tag_name", out var tagName))
            {
                var versionString = tagName.GetString();
                if (!string.IsNullOrEmpty(versionString))
                {
                    // Remove leading 'v' if present (e.g., "v1.0.0" -> "1.0.0")
                    if (versionString.StartsWith('v'))
                    {
                        versionString = versionString[1..];
                    }

                    if (Version.TryParse(versionString, out var version))
                    {
                        return version;
                    }
                }
            }
        }
        catch
        {
            // Silently fail - update check is not critical
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<Uri?> GetLatestReleaseUrlAsync()
    {
        try
        {
            var response = await GetCachedResponseAsync().ConfigureAwait(false);
            if (response is null)
            {
                return null;
            }

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("html_url", out var htmlUrl))
            {
                var urlString = htmlUrl.GetString();
                if (!string.IsNullOrEmpty(urlString))
                {
                    return new Uri(urlString);
                }
            }
        }
        catch
        {
            // Silently fail - update check is not critical
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<Uri?> GetLatestMsiDownloadUrlAsync()
    {
        try
        {
            var response = await GetCachedResponseAsync().ConfigureAwait(false);
            if (response is null)
            {
                return null;
            }

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("assets", out var assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var name) &&
                        name.GetString() is { } assetName &&
                        string.Equals(assetName, installerAssetName, StringComparison.OrdinalIgnoreCase) &&
                        asset.TryGetProperty("browser_download_url", out var downloadUrl))
                    {
                        var urlString = downloadUrl.GetString();
                        if (!string.IsNullOrEmpty(urlString))
                        {
                            return new Uri(urlString);
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently fail - update check is not critical
        }

        // Without this app's installer in the release, the release page lets the user choose.
        return await GetLatestReleaseUrlAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        httpClient.Dispose();
        cacheLock.Dispose();
        disposed = true;
    }

    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "OK")]
    private async Task<string?> GetCachedResponseAsync()
    {
        if (cachedResponse is not null)
        {
            return cachedResponse;
        }

        // Bound the wait so a hung HTTP request can't freeze every other
        // caller indefinitely (e.g. update-check on UI thread during startup).
        var acquired = false;
        try
        {
            acquired = await cacheLock
                .WaitAsync(LockAcquisitionTimeout)
                .ConfigureAwait(false);
            if (!acquired)
            {
                return null;
            }

            // Double-check after acquiring lock (another thread may have populated the cache)
            if (cachedResponse is not null)
            {
                return cachedResponse;
            }
        }
        finally
        {
            // Release before the HTTP call so we never hold the lock across
            // a network round-trip; HttpClient.Timeout still bounds the call.
            if (acquired)
            {
                cacheLock.Release();
            }
        }

        try
        {
            var response = await httpClient
                .GetStringAsync(new Uri(GitHubApiUrl))
                .ConfigureAwait(false);

            // Best-effort cache population; concurrent callers may race here
            // but the worst outcome is one extra HTTP request, never a hang.
            cachedResponse = response;
            return response;
        }
        catch
        {
            return null;
        }
    }
}