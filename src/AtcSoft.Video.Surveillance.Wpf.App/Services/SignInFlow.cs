namespace AtcSoft.Video.Surveillance.Wpf.App.Services;

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
    // Where a server keeps the setup code when it does not say (one from before it did).
    private const string OlderServerSetupCodeFile = "setup-code.txt";

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
        // A server from before it named the file always wants the code, and keeps it in the
        // file it always had.
        var status = await gateway.GetSetupStatusAsync().ConfigureAwait(true);
        var olderServer = string.IsNullOrEmpty(status?.SetupCodeFile);
        var viewModel = new AtcSoft.Video.Surveillance.Wpf.Dialogs.SignInDialogViewModel(
            setupRequired: status?.SetupRequired == true,
            signIn: auth.SignInAsync,
            completeSetup: auth.CompleteSetupAsync,
            setupCodeRequired: olderServer || status!.SetupCodeRequired,
            setupCodeFile: olderServer ? OlderServerSetupCodeFile : status!.SetupCodeFile);
        var dialog = new AtcSoft.Video.Surveillance.Wpf.Dialogs.SignInDialog(viewModel);
        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        return dialog.ShowDialog() is true;
    }
}