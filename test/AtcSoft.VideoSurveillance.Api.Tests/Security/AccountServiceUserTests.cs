namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// An admin manages the users and the API keys: the server always keeps one
/// admin, and a password that changes ends the user's sessions.
/// </summary>
public sealed class AccountServiceUserTests : IDisposable
{
    private const string AdminPassword = "correct horse battery";

    private readonly string dataPath = Path.Combine(Path.GetTempPath(), "atcsoft-accounts-" + Guid.NewGuid().ToString("N"));
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider time = new(DateTimeOffset.Parse("2026-10-15T12:00:00Z", CultureInfo.InvariantCulture));
    private readonly AccountService sut;

    public AccountServiceUserTests()
    {
        Directory.CreateDirectory(dataPath);
        var paths = new ServerDataPaths(dataPath);
        sut = new AccountService(paths, time, NullLogger<AccountService>.Instance);
        sut.CompleteSetup(File.ReadAllText(paths.SetupCodePath), "admin", AdminPassword).Outcome.Should().Be(SetupOutcome.Done);
    }

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(dataPath))
        {
            File.Delete(file);
        }

        Directory.Delete(dataPath);
    }

    [Fact]
    public void A_User_Is_Added_With_A_Role_And_Signs_In_With_It()
    {
        var (outcome, user) = sut.CreateUser("Ole", "viewer password", AccountRole.Viewer);

        outcome.Should().Be(UserChangeOutcome.Done);
        user!.Role.Should().Be(AccountRole.Viewer);
        sut.SignIn("ole", "viewer password")!.Role.Should().Be(AccountRole.Viewer);
        sut.ListUsers().Select(u => u.UserName).Should().Equal("admin", "Ole");
    }

    [Fact]
    public void Two_Users_Cannot_Have_The_Same_Name()
    {
        sut.CreateUser("Ole", "first password", AccountRole.Viewer);

        sut.CreateUser("OLE", "second password", AccountRole.Admin).Outcome.Should().Be(UserChangeOutcome.NameTaken);
    }

    [Fact]
    public void The_Last_Admin_Can_Neither_Be_Removed_Nor_Lose_The_Role()
    {
        var admin = sut.ListUsers().Single();

        sut.DeleteUser(admin.Id).Should().Be(UserChangeOutcome.LastAdmin);
        sut.UpdateUser(admin.Id, AccountRole.Viewer, password: null).Outcome.Should().Be(UserChangeOutcome.LastAdmin);

        var (_, second) = sut.CreateUser("second", "another password", AccountRole.Admin);
        sut.DeleteUser(admin.Id).Should().Be(UserChangeOutcome.Done, "another admin is left");
        sut.DeleteUser(second!.Id).Should().Be(UserChangeOutcome.LastAdmin);
    }

    [Fact]
    public void A_New_Password_Ends_The_Users_Sessions()
    {
        var (_, user) = sut.CreateUser("Ole", "old password", AccountRole.Operator);
        var session = sut.SignIn("Ole", "old password")!;

        sut.UpdateUser(user!.Id, role: null, password: "new password").Outcome.Should().Be(UserChangeOutcome.Done);

        sut.Refresh(session.RefreshToken).Should().BeNull();
        sut.SignIn("Ole", "old password").Should().BeNull();
        sut.SignIn("Ole", "new password")!.Role.Should().Be(AccountRole.Operator, "a change of password leaves the role");
    }

    [Fact]
    public void A_Removed_User_Can_No_Longer_Renew_A_Session()
    {
        var (_, user) = sut.CreateUser("Ole", "a password here", AccountRole.Viewer);
        var session = sut.SignIn("Ole", "a password here")!;

        sut.DeleteUser(user!.Id).Should().Be(UserChangeOutcome.Done);

        sut.Refresh(session.RefreshToken).Should().BeNull();
        sut.DeleteUser(user.Id).Should().Be(UserChangeOutcome.NotFound);
    }

    [Fact]
    public void A_User_Changes_Their_Own_Password_With_The_Current_One()
    {
        sut.ChangeOwnPassword("admin", "not the password", "a new password").Should().BeFalse();
        sut.ChangeOwnPassword("admin", AdminPassword, "a new password").Should().BeTrue();

        sut.SignIn("admin", "a new password").Should().NotBeNull();
    }

    [Fact]
    public void An_Api_Key_Is_Shown_Once_And_Signs_In_With_Its_Role_Until_It_Is_Removed()
    {
        var (key, secret) = sut.CreateApiKey("Home Assistant", AccountRole.Viewer);

        sut.FindApiKey(secret)!.Name.Should().Be("Home Assistant");
        sut.FindApiKey(secret)!.Role.Should().Be(AccountRole.Viewer);
        sut.FindApiKey(secret + "x").Should().BeNull();
        sut.ListApiKeys().Single().Id.Should().Be(key.Id);
        File.ReadAllText(Path.Combine(dataPath, "accounts.json")).Should().NotContain(secret);

        sut.DeleteApiKey(key.Id).Should().BeTrue();
        sut.FindApiKey(secret).Should().BeNull();
        sut.DeleteApiKey(key.Id).Should().BeFalse();
    }
}