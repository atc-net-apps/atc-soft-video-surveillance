namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// Signs a webhook body, so the receiver can tell the request came from
/// this server and was not changed on the way.
/// </summary>
public static class WebhookSignature
{
    /// <summary>
    /// Computes the value of the signature header: <c>sha256=</c> followed
    /// by the HMAC-SHA256 of the body, keyed with the secret, in lower-case hex.
    /// </summary>
    /// <param name="secret">The shared secret.</param>
    /// <param name="body">The request body, exactly as it is sent.</param>
    /// <returns>The header value.</returns>
    public static string Compute(
        string secret,
        ReadOnlySpan<byte> body)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var hash = System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret), body);
        return "sha256=" + Convert.ToHexStringLower(hash);
    }
}