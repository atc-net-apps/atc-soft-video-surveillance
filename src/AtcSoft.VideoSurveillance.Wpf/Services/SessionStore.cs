namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Keeps the desktop client's session between starts: per server, the refresh
/// token and who it is for, encrypted for the Windows user (DPAPI), in a folder
/// under the user's local application data.
/// </summary>
public sealed class SessionStore
{
    private readonly string folder;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStore"/> class.
    /// </summary>
    /// <param name="folder">The folder the sessions are kept in.</param>
    public SessionStore(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        this.folder = folder;
    }

    /// <summary>
    /// Gets the folder the client keeps its sessions in: under the Windows
    /// user's local application data.
    /// </summary>
    public static string DefaultFolder
        => AtcSoft.VideoSurveillance.Helpers.ApplicationPaths.GetLocalDataPath("VideoSurveillance", "sessions");

    public void Save(
        Uri server,
        string refreshToken,
        string? userName)
    {
        ArgumentNullException.ThrowIfNull(server);

        var json = JsonSerializer.SerializeToUtf8Bytes(new Kept(refreshToken, userName), JsonSerializerOptions.Web);
        var encrypted = ProtectedData.Protect(json, optionalEntropy: null, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(PathFor(server), encrypted);
    }

    /// <summary>
    /// Gets the kept session for a server; <see langword="null"/> when there is
    /// none, or it cannot be read (another Windows user's, a damaged file).
    /// </summary>
    public (string RefreshToken, string? UserName)? Load(Uri server)
    {
        ArgumentNullException.ThrowIfNull(server);

        var path = PathFor(server);
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = ProtectedData.Unprotect(File.ReadAllBytes(path), optionalEntropy: null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Kept>(json, JsonSerializerOptions.Web) is { RefreshToken.Length: > 0 } kept
                ? (kept.RefreshToken, kept.UserName)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {
            return null;
        }
    }

    public void Clear(Uri server)
    {
        ArgumentNullException.ThrowIfNull(server);

        try
        {
            File.Delete(PathFor(server));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Gone with the next save.
        }
    }

    /// <summary>
    /// Forgets the kept session for a server only while it is still the one with
    /// <paramref name="refreshToken"/>: a newer one another instance kept stays.
    /// </summary>
    public void ClearIf(
        Uri server,
        string refreshToken)
    {
        if (Load(server) is { } kept &&
            string.Equals(kept.RefreshToken, refreshToken, StringComparison.Ordinal))
        {
            Clear(server);
        }
    }

    // One file per server; the address in any casing and with or without a
    // trailing slash is the same server.
    private string PathFor(Uri server)
    {
        var key = server.GetLeftPart(UriPartial.Path).TrimEnd('/').ToUpperInvariant();
        var name = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..16];
        return Path.Combine(folder, name + ".session");
    }

    private sealed record Kept(
        string RefreshToken,
        string? UserName);
}