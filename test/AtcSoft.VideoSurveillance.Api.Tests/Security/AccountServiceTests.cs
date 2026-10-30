namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// The server's users: the first admin made with the setup code from the data
/// folder, sign-in, and sessions renewed with a refresh token that is replaced
/// each time, the replaced one still renewing for a short grace window.
/// </summary>
public sealed class AccountServiceTests : IDisposable
{
    private const string AdminName = "admin";
    private const string AdminPassword = "correct horse battery";

    private readonly string dataPath = Path.Combine(Path.GetTempPath(), "atcsoft-accounts-" + Guid.NewGuid().ToString("N"));
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider time = new(DateTimeOffset.Parse("2026-10-15T12:00:00Z", CultureInfo.InvariantCulture));
    private readonly ServerDataPaths paths;

    public AccountServiceTests()
    {
        Directory.CreateDirectory(dataPath);
        paths = new ServerDataPaths(dataPath);
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
    public void A_New_Server_Needs_Its_First_Admin_And_Writes_A_Setup_Code_Into_Its_Data_Folder()
    {
        var sut = CreateSut();

        sut.IsSetupRequired.Should().BeTrue();
        File.ReadAllText(paths.SetupCodePath).Trim().Should().HaveLength(AccountService.SetupCodeLength);
    }

    [Fact]
    public void The_Setup_Code_Makes_The_First_Admin_And_Is_Gone_Afterwards()
    {
        var sut = CreateSut();

        var (outcome, session) = sut.CompleteSetup(ReadSetupCode(), AdminName, AdminPassword);

        outcome.Should().Be(SetupOutcome.Done);
        session!.Role.Should().Be(AccountRole.Admin);
        session.UserName.Should().Be(AdminName);
        sut.IsSetupRequired.Should().BeFalse();
        File.Exists(paths.SetupCodePath).Should().BeFalse();
    }

    [Fact]
    public void A_Wrong_Setup_Code_Makes_No_One()
    {
        var sut = CreateSut();

        var (outcome, session) = sut.CompleteSetup("not-the-code", AdminName, AdminPassword);

        outcome.Should().Be(SetupOutcome.WrongCode);
        session.Should().BeNull();
        sut.IsSetupRequired.Should().BeTrue();
    }

    [Fact]
    public void A_Server_With_A_User_Cannot_Be_Set_Up_Again()
    {
        var sut = CreateSut();
        var code = ReadSetupCode();
        sut.CompleteSetup(code, AdminName, AdminPassword);

        var (outcome, _) = sut.CompleteSetup(code, "second", AdminPassword);

        outcome.Should().Be(SetupOutcome.AlreadySetUp);
    }

    [Fact]
    public void Signing_In_Takes_The_Right_Password_And_Does_Not_Say_Which_Part_Was_Wrong()
    {
        var sut = CreateSetUpSut();

        sut.SignIn("ADMIN", AdminPassword)!.UserName.Should().Be(AdminName, "the user name is not case sensitive");
        sut.SignIn(AdminName, "wrong password").Should().BeNull();
        sut.SignIn("nobody", AdminPassword).Should().BeNull();
    }

    [Fact]
    public async Task The_Access_Token_Names_The_User_And_Role_And_Lasts_An_Hour()
    {
        var sut = CreateSetUpSut();

        var session = sut.SignIn(AdminName, AdminPassword)!;
        var validated = await new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler()
            .ValidateTokenAsync(session.AccessToken, sut.Tokens.ValidationParameters);

        validated.IsValid.Should().BeTrue();
        validated.ClaimsIdentity.Name.Should().Be(AdminName);
        validated.ClaimsIdentity.HasClaim(validated.ClaimsIdentity.RoleClaimType, "admin").Should().BeTrue();
        session.AccessTokenLifetime.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void A_Refresh_Token_Is_Replaced_By_A_New_One_That_Renews_The_Session()
    {
        var sut = CreateSetUpSut();
        var first = sut.SignIn(AdminName, AdminPassword)!;

        var renewed = sut.Refresh(first.RefreshToken);

        renewed!.RefreshToken.Should().NotBe(first.RefreshToken);
        sut.Refresh(renewed.RefreshToken).Should().NotBeNull();
    }

    [Fact]
    public async Task Renewals_With_The_Same_Token_Within_The_Grace_Window_All_Get_The_Same_New_Token()
    {
        var sut = CreateSetUpSut();
        var first = sut.SignIn(AdminName, AdminPassword)!;

        // Several tabs waking at once.
        var renewals = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => sut.Refresh(first.RefreshToken))));
        time.Advance(TimeSpan.FromSeconds(90));
        var late = sut.Refresh(first.RefreshToken);

        renewals.Should().AllSatisfy(r => r.Should().NotBeNull());
        renewals.Select(r => r!.RefreshToken).Append(late!.RefreshToken).Distinct(StringComparer.Ordinal).Should().ContainSingle();
        renewals[0]!.RefreshToken.Should().NotBe(first.RefreshToken);
        sut.Refresh(late.RefreshToken).Should().NotBeNull("the new token renews the session");
    }

    [Fact]
    public void A_Replay_Within_The_Grace_Window_Leads_To_The_Newest_Token_Of_The_Session()
    {
        var sut = CreateSetUpSut();
        var first = sut.SignIn(AdminName, AdminPassword)!;
        var second = sut.Refresh(first.RefreshToken)!;
        var third = sut.Refresh(second.RefreshToken)!;

        sut.Refresh(first.RefreshToken)!.RefreshToken.Should().Be(third.RefreshToken);
        sut.Refresh(second.RefreshToken)!.RefreshToken.Should().Be(third.RefreshToken);
    }

    [Fact]
    public void A_Replaced_Token_Used_After_The_Grace_Window_Ends_The_Session()
    {
        var sut = CreateSetUpSut();
        var first = sut.SignIn(AdminName, AdminPassword)!;
        var renewed = sut.Refresh(first.RefreshToken)!;

        time.Advance(AccountService.RefreshGraceWindow + TimeSpan.FromSeconds(1));

        sut.Refresh(first.RefreshToken).Should().BeNull("the old token was probably copied");
        sut.Refresh(renewed.RefreshToken).Should().BeNull("the session it belonged to has ended");
    }

    [Fact]
    public void After_A_Restart_A_Replaced_Token_Is_Refused_Within_Its_Window_And_Ends_The_Session_After_It()
    {
        var sut = CreateSetUpSut();
        var first = sut.SignIn(AdminName, AdminPassword)!;
        var renewed = sut.Refresh(first.RefreshToken)!;

        var restarted = CreateSut();

        restarted.Refresh(first.RefreshToken).Should().BeNull("a restart forgets the replays");
        time.Advance(AccountService.RefreshGraceWindow + TimeSpan.FromSeconds(1));
        var still = restarted.Refresh(renewed.RefreshToken);
        still.Should().NotBeNull("a refusal within the window does not end the session");

        time.Advance(AccountService.RefreshGraceWindow + TimeSpan.FromSeconds(1));
        restarted.Refresh(renewed.RefreshToken).Should().BeNull();
        restarted.Refresh(still!.RefreshToken).Should().BeNull("the replaced token was used after its window");
    }

    [Fact]
    public void A_New_Password_Ends_The_Replays_Too()
    {
        var sut = CreateSetUpSut();
        var first = sut.SignIn(AdminName, AdminPassword)!;
        sut.Refresh(first.RefreshToken).Should().NotBeNull();

        sut.ChangeOwnPassword(AdminName, AdminPassword, "another long password").Should().BeTrue();

        sut.Refresh(first.RefreshToken).Should().BeNull();
    }

    [Fact]
    public void Signing_Out_With_The_Replaced_Token_Within_Its_Window_Ends_The_Session()
    {
        var sut = CreateSetUpSut();
        var first = sut.SignIn(AdminName, AdminPassword)!;
        var renewed = sut.Refresh(first.RefreshToken)!;

        sut.SignOut(first.RefreshToken);

        sut.Refresh(renewed.RefreshToken).Should().BeNull();
        sut.Refresh(first.RefreshToken).Should().BeNull();
    }

    [Fact]
    public void Signing_Out_With_The_New_Token_Ends_The_Replays_Too()
    {
        var sut = CreateSetUpSut();
        var first = sut.SignIn(AdminName, AdminPassword)!;
        var renewed = sut.Refresh(first.RefreshToken)!;

        sut.SignOut(renewed.RefreshToken);

        sut.Refresh(first.RefreshToken).Should().BeNull();
        sut.Refresh(renewed.RefreshToken).Should().BeNull();
    }

    [Fact]
    public void A_Session_Not_Renewed_For_Thirty_Days_Has_Run_Out()
    {
        var sut = CreateSetUpSut();
        var session = sut.SignIn(AdminName, AdminPassword)!;

        time.Advance(TimeSpan.FromDays(29));
        var renewed = sut.Refresh(session.RefreshToken)!;
        time.Advance(TimeSpan.FromDays(31));

        sut.Refresh(renewed.RefreshToken).Should().BeNull();
    }

    [Fact]
    public void Signing_Out_Ends_The_Session()
    {
        var sut = CreateSetUpSut();
        var session = sut.SignIn(AdminName, AdminPassword)!;

        sut.SignOut(session.RefreshToken);
        sut.SignOut(session.RefreshToken);

        sut.Refresh(session.RefreshToken).Should().BeNull();
    }

    [Fact]
    public void Users_And_Sessions_Survive_A_Restart_And_No_Password_Or_Token_Is_Kept_As_It_Is()
    {
        var session = CreateSetUpSut().SignIn(AdminName, AdminPassword)!;

        var restarted = CreateSut();

        restarted.IsSetupRequired.Should().BeFalse();
        restarted.SignIn(AdminName, AdminPassword).Should().NotBeNull();
        restarted.Refresh(session.RefreshToken).Should().NotBeNull();
        var stored = File.ReadAllText(paths.AccountsPath);
        stored.Should().NotContain(AdminPassword).And.NotContain(session.RefreshToken);
    }

    [Fact]
    public async Task A_Token_Made_By_Another_Server_Is_Not_Valid_Here()
    {
        var session = CreateSetUpSut().SignIn(AdminName, AdminPassword)!;
        using var otherFolder = new TemporaryFolder();
        var other = new AccountService(new ServerDataPaths(otherFolder.Path), time, NullLogger<AccountService>.Instance);

        var validated = await new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler()
            .ValidateTokenAsync(session.AccessToken, other.Tokens.ValidationParameters);

        validated.IsValid.Should().BeFalse("each server has a signing key of its own");
    }

    private AccountService CreateSut()
        => new(paths, time, NullLogger<AccountService>.Instance);

    private AccountService CreateSetUpSut()
    {
        var sut = CreateSut();
        sut.CompleteSetup(ReadSetupCode(), AdminName, AdminPassword).Outcome.Should().Be(SetupOutcome.Done);
        return sut;
    }

    private string ReadSetupCode()
        => File.ReadAllText(paths.SetupCodePath).Trim();

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-accounts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            foreach (var file in Directory.GetFiles(Path))
            {
                File.Delete(file);
            }

            Directory.Delete(Path);
        }
    }
}