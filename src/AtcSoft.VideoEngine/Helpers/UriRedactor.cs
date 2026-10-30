namespace AtcSoft.VideoEngine.Helpers;

/// <summary>
/// Strips credentials from a URI string for safe logging. RTSP camera
/// URIs typically embed username/password (rtsp://user:pass@host/path);
/// logging the raw <see cref="Uri.AbsoluteUri"/> writes the password to
/// disk in plain text, which is unacceptable for production deployments
/// where log files are shared or shipped to a central collector.
/// </summary>
internal static class UriRedactor
{
    /// <summary>
    /// Returns a string representation of <paramref name="uri"/> with the
    /// userinfo (user:password) replaced by <c>***:***</c>. The host, port,
    /// path, and query are preserved so log lines remain useful for
    /// diagnostics.
    /// </summary>
    public static string Redact(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (string.IsNullOrEmpty(uri.UserInfo))
        {
            return uri.AbsoluteUri;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = "***",
#pragma warning disable S2068 // "password" detected — this is the redaction sentinel, not a credential
            Password = "***",
#pragma warning restore S2068
        };

        return builder.Uri.AbsoluteUri;
    }
}