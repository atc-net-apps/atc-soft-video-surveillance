namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// The sign-in dialog signs in, or creates the first admin on a new server,
/// and shows the server's reason when it refused.
/// </summary>
public sealed class SignInDialogViewModelTests
{
    [Fact]
    public async Task A_Refused_Sign_In_Shows_The_Servers_Reason_And_Stays_Open()
    {
        var closed = false;
        var sut = new SignInDialogViewModel(
            setupRequired: false,
            signIn: (_, _) => Task.FromResult<string?>("The user name or the password is not right."),
            completeSetup: (_, _, _) => Task.FromResult<string?>(null));
        sut.CloseRequested += (_, _) => closed = true;
        sut.UserName = "admin";
        sut.Password = "wrong";

        await sut.SignInAsync();

        sut.Problem.Should().Be("The user name or the password is not right.");
        closed.Should().BeFalse();
        sut.Password.Should().BeEmpty("a refused password is not left in the box");
    }

    [Fact]
    public async Task A_Sign_In_Closes_The_Dialog()
    {
        var signedIn = (string.Empty, string.Empty);
        bool? result = null;
        var sut = new SignInDialogViewModel(
            setupRequired: false,
            signIn: (user, password) =>
            {
                signedIn = (user, password);
                return Task.FromResult<string?>(null);
            },
            completeSetup: (_, _, _) => Task.FromResult<string?>("not called"));
        sut.CloseRequested += (_, e) => result = e.DialogResult;
        sut.UserName = "admin";
        sut.Password = "the password";

        await sut.SignInAsync();

        signedIn.Should().Be(("admin", "the password"));
        result.Should().BeTrue();
    }

    [Fact]
    public async Task On_A_New_Server_The_Setup_Code_Creates_The_First_Admin()
    {
        var setup = (string.Empty, string.Empty, string.Empty);
        var sut = new SignInDialogViewModel(
            setupRequired: true,
            signIn: (_, _) => Task.FromResult<string?>("not called"),
            completeSetup: (code, user, password) =>
            {
                setup = (code, user, password);
                return Task.FromResult<string?>(null);
            });
        sut.SetupCode = "ABCD";
        sut.UserName = "admin";
        sut.Password = "a long password";

        await sut.SignInAsync();

        sut.IsSetup.Should().BeTrue();
        sut.Title.Should().Be("Create the first admin");
        setup.Should().Be(("ABCD", "admin", "a long password"));
    }
}