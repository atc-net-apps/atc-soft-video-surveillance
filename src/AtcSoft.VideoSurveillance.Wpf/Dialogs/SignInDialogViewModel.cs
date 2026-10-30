namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// View model for the sign-in dialog: signs in, or on a new server creates the
/// first admin with the setup code, and shows the server's reason when it refused.
/// </summary>
public partial class SignInDialogViewModel : ViewModelBase
{
    private readonly Func<string, string, Task<string?>> signIn;
    private readonly Func<string, string, string, Task<string?>> completeSetup;

    [ObservableProperty]
    private string setupCode = string.Empty;

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string? problem;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignInDialogViewModel"/> class.
    /// </summary>
    /// <param name="setupRequired">The server has no user yet: the dialog creates the first admin.</param>
    /// <param name="signIn">Signs in (user name, password); returns the server's reason when it refused.</param>
    /// <param name="completeSetup">Creates the first admin (setup code, user name, password); returns the server's reason when it refused.</param>
    public SignInDialogViewModel(
        bool setupRequired,
        Func<string, string, Task<string?>> signIn,
        Func<string, string, string, Task<string?>> completeSetup)
    {
        IsSetup = setupRequired;
        this.signIn = signIn;
        this.completeSetup = completeSetup;
    }

    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    /// <summary>
    /// Gets a value indicating whether the dialog creates the first admin.
    /// </summary>
    public bool IsSetup { get; }

    public string Title
        => IsSetup ? Translations.CreateTheFirstAdmin : Translations.SignInToTheServer;

    public string Help
        => IsSetup ? Translations.CreateTheFirstAdminHelp : string.Empty;

    [RelayCommand("SignIn")]
    public async Task SignInAsync()
    {
        IsBusy = true;
        Problem = null;

        try
        {
            Problem = IsSetup
                ? await completeSetup(SetupCode.Trim(), UserName.Trim(), Password).ConfigureAwait(true)
                : await signIn(UserName.Trim(), Password).ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            // The server could not be reached.
            Problem = ex.Message;
        }

        Password = string.Empty;
        IsBusy = false;

        if (Problem is null)
        {
            CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));
        }
    }

    [RelayCommand]
    private void Cancel()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: false));
}