namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// Encrypts the secrets the server keeps in its data folder (camera passwords,
/// webhook secrets) with ASP.NET Core Data Protection, whose keys lie in the
/// data folder too. A stored value starts with <see cref="Prefix"/>; one
/// without it was written before encryption and is read as it is.
/// </summary>
public sealed class SecretProtector
{
    /// <summary>
    /// The start of an encrypted value in a file.
    /// </summary>
    public const string Prefix = "enc:v1:";

    private const string Purpose = "AtcSoft.VideoSurveillance.StoredSecrets.v1";

    private readonly IDataProtector protector;

    public SecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        protector = provider.CreateProtector(Purpose);
    }

    /// <summary>
    /// Gets the value to write to a file; nothing stays nothing.
    /// </summary>
    public string? Protect(string? secret)
        => string.IsNullOrEmpty(secret)
            ? secret
            : Prefix + protector.Protect(secret);

    /// <summary>
    /// Reads a value from a file.
    /// </summary>
    /// <param name="stored">The value in the file.</param>
    /// <param name="secret">The secret; <see langword="null"/> when it cannot be read.</param>
    /// <param name="wasPlain">Whether the value was written before encryption.</param>
    /// <returns><see langword="false"/> when the value was encrypted with keys this server does not have.</returns>
    public bool TryUnprotect(
        string? stored,
        out string? secret,
        out bool wasPlain)
    {
        wasPlain = false;
        if (string.IsNullOrEmpty(stored))
        {
            secret = stored;
            return true;
        }

        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            secret = stored;
            wasPlain = true;
            return true;
        }

        try
        {
            secret = protector.Unprotect(stored[Prefix.Length..]);
            return true;
        }
        catch (CryptographicException)
        {
            secret = null;
            return false;
        }
    }
}