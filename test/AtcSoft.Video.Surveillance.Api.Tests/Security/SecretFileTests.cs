namespace AtcSoft.Video.Surveillance.Api.Security;

/// <summary>
/// The server's secrets live in a data folder every local user may read by
/// default, and the desktop client's installer lets them change it. A secret
/// file gets access of its own: nothing inherited from the folder.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SecretFileTests : IDisposable
{
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, domainSid: null);

    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-secret-" + Guid.NewGuid().ToString("N"));

    public SecretFileTests()
    {
        // Like %ProgramData%: the machine's users may read what is created in it.
        Directory.CreateDirectory(folder);
        var info = new DirectoryInfo(folder);
        var security = info.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            Users,
            FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        info.SetAccessControl(security);
    }

    public void Dispose()
        => Directory.Delete(folder, recursive: true);

    [Fact]
    public void A_Plain_File_In_The_Folder_Is_Readable_By_Every_User()
    {
        // The premise of the other tests.
        var path = Path.Combine(folder, "plain.txt");
        File.WriteAllText(path, "x");

        Accounts(new FileInfo(path).GetAccessControl()).Should().Contain(Users);
    }

    [Fact]
    public void A_Secret_Is_Written_Readable_Only_By_The_System_The_Administrators_And_The_Server()
    {
        var path = Path.Combine(folder, "signing.key");

        SecretFile.WriteAllText(path, "secret");

        File.ReadAllText(path).Should().Be("secret");
        var security = new FileInfo(path).GetAccessControl();
        security.AreAccessRulesProtected.Should().BeTrue("nothing is inherited from the folder");
        Accounts(security).Should().BeEquivalentTo(SecretFile.AllowedAccounts());
    }

    [Fact]
    public void A_Secret_That_Is_Written_Again_Keeps_Its_Restricted_Access()
    {
        var path = Path.Combine(folder, "setup-code.txt");
        File.WriteAllText(path, "old");

        SecretFile.WriteAllText(path, "new");

        File.ReadAllText(path).Should().Be("new");
        Accounts(new FileInfo(path).GetAccessControl()).Should().NotContain(Users);
        Directory.GetFiles(folder).Should().ContainSingle("the new text is moved over the old file, nothing is left beside it");
    }

    [Fact]
    public void A_Secret_An_Older_Server_Wrote_Is_Restricted()
    {
        var path = Path.Combine(folder, "accounts.json");
        File.WriteAllText(path, "{}");

        SecretFile.TryRestrict(path).Should().BeTrue();

        Accounts(new FileInfo(path).GetAccessControl()).Should().NotContain(Users);
        File.ReadAllText(path).Should().Be("{}", "the server itself can still read it");
    }

    [Fact]
    public void A_Folder_Of_Secrets_Is_Restricted_With_Its_Files_And_Those_Added_Later()
    {
        var keys = Path.Combine(folder, "keys");
        Directory.CreateDirectory(keys);
        var existing = Path.Combine(keys, "key-1.xml");
        File.WriteAllText(existing, "<key />");

        SecretFile.TryCreateRestrictedDirectory(keys).Should().BeTrue();
        var added = Path.Combine(keys, "key-2.xml");
        File.WriteAllText(added, "<key />");

        Accounts(new DirectoryInfo(keys).GetAccessControl()).Should().NotContain(Users);
        Accounts(new FileInfo(existing).GetAccessControl()).Should().NotContain(Users);
        Accounts(new FileInfo(added).GetAccessControl()).Should().NotContain(Users);
    }

    [Fact]
    public void A_Json_File_Written_Safely_With_The_Secret_Writer_Stays_Restricted_Over_Saves()
    {
        var path = Path.Combine(folder, "accounts.json");

        SafeJsonFile.TryWrite(path, new { Users = 1 }, writeTempFile: SecretFile.WriteAllText).Should().BeTrue();
        SafeJsonFile.TryWrite(path, new { Users = 2 }, writeTempFile: SecretFile.WriteAllText).Should().BeTrue();

        Accounts(new FileInfo(path).GetAccessControl()).Should().NotContain(Users);
        Accounts(new FileInfo(path + ".bak").GetAccessControl()).Should().NotContain(Users, "the backup holds the same secrets");
    }

    [Fact]
    public void A_Missing_Secret_Counts_As_Restricted()
        => SecretFile.TryRestrict(Path.Combine(folder, "missing.key")).Should().BeTrue();

    private static List<SecurityIdentifier> Accounts(
        FileSystemSecurity security)
        => security
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Where(r => r.AccessControlType == AccessControlType.Allow)
            .Select(r => (SecurityIdentifier)r.IdentityReference)
            .Distinct()
            .ToList();
}