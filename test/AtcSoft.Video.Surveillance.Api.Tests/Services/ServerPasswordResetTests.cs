namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// <c>--reset-password &lt;user&gt;</c> gives a user a new password on the console, and only
/// while no server runs on the data folder: a running server would write over it.
/// </summary>
public sealed class ServerPasswordResetTests : IDisposable
{
    private const string OldPassword = "the old password";

    private readonly string dataPath = Path.Combine(Path.GetTempPath(), "atcsoft-reset-" + Guid.NewGuid().ToString("N"));
    private readonly ServerDataPaths paths;
    private readonly StringWriter output = new();

    public ServerPasswordResetTests()
    {
        Directory.CreateDirectory(dataPath);
        paths = new ServerDataPaths(dataPath);
    }

    [Fact]
    public void The_Switch_And_The_Name_After_It_Are_Taken_Out()
    {
        var found = ServerPasswordReset.TakeArgument(["--urls", "http://+:8585", "--reset-password", "admin"], out var userName, out var rest);

        found.Should().BeTrue();
        userName.Should().Be("admin");
        rest.Should().Equal("--urls", "http://+:8585");
    }

    [Fact]
    public void The_Switch_Without_A_Name_Has_None()
    {
        ServerPasswordReset.TakeArgument(["--reset-password", "--urls", "http://+:8585"], out var userName, out var rest).Should().BeTrue();

        userName.Should().BeNull();
        rest.Should().Equal("--urls", "http://+:8585");
    }

    [Fact]
    public void Without_The_Switch_Nothing_Is_Taken()
    {
        ServerPasswordReset.TakeArgument(["--urls", "http://+:8585"], out _, out var rest).Should().BeFalse();

        rest.Should().Equal("--urls", "http://+:8585");
    }

    [Fact]
    public void A_User_Gets_A_New_Password_That_Signs_In_And_The_Old_One_Does_Not()
    {
        CreateAdmin();

        var exitCode = ServerPasswordReset.Reset(paths, "ADMIN", output);

        exitCode.Should().Be(0, output.ToString());
        var password = output.ToString().Split("is: ")[1].Split(Environment.NewLine)[0];
        password.Should().HaveLength(ServerPasswordReset.PasswordLength);
        var accounts = new AccountService(paths, TimeProvider.System, NullLogger<AccountService>.Instance);
        accounts.SignIn("admin", password).Should().NotBeNull();
        accounts.SignIn("admin", OldPassword).Should().BeNull();
    }

    [Fact]
    public void A_Running_Server_Is_Refused()
    {
        CreateAdmin();
        using var running = ServerLock.TryAcquire(paths.LockPath);

        var exitCode = ServerPasswordReset.Reset(paths, "admin", output);

        exitCode.Should().Be(1);
        output.ToString().Should().Contain("stop it first");
        new AccountService(paths, TimeProvider.System, NullLogger<AccountService>.Instance)
            .SignIn("admin", OldPassword).Should().NotBeNull("nothing was changed");
    }

    [Fact]
    public void An_Unknown_User_Is_Refused_With_The_Users_There_Are()
    {
        CreateAdmin();

        var exitCode = ServerPasswordReset.Reset(paths, "someone", output);

        exitCode.Should().Be(1);
        output.ToString().Should().Contain("someone").And.Contain("admin");
    }

    [Fact]
    public void A_Server_Without_Users_Points_To_The_First_Admin()
    {
        var exitCode = ServerPasswordReset.Reset(paths, "admin", output);

        exitCode.Should().Be(1);
        output.ToString().Should().Contain("no user yet");
    }

    [Fact]
    public void Without_A_Name_It_Says_How()
    {
        ServerPasswordReset.Reset(paths, userName: null, output).Should().Be(2);

        output.ToString().Should().Contain(ServerPasswordReset.Argument);
    }

    public void Dispose()
    {
        output.Dispose();
        try
        {
            Directory.Delete(dataPath, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a temp folder left behind is harmless.
        }
    }

    private void CreateAdmin()
        => new AccountService(paths, TimeProvider.System, NullLogger<AccountService>.Instance)
            .CompleteSetup(setupCode: null, "admin", OldPassword, setupCodeRequired: false)
            .Outcome.Should().Be(SetupOutcome.Done);
}