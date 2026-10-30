namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// The signed-in user changes their own password with the current one; the
/// server's reason is shown when it refused.
/// </summary>
public sealed class ChangePasswordDialogViewModelTests
{
    [Fact]
    public async Task A_Wrong_Current_Password_Shows_The_Servers_Reason_And_Stays_Open()
    {
        var closed = false;
        var sut = new ChangePasswordDialogViewModel((_, _) => Task.FromResult<string?>("The current password is not right."));
        sut.CloseRequested += (_, _) => closed = true;
        sut.CurrentPassword = "wrong";
        sut.NewPassword = "a new password";

        await sut.SaveAsync();

        sut.Problem.Should().Be("The current password is not right.");
        closed.Should().BeFalse();
    }

    [Fact]
    public async Task A_Changed_Password_Closes_The_Dialog()
    {
        var changed = (string.Empty, string.Empty);
        bool? result = null;
        var sut = new ChangePasswordDialogViewModel((current, next) =>
        {
            changed = (current, next);
            return Task.FromResult<string?>(null);
        });
        sut.CloseRequested += (_, e) => result = e.DialogResult;
        sut.CurrentPassword = "old password";
        sut.NewPassword = "a new password";

        await sut.SaveAsync();

        changed.Should().Be(("old password", "a new password"));
        result.Should().BeTrue();
    }
}