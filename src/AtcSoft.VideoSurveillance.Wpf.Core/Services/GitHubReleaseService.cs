// ReSharper disable RedundantArgumentDefaultValue
namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Service for checking GitHub releases for the AtcSoft.VideoSurveillance repository.
/// </summary>
[Registration(Lifetime.Singleton)]
public sealed class GitHubReleaseService : IGitHubReleaseService, IDisposable
{
    private const string GitHubApiUrl = "https://api.github.com/repos/davidkallesen/AtcSoft.VideoSurveillance/releases/latest";
    private const string UserAgent = "AtcSoft-VideoSurveillance";
    private static readonly TimeSpan LockAcquisitionTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HttpRequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient httpClient;
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
    }

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
                        assetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) &&
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

        // Fall back to release page URL
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