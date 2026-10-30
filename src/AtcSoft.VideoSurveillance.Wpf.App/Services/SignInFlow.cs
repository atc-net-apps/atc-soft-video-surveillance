namespace AtcSoft.VideoSurveillance.Wpf.App.Services;

/// <summary>
/// Gets the client signed in: the session kept from the last start, else the
/// user name and password from the configuration (<c>Auth:UserName</c> /
/// <c>Auth:Password</c>, for a development run that starts it unattended), else
/// the sign-in dialog, which on a new server creates the first admin.
/// </summary>
public sealed class SignInFlow(
    AuthService auth,
    GatewayService gateway,
    IConfiguration configuration)
{
    /// <summary>
    /// Signs in; <see langword="false"/> when the user closed the dialog.
    /// </summary>
    public async Task<bool> EnsureSignedInAsync(Window? owner)
    {
        if (await auth.RestoreAsync().ConfigureAwait(true))
        {
            return true;
        }

        if (configuration["Auth:UserName"] is { Length: > 0 } userName &&
            configuration["Auth:Password"] is { Length: > 0 } password &&
            await auth.SignInAsync(userName, password).ConfigureAwait(true) is null)
        {
            return true;
        }

        return await ShowDialogAsync(owner).ConfigureAwait(true);
    }

    /// <summary>
    /// Shows the sign-in dialog; <see langword="false"/> when the user closed it.
    /// </summary>
    public async Task<bool> ShowDialogAsync(Window? owner)
    {
        var setupRequired = await gateway.IsSetupRequiredAsync().ConfigureAwait(true) == true;
        var viewModel = new AtcSoft.VideoSurveillance.Wpf.Dialogs.SignInDialogViewModel(setupRequired, auth.SignInAsync, auth.CompleteSetupAsync);
        var dialog = new AtcSoft.VideoSurveillance.Wpf.Dialogs.SignInDialog(viewModel);
        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        return dialog.ShowDialog() is true;
    }
}