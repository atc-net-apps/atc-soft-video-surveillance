namespace AtcSoft.Video.Surveillance.Api.Handlers.Auth;

/// <summary>
/// The setup handlers ask for the setup code from everyone but a browser on the server's own
/// machine, and tell the clients which it is.
/// </summary>
public sealed class SetupHandlerTests
{
    private readonly IAccountService accounts = Substitute.For<IAccountService>();
    private readonly IHttpContextAccessor httpContextAccessor = Substitute.For<IHttpContextAccessor>();

    public SetupHandlerTests()
    {
        accounts.IsSetupRequired.Returns(true);
        accounts
            .CompleteSetup(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns((SetupOutcome.WrongCode, null));
    }

    [Theory]
    [InlineData("127.0.0.1", true, false)]
    [InlineData("192.168.1.20", true, true)]
    [InlineData("127.0.0.1", false, true)]
    public async Task The_Status_Says_Whether_This_Caller_Needs_The_Code(
        string address,
        bool allowLocal,
        bool expected)
    {
        From(address);
        var sut = new GetSetupStatusHandler(accounts, new LocalSetup(allowLocal), httpContextAccessor);

        var result = await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        var status = result.Result.Should().BeOfType<Ok<SetupStatus>>().Subject.Value!;
        status.SetupRequired.Should().BeTrue();
        status.SetupCodeRequired.Should().Be(expected);
        status.SetupCodeFile.Should().Be(LocalSetup.SetupCodeFileName);
    }

    [Fact]
    public async Task A_Server_With_Users_Asks_No_One_For_A_Code()
    {
        accounts.IsSetupRequired.Returns(false);
        From("192.168.1.20");
        var sut = new GetSetupStatusHandler(accounts, new LocalSetup(allowed: true), httpContextAccessor);

        var result = await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        result.Result.Should().BeOfType<Ok<SetupStatus>>().Subject.Value!.SetupCodeRequired.Should().BeFalse();
    }

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("192.168.1.20", true)]
    public async Task Setup_Asks_The_Accounts_For_The_Code_Unless_The_Caller_Is_Local(
        string address,
        bool expected)
    {
        From(address);
        var sut = new CompleteSetupHandler(accounts, Substitute.For<IAuditLog>(), new LocalSetup(allowed: true), httpContextAccessor);

        await sut.ExecuteAsync(
            new CompleteSetupParameters(new CompleteSetupRequest(UserName: "admin", Password: "a long password")),
            TestContext.Current.CancellationToken);

        accounts.Received(1).CompleteSetup(null, "admin", "a long password", expected);
    }

    [Fact]
    public async Task A_Refused_Setup_Names_The_File_With_The_Code()
    {
        From("192.168.1.20");
        var sut = new CompleteSetupHandler(accounts, Substitute.For<IAuditLog>(), new LocalSetup(allowed: true), httpContextAccessor);

        var result = await sut.ExecuteAsync(
            new CompleteSetupParameters(new CompleteSetupRequest(UserName: "admin", Password: "a long password", SetupCode: "WRONG")),
            TestContext.Current.CancellationToken);

        var problem = result.Result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Detail.Should().Contain(LocalSetup.SetupCodeFileName);
    }

    private void From(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        context.Request.Host = new HostString("localhost", 8585);
        httpContextAccessor.HttpContext.Returns(context);
    }
}