namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// What the browser keeps of a session between visits: the refresh token and
/// who it is for (never the access token or a password).
/// </summary>
public sealed record StoredSession(
    string RefreshToken,
    string? UserName)
{
    public static string Write(
        string refreshToken,
        string? userName)
        => JsonSerializer.Serialize(new StoredSession(refreshToken, userName), JsonSerializerOptions.Web);

    /// <summary>
    /// Reads what was kept; <see langword="null"/> for nothing, or for something
    /// that is not a session (an older format, a hand edit).
    /// </summary>
    public static StoredSession? Read(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredSession>(stored, JsonSerializerOptions.Web) is { RefreshToken.Length: > 0 } session
                ? session
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}