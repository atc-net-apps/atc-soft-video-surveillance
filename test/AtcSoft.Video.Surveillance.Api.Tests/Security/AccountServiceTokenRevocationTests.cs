namespace AtcSoft.Video.Surveillance.Api.Security;

/// <summary>
/// A token outlives nothing that ended it: a sign-out, a deleted user, a new
/// password or a changed role takes effect on the next request, not when the
/// token runs out an hour (a media token twelve hours) later.
/// </summary>
public sealed class AccountServiceTokenRevocationTests : IDisposable
{
    private const string AdminPassword = "correct horse battery";
    private const string ViewerPassword = "viewer password";

    private readonly string dataPath = Path.Combine(Path.GetTempPath(), "atcsoft-accounts-" + Guid.NewGuid().ToString("N"));
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider time = new(DateTimeOffset.Parse("2026-10-15T12:00:00Z", CultureInfo.InvariantCulture));
    private readonly AccountService sut;
    private readonly Guid operatorId;

    public AccountServiceTokenRevocationTests()
    {
        Directory.CreateDirectory(dataPath);
        var paths = new ServerDataPaths(dataPath);
        sut = new AccountService(paths, time, NullLogger<AccountService>.Instance);
        sut.CompleteSetup(File.ReadAllText(paths.SetupCodePath), "admin", AdminPassword).Outcome.Should().Be(SetupOutcome.Done);
        operatorId = sut.CreateUser("ole", ViewerPassword, AccountRole.Operator).User!.Id;
    }

    public void Dispose()
        => Directory.Delete(dataPath, recursive: true);

    [Fact]
    public async Task A_Fresh_Session_Has_Tokens_That_Hold()
    {
        var session = sut.SignIn("ole", ViewerPassword)!;

        sut.IsAccessTokenCurrent(await ReadAccessTokenAsync(session.AccessToken)).Should().BeTrue();
        (await sut.IsValidMediaTokenAsync(session.MediaToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Signing_Out_Ends_The_Access_And_Media_Tokens_At_Once()
    {
        var session = sut.SignIn("ole", ViewerPassword)!;
        var other = sut.SignIn("ole", ViewerPassword)!;

        sut.SignOut(session.RefreshToken);

        sut.IsAccessTokenCurrent(await ReadAccessTokenAsync(session.AccessToken)).Should().BeFalse();
        (await sut.IsValidMediaTokenAsync(session.MediaToken)).Should().BeFalse();
        sut.IsAccessTokenCurrent(await ReadAccessTokenAsync(other.AccessToken)).Should().BeTrue("the user's other session goes on");
    }

    [Fact]
    public async Task A_Deleted_User_Is_Refused_At_Once()
    {
        var session = sut.SignIn("ole", ViewerPassword)!;

        sut.DeleteUser(operatorId).Should().Be(UserChangeOutcome.Done);

        sut.IsAccessTokenCurrent(await ReadAccessTokenAsync(session.AccessToken)).Should().BeFalse();
        (await sut.IsValidMediaTokenAsync(session.MediaToken)).Should().BeFalse();
    }

    [Fact]
    public async Task A_New_Password_Ends_Every_Token_Of_The_User()
    {
        var session = sut.SignIn("ole", ViewerPassword)!;

        sut.UpdateUser(operatorId, role: null, password: "a new password").Outcome.Should().Be(UserChangeOutcome.Done);

        sut.IsAccessTokenCurrent(await ReadAccessTokenAsync(session.AccessToken)).Should().BeFalse();
        (await sut.IsValidMediaTokenAsync(session.MediaToken)).Should().BeFalse();
    }

    [Fact]
    public async Task A_Changed_Role_Refuses_The_Old_Token_And_A_Renewed_Session_Has_The_New_Role()
    {
        var session = sut.SignIn("ole", ViewerPassword)!;

        sut.UpdateUser(operatorId, AccountRole.Viewer, password: null).Outcome.Should().Be(UserChangeOutcome.Done);

        sut.IsAccessTokenCurrent(await ReadAccessTokenAsync(session.AccessToken)).Should().BeFalse("it still says operator");
        (await sut.IsValidMediaTokenAsync(session.MediaToken)).Should().BeTrue("a media token only reads recordings, which a viewer may");

        var renewed = sut.Refresh(session.RefreshToken)!;
        renewed.Role.Should().Be(AccountRole.Viewer);
        sut.IsAccessTokenCurrent(await ReadAccessTokenAsync(renewed.AccessToken)).Should().BeTrue();
    }

    [Fact]
    public async Task A_Renewed_Session_Keeps_Its_Tokens_Valid()
    {
        var session = sut.SignIn("ole", ViewerPassword)!;

        var renewed = sut.Refresh(session.RefreshToken)!;

        sut.IsAccessTokenCurrent(await ReadAccessTokenAsync(renewed.AccessToken)).Should().BeTrue();
        sut.IsAccessTokenCurrent(await ReadAccessTokenAsync(session.AccessToken)).Should().BeTrue("the session is the same; only its refresh token changed");
    }

    [Fact]
    public async Task A_Session_That_Ran_Out_Ends_Its_Tokens()
    {
        var session = sut.SignIn("ole", ViewerPassword)!;

        time.Advance(TimeSpan.FromDays(400));

        (await sut.IsValidMediaTokenAsync(session.MediaToken)).Should().BeFalse();
    }

    [Fact]
    public void A_Token_Without_A_Session_From_An_Older_Server_Holds_While_Its_User_Does()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(AccessTokens.UserIdClaim, operatorId.ToString()),
                new Claim(CallerIdentity.NameClaim, "ole"),
                new Claim(CallerIdentity.RoleClaim, "operator"),
            ],
            "test");

        sut.IsAccessTokenCurrent(new ClaimsPrincipal(identity)).Should().BeTrue();

        sut.DeleteUser(operatorId);

        sut.IsAccessTokenCurrent(new ClaimsPrincipal(identity)).Should().BeFalse();
    }

    [Fact]
    public async Task A_Media_Token_Is_Not_An_Access_Token_And_A_Forged_One_Is_Refused()
    {
        var session = sut.SignIn("ole", ViewerPassword)!;

        (await sut.IsValidMediaTokenAsync(session.AccessToken)).Should().BeFalse();
        (await sut.IsValidMediaTokenAsync(session.MediaToken + "x")).Should().BeFalse();
        (await sut.IsValidMediaTokenAsync(null)).Should().BeFalse();
    }

    private async Task<ClaimsPrincipal> ReadAccessTokenAsync(string token)
    {
        var validated = await new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler()
            .ValidateTokenAsync(token, sut.Tokens.ValidationParameters);
        validated.IsValid.Should().BeTrue("the signature and lifetime are fine; only the session decides");
        return new ClaimsPrincipal(validated.ClaimsIdentity);
    }
}