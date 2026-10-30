namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// Encrypts a secret (a camera password) for a file the desktop app keeps, with Windows data
/// protection for the machine: every user of the computer can use the cameras, a copy of the
/// file on another computer or in a backup gives nothing away. A plain value from an older
/// file is read as it is.
/// </summary>
public static class StoredSecret
{
    /// <summary>
    /// The start of an encrypted value in a file.
    /// </summary>
    public const string Prefix = "dpapi:v1:";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AtcSoft.VideoSurveillance.CameraWall.StoredSecrets.v1");

    /// <summary>
    /// Gets whether the value in a file is encrypted.
    /// </summary>
    public static bool IsProtected(string? value)
        => value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Gets the value to write to the file; nothing stays nothing, and an encrypted value stays as it is.
    /// </summary>
    public static string? Protect(string? value)
    {
        if (string.IsNullOrEmpty(value) || IsProtected(value))
        {
            return value;
        }

        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.LocalMachine);
        return Prefix + Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Gets the plain value of a value read from the file: an encrypted one decrypted, a plain
    /// one as it is, and an encrypted one this machine cannot read as <see langword="null"/>.
    /// </summary>
    public static string? Unprotect(string? value)
    {
        if (!IsProtected(value))
        {
            return value;
        }

        try
        {
            var encrypted = Convert.FromBase64String(value![Prefix.Length..]);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.LocalMachine));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}