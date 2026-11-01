namespace AtcSoft.Video.Surveillance.Api.Security;

/// <summary>
/// The server's secrets on disk (the token signing key, the accounts, the
/// setup code, the keys the stored passwords are encrypted with) are kept
/// from the machine's other users. By default the server's data folder is the
/// shared <c>%ProgramData%\AtcSoft\VideoSurveillance</c>, which every local user
/// may read, and the desktop client's installer lets them change it too. A
/// secret gets access of its own instead: the local system, the
/// administrators and the account the server runs as, nothing inherited.
/// </summary>
public static class SecretFile
{
    /// <summary>
    /// Writes a secret file, created with restricted access so it is never
    /// readable by anyone else, not even for a moment. A file already there is
    /// replaced in one step: the text is written to a new restricted file next
    /// to it, flushed to the disk, and moved over it, so a crash or a power cut
    /// leaves the old file or the new one, never none.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="contents">The text to write.</param>
    public static void WriteAllText(
        string path,
        string contents)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(contents);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var temp = path + ".new-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Created with its own access; a move keeps it.
                using var stream = new FileInfo(temp).Create(
                    FileMode.CreateNew,
                    FileSystemRights.Write | FileSystemRights.ReadData | FileSystemRights.Synchronize,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.None,
                    CreateFileSecurity());
                using var writer = new StreamWriter(stream);
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            else
            {
                using var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Left behind; it holds nothing the file it was for does not.
            }

            throw;
        }
    }

    /// <summary>
    /// Restricts a secret file that already exists, or a folder of them (its
    /// files and the files added to it later get the same access).
    /// </summary>
    /// <param name="path">The file or folder.</param>
    /// <returns>
    /// <see langword="true"/> when it is restricted or does not exist;
    /// <see langword="false"/> when its access could not be changed.
    /// </returns>
    public static bool TryRestrict(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            if (Directory.Exists(path))
            {
                new DirectoryInfo(path).SetAccessControl(CreateDirectorySecurity());
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    new FileInfo(file).SetAccessControl(CreateFileSecurity());
                }
            }
            else if (File.Exists(path))
            {
                new FileInfo(path).SetAccessControl(CreateFileSecurity());
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a folder for secrets with restricted access, or restricts the folder that is there.
    /// </summary>
    /// <param name="path">The folder.</param>
    /// <returns><see langword="false"/> when its access could not be changed.</returns>
    public static bool TryCreateRestrictedDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        Directory.CreateDirectory(path);
        return TryRestrict(path);
    }

    /// <summary>
    /// Gets the accounts allowed to a secret: the local system, the administrators
    /// and the account the server runs as.
    /// </summary>
    /// <returns>The accounts.</returns>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<SecurityIdentifier> AllowedAccounts()
    {
        var accounts = new List<SecurityIdentifier>
        {
            new(WellKnownSidType.LocalSystemSid, domainSid: null),
            new(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null),
        };

        using var identity = WindowsIdentity.GetCurrent();
        if (identity.User is { } user && !accounts.Contains(user))
        {
            accounts.Add(user);
        }

        return accounts;
    }

    [SupportedOSPlatform("windows")]
    private static FileSecurity CreateFileSecurity()
    {
        var security = new FileSecurity();

        // Protected: nothing is inherited from the data folder.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var account in AllowedAccounts())
        {
            security.AddAccessRule(new FileSystemAccessRule(account, FileSystemRights.FullControl, AccessControlType.Allow));
        }

        return security;
    }

    [SupportedOSPlatform("windows")]
    private static DirectorySecurity CreateDirectorySecurity()
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var account in AllowedAccounts())
        {
            security.AddAccessRule(new FileSystemAccessRule(
                account,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        return security;
    }
}